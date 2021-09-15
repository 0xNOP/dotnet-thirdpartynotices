﻿using DotnetThirdPartyNotices.Extensions;
using DotnetThirdPartyNotices.Models;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

var rootCommand = new RootCommand
{
    new Argument<string>("argument", "Path of the directory to look for projects (optional)"),
    new Option<string>(
        "--output-filename",
        () => "third-party-notices.txt",
        "Output filename")
};

rootCommand.Handler = CommandHandler.Create<string, string>(async (argument, outputFilename) =>
{
    MSBuildLocator.RegisterDefaults();

    await Run(outputFilename, argument);
});

return await rootCommand.InvokeAsync(args);

static async Task Run(string outputFilename, string argument)
{
    var scanDirectory = argument ?? Directory.GetCurrentDirectory();
    Console.WriteLine(scanDirectory);
    var projectFilePath = Directory.GetFiles(scanDirectory, "*.*", SearchOption.TopDirectoryOnly)
        .SingleOrDefault(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"));
    if (projectFilePath == null)
    {
        Console.WriteLine("No C# or F# project file found in the current directory.");
        return;
    }

    var project = new Project(projectFilePath);
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
        Console.WriteLine($"Writing to {outputFilename}...");
        await File.WriteAllTextAsync(outputFilename, stringBuilder.ToString());

        Console.WriteLine($"Done in {stopwatch.ElapsedMilliseconds}ms");
    }
}