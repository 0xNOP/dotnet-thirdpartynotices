using System;
using System.Threading.Tasks;
using DotnetThirdPartyNotices.LicenseResolvers.Interfaces;

namespace DotnetThirdPartyNotices.LicenseResolvers;

internal class OpenSourceOrgLicenseResolver : ILicenseUriLicenseResolver
{
    public bool CanResolve(Uri licenseUri) => licenseUri.Host == "opensource.org";

    public async Task<string> Resolve(Uri licenseUri)
    {
        var s = licenseUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (s[0] != "licenses") return null;

        var licenseId = s[1];
        using var githubService = new GithubService();
        return await githubService.GetLicenseContentFromId(licenseId);
    }
}