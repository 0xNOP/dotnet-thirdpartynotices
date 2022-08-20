using System.Diagnostics;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.LicenseResolvers.Interfaces;

internal interface IFileVersionInfoLicenseResolver : ILicenseResolver
{
    bool CanResolve(FileVersionInfo fileVersionInfo);
    Task<string> Resolve(FileVersionInfo fileVersionInfo);
}