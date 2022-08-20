using DotnetThirdPartyNotices.Extensions;
using DotnetThirdPartyNotices.Models;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var scanDirArgument = new Argument<string>("scan-dir", "Path of the directory to look for projects (optional)") {Arity = ArgumentArity.ZeroOrOne};
var outputFileOption = new Option<string>(
    "--output-filename",
    () => "third-party-notices.txt",
    "Output filename");
var copyToOutDirOption =
    new Option<bool>("--copy-to-outdir", () => false, "Copy to output directory in Release configuration");

var rootCommand = new RootCommand("A tool to generate file with third party legal notices for .NET projects")
{
    scanDirArgument,
    outputFileOption,
    copyToOutDirOption
};

rootCommand.SetHandler(async (scanDir, outputFilename, copyToOutDir) =>
{
    MSBuildLocator.RegisterDefaults();
    
    await Run(scanDir, outputFilename, copyToOutDir);
}, scanDirArgument, outputFileOption, copyToOutDirOption);

return await rootCommand.InvokeAsync(args);

static async Task Run(string scanDir, string outputFilename, bool copyToOutputDir)
{
    var scanDirectory = scanDir ?? Directory.GetCurrentDirectory();
    Log.Information("Scan directory {ScanDirectory}", scanDirectory);
    var projectFilePath = Directory.GetFiles(scanDirectory, "*.*", SearchOption.TopDirectoryOnly)
        .SingleOrDefault(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"));
    if (projectFilePath == null)
    {
        Log.Error("No C# or F# project file found in the current directory");
        return;
    }

    var project = new Project(projectFilePath);
    project.SetProperty("Configuration", "Release");
    project.SetProperty("DesignTimeBuild", "true");
    
    Log.Information("Resolving files...");
    
    var stopwatch = new Stopwatch();

    stopwatch.Start();

    var licenseContents = new Dictionary<string, List<ResolvedFileInfo>>();
    var resolvedFiles = project.ResolveFiles().ToList();

    Log.Information("Resolved files count: {ResolvedFilesCount}", resolvedFiles.Count);
    
    var unresolvedFiles = new List<ResolvedFileInfo>();

    foreach (var resolvedFileInfo in resolvedFiles)
    {
        Log.Information("Resolving license for {RelativeOutputPath}", resolvedFileInfo.RelativeOutputPath);
        if (resolvedFileInfo.NuSpec != null)
        {
            Log.Information("Package: {NuSpecId}", resolvedFileInfo.NuSpec.Id);
        }
        else
        {
            Log.Warning("Package not found");
        }

        var licenseContent = await resolvedFileInfo.ResolveLicense();
        if (licenseContent == null)
        {
            unresolvedFiles.Add(resolvedFileInfo);
            Log.Error("No license found for {RelativeOutputPath}. Source path: {SourcePath}. Verify this manually",
                resolvedFileInfo.RelativeOutputPath, resolvedFileInfo.SourcePath);
            continue;
        }

        if (!licenseContents.ContainsKey(licenseContent))
            licenseContents[licenseContent] = new List<ResolvedFileInfo>();

        licenseContents[licenseContent].Add(resolvedFileInfo);
    }

    stopwatch.Stop();

    Log.Information("Resolved {LicenseContentsCount} licenses for {Sum}/{ResolvedFilesCount} files in {StopwatchElapsedMilliseconds}ms", licenseContents.Count, licenseContents.Values.Sum(v => v.Count), resolvedFiles.Count, stopwatch.ElapsedMilliseconds);
    Log.Information("Unresolved files: {UnresolvedFilesCount}", unresolvedFiles.Count);

    stopwatch.Start();

    var stringBuilder = new StringBuilder();

    foreach (var (licenseContent, resolvedFileInfos) in licenseContents)
    {
        var longestNameLen = 0;
        foreach (var resolvedFileInfo in resolvedFileInfos)
        {
            var strLen = resolvedFileInfo.RelativeOutputPath.Length;
            if (strLen > longestNameLen)
                longestNameLen = strLen;

            stringBuilder.AppendLine(resolvedFileInfo.RelativeOutputPath);
        }

        stringBuilder.AppendLine(new string('-', longestNameLen));

        stringBuilder.AppendLine(licenseContent);
        stringBuilder.AppendLine();
    }

    stopwatch.Stop();

    if (stringBuilder.Length > 0)
    {
        if (copyToOutputDir)
        {
            outputFilename = Path.Combine(
                Path.Combine(scanDirectory, project.GetPropertyValue("OutDir")),
                Path.GetFileName(outputFilename));
        }
        
        Log.Information("Writing to {OutputFilename}...", outputFilename);
        await File.WriteAllTextAsync(outputFilename, stringBuilder.ToString());
     
        Log.Information("Done in {StopwatchElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
    }
}