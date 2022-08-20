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
            ProjectUrl = metadata.Element(ns + "projectUrl")?.Value
        };
    }

    public static NuSpec FromFile(string fileName)
    {
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        using var xmlReader = new StreamReader(fileName);
        return FromTextReader(xmlReader);
    }

    public static NuSpec FromNupkg(string fileName)
    {
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        using var zipToCreate = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        using var zip = new ZipArchive(zipToCreate, ZipArchiveMode.Read);
        var zippedNuspec = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec"));
        using var stream = zippedNuspec.Open();
        using var streamReader = new StreamReader(stream);
        return FromTextReader(streamReader);
    }
}