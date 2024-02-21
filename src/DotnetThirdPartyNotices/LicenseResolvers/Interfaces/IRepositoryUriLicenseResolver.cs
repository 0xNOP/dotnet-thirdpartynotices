using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.LicenseResolvers.Interfaces;

internal interface IRepositoryUriLicenseResolver : ILicenseResolver
{
    bool CanResolve(Uri projectUri);
    Task<string> Resolve(Uri projectUri);
}