using DotnetThirdPartyNotices.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DotnetThirdPartyNotices.Services;

public partial class LocalPackageService : ILocalPackageService
{
    public NuSpec? GetNuSpecFromPackagePath(string? path)
    {
        if (path == null)
            return null;
        var nuspecPath = GetNuspecPathFromPackagePath(path);
        if (nuspecPath != null)
            return GetNuSpecFromNuspecPath(nuspecPath);
        var nupkgPath = GetNupkgPathFromPackagePath(path);
        if (nupkgPath != null)
            return GetNuSpecFromNupkgPath(nupkgPath);
        return null;
    }
    public NuSpec? GetNuSpecFromNupkgPath(string? path)
    {
        if (path == null)
            return null;
        using var zipToCreate = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var zip = new ZipArchive(zipToCreate, ZipArchiveMode.Read);
        var zippedNuspec = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec"));
        using var stream = zippedNuspec.Open();
        using var streamReader = new StreamReader(stream);
        return GetNuSpecFromStreamReader(streamReader);
    }

    public NuSpec? GetNuSpecFromNuspecPath(string? path)
    {
        if (path == null)
            return null;
        using var streamReader = new StreamReader(path);
        return GetNuSpecFromStreamReader(streamReader);
    }

    public NuSpec? GetNuSpecFromStreamReader(TextReader? textReader)
    {
        if (textReader == null)
            return null;
        var xDocument = XDocument.Load(textReader);
        if (xDocument.Root == null) return null;
        var ns = xDocument.Root.GetDefaultNamespace();
        var metadata = xDocument.Root.Element(ns + "metadata");
        if (metadata == null)
            return null;
        var id = metadata.Element(ns + "id")?.Value;
        if (id == null)
            return null;
        return new NuSpec(id)
        {
            Version = metadata.Element(ns + "version")?.Value,
            LicenseUrl = metadata.Element(ns + "licenseUrl")?.Value,
            ProjectUrl = metadata.Element(ns + "projectUrl")?.Value,
            RepositoryUrl = metadata.Element(ns + "repository")?.Attribute("url")?.Value,
            LicenseRelativePath = metadata.Elements(ns + "license").Where(x => x.Attribute("type")?.Value == "file").FirstOrDefault()?.Value
        };
    }


    public string? GetNuspecPathFromPackagePath(string? packagePath)
    {
        return packagePath != null
            ? Directory.EnumerateFiles(packagePath, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault()
            : null;
    }

    public string? GetNupkgPathFromPackagePath(string? packagePath)
    {
        return packagePath != null
            ? Directory.EnumerateFiles(packagePath, "*.nupkg", SearchOption.TopDirectoryOnly).FirstOrDefault()
            : null;
    }

    public string? GetPackagePathFromAssemblyPath(string? assemblyPath)
    {
        if(assemblyPath == null)
            return null;
        var directoryParts = Path.GetDirectoryName(assemblyPath)?.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if(directoryParts == null)
            return null;
        // packages\{packageName}\{version}\lib\{targetFramework}\{packageName}.dll
        // packages\{packageName}\{version}\runtimes\{runtime-identifier}\lib\{targetFramework}\{packageName}.dll
        // packages\{packageName}\{version}\lib\{targetFramework}\{culture}\{packageName}.dll
        var index = Array.FindLastIndex(directoryParts, x => NewNugetVersionRegex().IsMatch(x));
        if (index > -1)
            return string.Join('\\', directoryParts.Take(index + 1));
        // packages\{packageName}.{version}\lib\{targetFramework}\{packageName}.dll
        index = Array.FindLastIndex(directoryParts, x => OldNugetVersionRegex().IsMatch(x));
        if (index > -1)
            return string.Join('\\', directoryParts.Take(index + 1));
        return null;
    }

    [GeneratedRegex(@"^\d+.\d+.\d+\S*$", RegexOptions.None, 300)]
    private static partial Regex NewNugetVersionRegex();

    [GeneratedRegex(@"^\S+\.\d+.\d+.\d+\S*$", RegexOptions.None, 300)]
    private static partial Regex OldNugetVersionRegex();
}