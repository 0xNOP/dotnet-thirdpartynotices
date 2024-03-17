using DotnetThirdPartyNotices.Models;
using DotnetThirdPartyNotices.Services;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Commands;

internal class ScanCommand : Command
{
    public ScanCommand() : base("scan", "A tool to generate file with third party legal notices for .NET projects")
    {
        AddArgument(new Argument<string>("scan-dir", "Path of the directory to look for projects (optional)") { Arity = ArgumentArity.ZeroOrOne });
        AddOption(new Option<string>("--output-filename", () => "third-party-notices.txt", "Output filename"));
        AddOption(new Option<bool>("--copy-to-outdir", () => false, "Copy output file to output directory in selected configuration"));
        AddOption(new Option<string>("--filter", () => string.Empty, "Filter project files"));
        AddOption(new Option<string>("--github-token", () => string.Empty, "GitHub's token"));
        AddOption(new Option<string>("--configuration", () => "Release", "Project configuration to use"));
    }

    internal new class Handler(ILogger<Handler> logger, IProjectService projectService, ILicenseService licenseService) : ICommandHandler
    {
        public string? ScanDir { get; set; }
        public string? OutputFilename { get; set; }
        public bool CopyToOutDir { get; set; }
        public string? Filter { get; set; }
        public string? GithubToken { get; set; }
        public string? Configuration { get; set; }

        private readonly Dictionary<string, List<ResolvedFileInfo>> _licenseContents = [];
        private readonly List<ResolvedFileInfo> _unresolvedFiles = [];

        public int Invoke(InvocationContext context)
        {
            return 0;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var cancellationToken = context.GetCancellationToken();
            MSBuildLocator.RegisterDefaults();
            ScanDir ??= Directory.GetCurrentDirectory();
            var projectFilePaths = projectService.GetProjectFilePaths(ScanDir);
            projectFilePaths = GetFilteredProjectPathes(projectFilePaths);
            if (projectFilePaths.Length == 0)
            {
                logger.LogError("No C# or F# project file found in the directory");
                return 0;
            }
            foreach (var projectFilePath in projectFilePaths)
                await ScanProjectAsync(projectFilePath, cancellationToken);
            if (!CopyToOutDir)
                await GenerateOutputFileAsync(OutputFilename, cancellationToken);
            return 0;
        }

        private string[] GetFilteredProjectPathes(string[] projectPathes)
        {
            if (string.IsNullOrEmpty(Filter))
                return projectPathes;
            var filterRegex = new Regex(Filter, RegexOptions.None, TimeSpan.FromMilliseconds(300));
            return projectPathes.Where(x => filterRegex.IsMatch(x)).ToArray();
        }

        private async Task ScanProjectAsync(string projectFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            logger.LogInformation("Resolving files for {ProjectName} using {configuration} configuration...", Path.GetFileName(projectFilePath), Configuration ?? "Release");
            var project = new Project(projectFilePath);
            project.SetProperty("Configuration", Configuration ?? "Release");
            project.SetProperty("DesignTimeBuild", "true");
            var resolvedFiles = projectService.ResolveFiles(project).ToList();
            logger.LogInformation("Resolved files count: {ResolvedFilesCount}", resolvedFiles.Count);
            foreach (var resolvedFileInfo in resolvedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation("Resolving license for {RelativeOutputPath}", resolvedFileInfo.RelativeOutputPath);
                if (resolvedFileInfo.NuSpec != null)
                {
                    logger.LogInformation("Package: {NuSpecId}", resolvedFileInfo.NuSpec.Id);
                }
                else
                {
                    logger.LogWarning("Package not found");
                }
                var resolverOptions = new ResolverOptions
                {
                    GitHubToken = GithubToken
                };
                var licenseContent = await licenseService.ResolveFromResolvedFileInfoAsync(resolvedFileInfo, resolverOptions, cancellationToken);
                if (licenseContent == null)
                {
                    _unresolvedFiles.Add(resolvedFileInfo);
                    logger.LogError("No license found for {RelativeOutputPath}. Source path: {SourcePath}. Verify this manually", resolvedFileInfo.RelativeOutputPath, resolvedFileInfo.SourcePath);
                    continue;
                }
                if (!_licenseContents.ContainsKey(licenseContent))
                    _licenseContents[licenseContent] = [];
                _licenseContents[licenseContent].Add(resolvedFileInfo);
            }
            stopWatch.Stop();
            logger.LogInformation("Project {ProjectName} resolved in {StopwatchElapsedMilliseconds}ms", Path.GetFileName(projectFilePath), stopWatch.ElapsedMilliseconds);
            if (CopyToOutDir && !string.IsNullOrEmpty(ScanDir) && !string.IsNullOrEmpty(OutputFilename))
                await GenerateOutputFileAsync(Path.Combine(ScanDir, project.GetPropertyValue("OutDir"), Path.GetFileName(OutputFilename)), cancellationToken);
        }

        private async Task GenerateOutputFileAsync(string? outputFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (outputFilePath == null)
                return;
            var uniqueResolvedFilesCount = _licenseContents.Values.Sum(v => v.GroupBy(x => x.SourcePath).Count());
            var uniqueUnresolvedFilesCount = _unresolvedFiles.GroupBy(x => x.SourcePath).Count();
            logger.LogInformation("Resolved {LicenseContentsCount} licenses for {Sum}/{ResolvedFilesCount} files", _licenseContents.Count, uniqueResolvedFilesCount, uniqueResolvedFilesCount + uniqueUnresolvedFilesCount);
            logger.LogInformation("Unresolved files: {UnresolvedFilesCount}", _unresolvedFiles.Count);
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var stringBuilder = new StringBuilder();
            foreach (var (licenseContent, resolvedFileInfos) in _licenseContents)
            {
                var longestNameLen = 0;
                foreach (var resolvedFileInfo in resolvedFileInfos.GroupBy(x => x.SourcePath).Select(x => x.First()))
                {
                    var strLen = resolvedFileInfo.RelativeOutputPath?.Length ?? 0;
                    if (strLen > longestNameLen)
                        longestNameLen = strLen;
                    stringBuilder.AppendLine(resolvedFileInfo.RelativeOutputPath);
                }
                stringBuilder.AppendLine(new string('-', longestNameLen));
                stringBuilder.AppendLine(licenseContent);
                stringBuilder.AppendLine();
            }
            stopWatch.Stop();
            logger.LogInformation("Generate licenses in {StopwatchElapsedMilliseconds}ms", stopWatch.ElapsedMilliseconds);
            if (stringBuilder.Length == 0)
                return;
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Writing to {OutputFilename}...", outputFilePath);
            await System.IO.File.WriteAllTextAsync(outputFilePath, stringBuilder.ToString());
            _licenseContents.Clear();
            _unresolvedFiles.Clear();
        }
    }
}
