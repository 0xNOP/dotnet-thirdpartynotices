using DotnetThirdPartyNotices.Models;

namespace DotnetThirdPartyNotices.Services;

internal interface IUriLicenseResolver
{
    Task<bool> CanResolveAsync(Uri licenseUri, ResolverOptions resolverOptions, CancellationToken cancellationToken);
    Task<string?> ResolveAsync(Uri licenseUri, ResolverOptions resolverOptions, CancellationToken cancellationToken);
}
