using System.IO;

namespace DotnetThirdPartyNotices;

internal static class Utils
{
    public static string GetPackagePathFromAssemblyPath(string assemblyPath)
    {
        var parentDirectoryInfo = Directory.GetParent(assemblyPath);
        var isValid = false;
        while (parentDirectoryInfo != null && !(isValid = NuGetVersion.IsValid(parentDirectoryInfo.Name)))
        {
            parentDirectoryInfo = parentDirectoryInfo.Parent;
        }

        return isValid ? parentDirectoryInfo.FullName : null;
    }
}