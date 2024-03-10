using DotnetThirdPartyNotices.Models;

namespace DotnetThirdPartyNotices.Services;

internal interface ILicenseService
{
    Task<string?> ResolveFromResolvedFileInfo(ResolvedFileInfo resolvedFileInfo);
}