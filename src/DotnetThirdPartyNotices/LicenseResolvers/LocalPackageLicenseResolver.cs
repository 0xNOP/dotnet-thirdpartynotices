using DotnetThirdPartyNotices.LicenseResolvers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.LicenseResolvers;

internal class LocalPackageLicenseResolver : IFileVersionInfoLicenseResolver
{
    public bool CanResolve( FileVersionInfo fileVersionInfo ) => true;

    public async Task<string> Resolve( FileVersionInfo fileVersionInfo )
    {
        var packageName = Path.GetFileNameWithoutExtension(fileVersionInfo.FileName);
        var directoryParts = Path.GetDirectoryName(fileVersionInfo.FileName ).Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for ( var i = 0; i < directoryParts.Length; i++ )
        {
            var directoryPath = string.Join('\\', directoryParts.SkipLast(i));
            var licensePath = Directory.EnumerateFiles(directoryPath, "license.txt", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (licensePath != null)
                return await File.ReadAllTextAsync(licensePath);
            if (directoryPath.EndsWith($"\\{packageName}", StringComparison.OrdinalIgnoreCase))
                break;
        }
        return null;
    }
}
