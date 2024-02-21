using System;
using System.Threading.Tasks;
using DotnetThirdPartyNotices.Extensions;
using DotnetThirdPartyNotices.LicenseResolvers.Interfaces;

namespace DotnetThirdPartyNotices.LicenseResolvers;

internal class GithubLicenseResolver : ILicenseUriLicenseResolver, IProjectUriLicenseResolver, IRepositoryUriLicenseResolver
{
    bool ILicenseUriLicenseResolver.CanResolve(Uri uri) => uri.IsGithubUri();
    bool IProjectUriLicenseResolver.CanResolve(Uri uri) => uri.IsGithubUri();
    bool IRepositoryUriLicenseResolver.CanResolve(Uri uri) => uri.IsGithubUri();

    Task<string> ILicenseUriLicenseResolver.Resolve(Uri licenseUri)
    {
        licenseUri = licenseUri.ToRawGithubUserContentUri();

        return licenseUri.GetPlainText();
    }

    async Task<string> IProjectUriLicenseResolver.Resolve(Uri projectUri)
    {
        using var githubService = new GithubService();
        return await githubService.GetLicenseContentFromRepositoryPath(projectUri.AbsolutePath);
    }

    async Task<string> IRepositoryUriLicenseResolver.Resolve(Uri projectUri)
    {
        using var githubService = new GithubService();
        return await githubService.GetLicenseContentFromRepositoryPath(projectUri.AbsolutePath);
    }
}