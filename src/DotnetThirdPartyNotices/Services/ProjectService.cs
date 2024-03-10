using DotnetThirdPartyNotices.Models;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DotnetThirdPartyNotices.Services;

internal class ProjectService(ILogger<ProjectService> logger, ILocalPackageService localPackageService) : IProjectService
{
    public IEnumerable<ResolvedFileInfo> ResolveFiles(Project project)
    {
        var targetFrameworksProperty = project.GetProperty("TargetFrameworks");
        if (targetFrameworksProperty != null)
        {
            var targetFrameworks = targetFrameworksProperty.EvaluatedValue.Split(';');
            project.SetProperty("TargetFramework", targetFrameworks[0]);
        }
        var projectInstance = project.CreateProjectInstance();
        var targetFrameworkIdentifier = projectInstance.GetPropertyValue("TargetFrameworkIdentifier");
        return targetFrameworkIdentifier switch
        {
            TargetFrameworkIdentifiers.NetCore or TargetFrameworkIdentifiers.NetStandard => ResolveFilesUsingComputeFilesToPublish(projectInstance),
            TargetFrameworkIdentifiers.NetFramework => ResolveFilesUsingResolveAssemblyReferences(projectInstance),
            _ => throw new InvalidOperationException("Unsupported target framework."),
        };
    }

    private IEnumerable<ResolvedFileInfo> ResolveFilesUsingResolveAssemblyReferences(ProjectInstance projectInstance)
    {
        var resolvedFileInfos = new List<ResolvedFileInfo>();

        if (!projectInstance.Build("ResolveAssemblyReferences", [new ConsoleLogger(LoggerVerbosity.Minimal)]))
            return [];

        foreach (var item in projectInstance.GetItems("ReferenceCopyLocalPaths"))
        {
            var assemblyPath = item.EvaluatedInclude;
            var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);
            var packagePath = localPackageService.GetPackagePathFromAssemblyPath(assemblyPath);
            var resolvedFileInfo = new ResolvedFileInfo
            {
                SourcePath = assemblyPath,
                VersionInfo = versionInfo,
                RelativeOutputPath = Path.GetFileName(assemblyPath),
                PackagePath = packagePath,
                NuSpec = item.GetMetadataValue("ResolvedFrom") == "{HintPathFromItem}" && item.GetMetadataValue("HintPath").StartsWith("..\\packages")
                    ? (localPackageService.GetNuSpecFromPackagePath(packagePath) ?? throw new ApplicationException($"Cannot find package path from assembly path ({assemblyPath})"))
                    : null
            };
            resolvedFileInfos.Add(resolvedFileInfo);
        }

        return resolvedFileInfos;
    }

    private IEnumerable<ResolvedFileInfo> ResolveFilesUsingComputeFilesToPublish(ProjectInstance projectInstance)
    {
        var resolvedFileInfos = new List<ResolvedFileInfo>();

        projectInstance.Build("ComputeFilesToPublish", [new ConsoleLogger(LoggerVerbosity.Minimal)]);

        foreach (var item in projectInstance.GetItems("ResolvedFileToPublish"))
        {
            var assemblyPath = item.EvaluatedInclude;

            var packageName = item.GetMetadataValue(item.HasMetadata("PackageName") ? "PackageName" : "NugetPackageId");
            var packageVersion = item.GetMetadataValue(item.HasMetadata("PackageName") ? "PackageVersion" : "NugetPackageVersion");
            if (packageName == string.Empty || packageVersion == string.Empty)
            {
                // Skip if it's not a NuGet package
                continue;
            }
            var relativePath = item.GetMetadataValue("RelativePath");
            var packagePath = localPackageService.GetPackagePathFromAssemblyPath(assemblyPath);
            var nuSpec = localPackageService.GetNuSpecFromPackagePath(packagePath) ?? throw new ApplicationException($"Cannot find package path from assembly path ({assemblyPath})");
            var resolvedFileInfo = new ResolvedFileInfo
            {
                SourcePath = assemblyPath,
                VersionInfo = FileVersionInfo.GetVersionInfo(assemblyPath),
                NuSpec = nuSpec,
                RelativeOutputPath = relativePath,
                PackagePath = packagePath
            };

            resolvedFileInfos.Add(resolvedFileInfo);
        }

        return resolvedFileInfos;
    }

    public string[] GetProjectFilePaths(string directoryPath)
    {
        logger.LogInformation("Scan directory {ScanDirectory}", directoryPath);
        var paths = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"))
            .ToArray();
        if (paths.Length > 0)
            return paths;
        var solutionFilePath = Directory.EnumerateFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly)
            .SingleOrDefault();
        if (solutionFilePath == null)
            return paths;
        return SolutionFile.Parse(solutionFilePath).ProjectsInOrder
            .Select(x => x.AbsolutePath)
            .Where(x => x.EndsWith(".csproj") || x.EndsWith(".fsproj"))
            .ToArray();
    }
}
