using DotnetThirdPartyNotices.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services;

internal interface IFileVersionInfoLicenseResolver : ILicenseResolver
{
    Task<bool> CanResolveAsync(FileVersionInfo fileVersionInfo, CancellationToken cancellationToken);
    Task<string?> ResolveAsync(FileVersionInfo fileVersionInfo, ResolverOptions resolverOptions, CancellationToken cancellationToken);
}