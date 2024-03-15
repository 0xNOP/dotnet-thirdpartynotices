using System.Diagnostics;

namespace DotnetThirdPartyNotices.Models;

internal class ResolvedFileInfo(string sourcePath)
{
    public string SourcePath { get; } = sourcePath;
    public string? RelativeOutputPath { get; init; }
    public FileVersionInfo? VersionInfo { get; init; }
    public NuSpec? NuSpec { get; init; }
    public string? PackagePath { get; init; }
}