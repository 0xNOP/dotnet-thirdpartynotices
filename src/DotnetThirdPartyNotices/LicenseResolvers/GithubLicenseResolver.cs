﻿using System;
using System.Threading.Tasks;
using DotnetThirdPartyNotices.Extensions;

namespace DotnetThirdPartyNotices.LicenseResolvers
{
    internal class GithubLicenseResolver : ILicenseUriLicenseResolver, IProjectUriLicenseResolver
    {
        bool ILicenseUriLicenseResolver.CanResolve(Uri uri) => uri.IsGithubUri();
        bool IProjectUriLicenseResolver.CanResolve(Uri uri) => uri.IsGithubUri();

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
    }
}
