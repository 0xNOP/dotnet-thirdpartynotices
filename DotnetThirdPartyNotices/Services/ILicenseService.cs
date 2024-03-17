using DotnetThirdPartyNotices.Models;

namespace DotnetThirdPartyNotices.Services;

internal interface ILicenseService
{
    Task<string?> ResolveFromResolvedFileInfoAsync(ResolvedFileInfo resolvedFileInfo, ResolverOptions resolverOptions, CancellationToken cancellationToken);
}