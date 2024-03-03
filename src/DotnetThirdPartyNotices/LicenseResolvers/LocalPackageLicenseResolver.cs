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
    public bool CanResolve( FileVersionInfo fileVersionInfo ) => GetLicensePath(fileVersionInfo) != null;

    public async Task<string> Resolve(FileVersionInfo fileVersionInfo)
    {
        var licensePath = GetLicensePath(fileVersionInfo);
        if (licensePath == null)
            return null;
        return await File.ReadAllTextAsync( licensePath );
    }

    private string GetLicensePath( FileVersionInfo fileVersionInfo )
    {
        var directoryPath = Utils.GetPackagePath( fileVersionInfo.FileName ) ?? Path.GetDirectoryName( fileVersionInfo.FileName );
        return Directory.EnumerateFiles( directoryPath, "license.*", new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        } ).FirstOrDefault( x => x.EndsWith( "\\license.txt", StringComparison.OrdinalIgnoreCase ) || x.EndsWith( "\\license.md", StringComparison.OrdinalIgnoreCase ) );
    }
}
