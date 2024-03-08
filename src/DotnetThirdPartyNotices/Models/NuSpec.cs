namespace DotnetThirdPartyNotices.Models;

public record NuSpec
{
    public string Id { get; init; }
    public string Version { get; init; }
    public string LicenseUrl { get; init; }
    public string ProjectUrl { get; init; }
    public string RepositoryUrl { get; init; }
    public string LicenseRelativePath { get; init; }
}