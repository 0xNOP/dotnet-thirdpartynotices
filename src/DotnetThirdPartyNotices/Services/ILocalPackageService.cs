using DotnetThirdPartyNotices.Models;
using System.IO;

namespace DotnetThirdPartyNotices.Services
{
    public interface ILocalPackageService
    {
        string GetNupkgPathFromPackagePath(string packagePath);
        NuSpec GetNuSpecFromNupkgPath(string path);
        NuSpec GetNuSpecFromNuspecPath(string path);
        NuSpec GetNuSpecFromPackagePath(string path);
        NuSpec GetNuSpecFromStreamReader(TextReader textReader);
        string GetNuspecPathFromPackagePath(string packagePath);
        string GetPackagePathFromAssemblyPath(string assemblyPath);
    }
}