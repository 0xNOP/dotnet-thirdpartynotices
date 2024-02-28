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

    public Task<string> Resolve( FileVersionInfo fileVersionInfo )
    {
        return Task.FromResult( Resolve( fileVersionInfo.FileName ) );
    }

    private string Resolve( string assemblyPath )
    {
        var packagePath = Utils.GetPackagePath( assemblyPath );
        if (packagePath != null)
        {
            return Directory.EnumerateFiles( packagePath, "license.txt", new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            } ).FirstOrDefault();
        }
        return Directory.EnumerateFiles( Path.GetDirectoryName( assemblyPath ), "license.txt", new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        } ).FirstOrDefault();
    }
}
