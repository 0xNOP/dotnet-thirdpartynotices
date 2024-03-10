using System.Diagnostics;

namespace DotnetThirdPartyNotices.Models;

internal class ResolvedFileInfo
{
    public string? SourcePath { get; init; }
    public string? RelativeOutputPath { get; init; }
    public FileVersionInfo? VersionInfo { get; init; }
    public NuSpec? NuSpec { get; init; }
    public string? PackagePath { get; init; }
}