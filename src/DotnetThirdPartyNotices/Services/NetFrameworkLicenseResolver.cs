using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services;

internal class NetFrameworkLicenseResolver : ILicenseUriLicenseResolver, IFileVersionInfoLicenseResolver
{
    private static string _licenseContent;

    public bool CanResolve(Uri licenseUri) => licenseUri.ToString().Contains("LinkId=529443");
    public bool CanResolve(FileVersionInfo fileVersionInfo) => fileVersionInfo.ProductName == "Microsoft® .NET Framework";

    public Task<string> Resolve(Uri licenseUri) => GetLicenseContent();
    public Task<string> Resolve(FileVersionInfo fileVersionInfo) => GetLicenseContent();

    public async Task<string> GetLicenseContent()
    {
        if (_licenseContent != null) // small optimization: avoid getting the resource on every call
            return _licenseContent;

        var executingAssembly = Assembly.GetExecutingAssembly();
        await using var stream = executingAssembly.GetManifestResourceStream(typeof(Utils), "dotnet_library_license.txt");
        using var streamReader = new StreamReader(stream);
        _licenseContent = await streamReader.ReadToEndAsync();
        return _licenseContent;
    }
}