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
        // packages\{packageName}\{version}\lib\{targetFramework}\{packageName}.dll
        // packages\{packageName}\{version}\runtimes\{runtime-identifier}\lib\{targetFramework}\{packageName}.dll
        // packages\{packageName}\{version}\lib\{targetFramework}\{culture}\{packageName}.dll
        var index = Array.FindLastIndex( directoryParts, x => NewNugetVersionRegex().IsMatch(x));
        if (index > -1)
            return string.Join('\\', directoryParts.Take(index + 1) );
        // packages\{packageName}.{version}\lib\{targetFramework}\{packageName}.dll
        index = Array.FindLastIndex(directoryParts, x => OldNugetVersionRegex().IsMatch(x));
        if (index > -1)
            return string.Join('\\', directoryParts.Take(index + 1));
        return null;
    }

    [GeneratedRegex( @"^\d+.\d+.\d+\S*$", RegexOptions.None )]
    private static partial Regex NewNugetVersionRegex();

    [GeneratedRegex( @"^\S+\.\d+.\d+.\d+\S*$" )]
    private static partial Regex OldNugetVersionRegex();
}