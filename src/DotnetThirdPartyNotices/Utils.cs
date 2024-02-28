using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DotnetThirdPartyNotices;

internal static partial class Utils
{
    public static string GetNuspecPath( string assemblyPath )
    {
        var package = GetPackagePath( assemblyPath );
        return package != null
            ? Directory.EnumerateFiles( package, "*.nuspec", SearchOption.TopDirectoryOnly ).FirstOrDefault()
            : null;
    }

    public static string GetNupkgPath( string assemblyPath )
    {
        var package = GetPackagePath( assemblyPath );
        return package != null
            ? Directory.EnumerateFiles( package, "*.nupkg", SearchOption.TopDirectoryOnly ).FirstOrDefault()
            : null;
    }

    public static string GetPackagePath( string assemblyPath )
    {
        var directoryParts = Path.GetDirectoryName( assemblyPath ).Split( '\\', StringSplitOptions.RemoveEmptyEntries );
        // New structure: packages\{packageName}\{version}\lib\{targetFramework}\{packageName}.dll
        if (NewNugetVersionRegex().IsMatch( directoryParts.SkipLast( 2 ).Last() ))
            return string.Join( '\\', directoryParts.SkipLast( 3 ) );
        // Old structure: packages\{packageName}.{version}\lib\{targetFramework}\{packageName}.dll
        if (OldNugetVersionRegex().IsMatch( directoryParts.SkipLast( 2 ).Last() ))
            return string.Join( '\\', directoryParts.SkipLast( 2 ) );
        return null;
    }

    [GeneratedRegex( @"^\d+.\d+.\d+\S*$", RegexOptions.None )]
    private static partial Regex NewNugetVersionRegex();

    [GeneratedRegex( @"^(.*).(\d+.\d+.\d+\S*)$" )]
    private static partial Regex OldNugetVersionRegex();
}