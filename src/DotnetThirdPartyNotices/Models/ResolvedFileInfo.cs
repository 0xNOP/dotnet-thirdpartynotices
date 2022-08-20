using System.Diagnostics;

namespace DotnetThirdPartyNotices.Models;

internal class ResolvedFileInfo
{
    public string SourcePath { get; set; }
    public string RelativeOutputPath { get; set; }
    public FileVersionInfo VersionInfo { get; set; }
    public NuSpec NuSpec { get; set; }
}