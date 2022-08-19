using DotnetThirdPartyNotices.Extensions;
using DotnetThirdPartyNotices.Models;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

var scanDirArgument = new Argument<string>("scan-dir", "Path of the directory to look for projects (optional)") {Arity = ArgumentArity.ZeroOrOne};
var outputFileOption = new Option<string>(
    "--output-filename",
    () => "third-party-notices.txt",
    "Output filename");
var copyToOutdirOption =
    new Option<bool>("--copy-to-outdir", () => false, "Copy to output directory in Release configuration");

var rootCommand = new RootCommand("A tool to generate file with third party legal notices for .NET projects")
{
    scanDirArgument,
    outputFileOption,
    copyToOutdirOption
};

rootCommand.SetHandler(async (scanDir, outputFilename, copyToOutDir) =>
{
    MSBuildLocator.RegisterDefaults();
    
    await Run(scanDir, outputFilename, copyToOutDir);
}, scanDirArgument, outputFileOption, copyToOutdirOption);

return await rootCommand.InvokeAsync(args);

static async Task Run(string scanDir, string outputFilename, bool copyToOutputDir)
{
    var scanDirectory = scanDir ?? Directory.GetCurrentDirectory();
    Console.WriteLine(scanDirectory);
    var projectFilePath = Directory.GetFiles(scanDirectory, "*.*", SearchOption.TopDirectoryOnly)
        .SingleOrDefault(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"));
    if (projectFilePath == null)
    {
        Console.WriteLine("No C# or F# project file found in the current directory.");
        return;
    }

    var project = new Project(projectFilePath);
    project.SetProperty("Configuration", "Release");
    project.SetProperty("DesignTimeBuild", "true");
    
    Console.WriteLine("Resolving files...");
    
    var stopwatch = new Stopwatch();

    stopwatch.Start();

    var licenseContents = new Dictionary<string, List<ResolvedFileInfo>>();
    var resolvedFiles = project.ResolveFiles().ToList();

    Console.WriteLine($"Resolved files count: {resolvedFiles.Count}");
    
    var unresolvedFiles = new List<ResolvedFileInfo>();

    foreach (var resolvedFileInfo in resolvedFiles)
    {
        Console.WriteLine($"Resolving license for {resolvedFileInfo.RelativeOutputPath}");
        Console.WriteLine(resolvedFileInfo.NuSpec != null
            ? $"  Package: {resolvedFileInfo.NuSpec.Id}"
            : " NOT FOUND");

        var licenseContent = await resolvedFileInfo.ResolveLicense();
        if (licenseContent == null)
        {
            unresolvedFiles.Add(resolvedFileInfo);
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(
                $"No license found for {resolvedFileInfo.RelativeOutputPath}. Source path: {resolvedFileInfo.SourcePath}. Verify this manually.");
            Console.ResetColor();
            continue;
        }

        if (!licenseContents.ContainsKey(licenseContent))
            licenseContents[licenseContent] = new List<ResolvedFileInfo>();

        licenseContents[licenseContent].Add(resolvedFileInfo);
    }

    stopwatch.Stop();

    Console.WriteLine($"Resolved {licenseContents.Count} licenses for {licenseContents.Values.Sum(v => v.Count)}/{resolvedFiles.Count} files in {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"Unresolved files: {unresolvedFiles.Count}");

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
        
        Console.WriteLine($"Writing to {outputFilename}...");
        await File.WriteAllTextAsync(outputFilename, stringBuilder.ToString());
     
        Console.WriteLine($"Done in {stopwatch.ElapsedMilliseconds}ms");
    }
}