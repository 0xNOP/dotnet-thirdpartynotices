namespace DotnetThirdPartyNotices.Models;

public record NuSpec
{
    public NuSpec(string id)
    {
        Id = id;
    }

    public string Id { get; }
    public string? Version { get; init; }
    public string? LicenseUrl { get; init; }
    public string? ProjectUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? LicenseRelativePath { get; init; }
}