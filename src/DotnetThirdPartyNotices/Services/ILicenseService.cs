using DotnetThirdPartyNotices.Models;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services
{
    internal interface ILicenseService
    {
        Task<string> ResolveFromResolvedFileInfo(ResolvedFileInfo resolvedFileInfo);
    }
}