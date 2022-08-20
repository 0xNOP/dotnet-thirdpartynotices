using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DotnetThirdPartyNotices.Models;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Serilog;
using ILogger = Microsoft.Build.Framework.ILogger;

namespace DotnetThirdPartyNotices.Extensions;

internal static class ProjectExtensions
{
    public static IEnumerable<ResolvedFileInfo> ResolveFiles(this Project project)
    {
        var targetFrameworksProperty = project.GetProperty("TargetFrameworks");
        if (targetFrameworksProperty != null)
        {
            var targetFrameworks = targetFrameworksProperty.EvaluatedValue.Split(';');
            project.SetProperty("TargetFramework", targetFrameworks[0]);
        }

        var projectInstance = project.CreateProjectInstance();
        var targetFrameworkIdentifier = projectInstance.GetPropertyValue("TargetFrameworkIdentifier");

        Log.Information("Target framework: {TargetFrameworkIdentifier}", targetFrameworkIdentifier);

        switch (targetFrameworkIdentifier)
        {
            case TargetFrameworkIdentifiers.NetCore:
            case TargetFrameworkIdentifiers.NetStandard:
                return ResolveFilesUsingComputeFilesToPublish(projectInstance);
            case TargetFrameworkIdentifiers.NetFramework:
                return ResolveFilesUsingResolveAssemblyReferences(projectInstance);
            default:
                throw new InvalidOperationException("Unsupported target framework.");
        }
    }

    private static IEnumerable<ResolvedFileInfo> ResolveFilesUsingResolveAssemblyReferences(ProjectInstance projectInstance)
    {
        var resolvedFileInfos = new List<ResolvedFileInfo>();
            
        projectInstance.Build("ResolveAssemblyReferences", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Minimal) });
            
        foreach (var item in projectInstance.GetItems("ReferenceCopyLocalPaths"))
        {
            var assemblyPath = item.EvaluatedInclude;
            var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);

            var resolvedFileInfo = new ResolvedFileInfo
            {
                VersionInfo = versionInfo,
                SourcePath = assemblyPath,
                RelativeOutputPath = Path.GetFileName(assemblyPath)
            };
                
            if (item.GetMetadataValue("ResolvedFrom") == "{HintPathFromItem}" && item.GetMetadataValue("HintPath").StartsWith("..\\packages"))
            {
                var packagePath = Utils.GetPackagePathFromAssemblyPath(assemblyPath);
                if (packagePath == null)
                    throw new ApplicationException($"Cannot find package path from assembly path ({assemblyPath})");
                    
                var nuPkgFileName = Directory.GetFiles(packagePath, "*.nupkg", SearchOption.TopDirectoryOnly).Single();

                var nuSpec = NuSpec.FromNupkg(nuPkgFileName);
                resolvedFileInfo.NuSpec = nuSpec;
                resolvedFileInfos.Add(resolvedFileInfo);
            }
            else
            {
                resolvedFileInfos.Add(resolvedFileInfo);
            }
        }

        return resolvedFileInfos;
    }

    private static IEnumerable<ResolvedFileInfo> ResolveFilesUsingComputeFilesToPublish(ProjectInstance projectInstance)
    {
        var resolvedFileInfos = new List<ResolvedFileInfo>();

        projectInstance.Build("ComputeFilesToPublish", new ILogger[] { new ConsoleLogger(LoggerVerbosity.Minimal) });

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

            var packagePath = Utils.GetPackagePathFromAssemblyPath(assemblyPath);
            if (packagePath == null)
                throw new ApplicationException($"Cannot find package path from assembly path ({assemblyPath})");

            // TODO: don't think this is reliable because I'm not sure if .nuspec will always be there, or if it will always be named tha way
            var nuSpecFilePath = Path.Combine(packagePath, $"{packageName}.nuspec"); // Directory.GetFiles(packageFolder, "*.nuspec", SearchOption.TopDirectoryOnly).SingleOrDefault();
            var nuSpec = NuSpec.FromFile(nuSpecFilePath);

            var relativePath = item.GetMetadataValue("RelativePath");
            var resolvedFileInfo = new ResolvedFileInfo
            {
                SourcePath = assemblyPath,
                VersionInfo = FileVersionInfo.GetVersionInfo(assemblyPath),
                NuSpec = nuSpec,
                RelativeOutputPath = relativePath
            };

            resolvedFileInfos.Add(resolvedFileInfo);
        }

        return resolvedFileInfos;
    }
}