using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace DotnetThirdPartyNotices;

public record NuSpec
{
    public string Id { get; init; }
    public string Version { get; init; }
    public string LicenseUrl { get; init; }
    public string ProjectUrl { get; init; }
    public string RepositoryUrl { get; init; }
    public string LicenseRelativePath { get; init; }

    private static NuSpec FromTextReader(TextReader streamReader)
    {
        using var xmlReader = XmlReader.Create(streamReader);
        var xDocument = XDocument.Load(xmlReader);
        if (xDocument.Root == null) return null;
        var ns = xDocument.Root.GetDefaultNamespace();

        var metadata = xDocument.Root.Element(ns + "metadata");
        if (metadata == null) return null;

        return new NuSpec
        {
            Id = metadata.Element(ns + "id")?.Value,
            Version = metadata.Element(ns + "version")?.Value,
            LicenseUrl = metadata.Element(ns + "licenseUrl")?.Value,
            ProjectUrl = metadata.Element(ns + "projectUrl")?.Value,
            RepositoryUrl = metadata.Element(ns + "repository")?.Attribute("url")?.Value,
            LicenseRelativePath = metadata.Elements(ns + "license").Where(x => x.Attribute("type")?.Value == "file").FirstOrDefault()?.Value
        };
    }

    public static NuSpec FromFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull( fileName );
        using var xmlReader = new StreamReader(fileName);
        return FromTextReader(xmlReader);
    }

    public static NuSpec FromNupkg(string fileName)
    {
        ArgumentNullException.ThrowIfNull( fileName );
        using var zipToCreate = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        using var zip = new ZipArchive(zipToCreate, ZipArchiveMode.Read);
        var zippedNuspec = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec"));
        using var stream = zippedNuspec.Open();
        using var streamReader = new StreamReader(stream);
        return FromTextReader(streamReader);
    }

    public static NuSpec FromAssemble(string assemblePath)
    {
        if (assemblePath == null) throw new ArgumentNullException(nameof(assemblePath));
        var nuspec = Utils.GetNuspecPath(assemblePath);
        if (nuspec != null)
            return FromFile( nuspec );
        var nupkg = Utils.GetNupkgPath(assemblePath);
        if(nupkg != null)
            return FromNupkg(nupkg);
        return null;
    }
}