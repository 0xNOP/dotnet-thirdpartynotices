using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DotnetThirdPartyNotices.LicenseResolvers.Interfaces;
using DotnetThirdPartyNotices.Models;

namespace DotnetThirdPartyNotices.Extensions;

internal static class ResolvedFileInfoExtensions
{
    // Create instances only once
    private static readonly Lazy<List<ILicenseResolver>> LicenseResolvers =
        new(() => GetInstancesFromExecutingAssembly<ILicenseResolver>().ToList());

    private static readonly Lazy<List<ILicenseUriLicenseResolver>> LicenseUriLicenseResolvers =
        new(() => LicenseResolvers.Value.OfType<ILicenseUriLicenseResolver>().ToList());

    private static readonly Lazy<List<IProjectUriLicenseResolver>> ProjectUriLicenseResolvers =
        new(() => LicenseResolvers.Value.OfType<IProjectUriLicenseResolver>().ToList());

    private static readonly Lazy<List<IRepositoryUriLicenseResolver>> RepositoryUriLicenseResolvers =
        new(() => LicenseResolvers.Value.OfType<IRepositoryUriLicenseResolver>().ToList());

    private static readonly Lazy<List<IFileVersionInfoLicenseResolver>> FileVersionInfoLicenseResolvers =
        new(() => LicenseResolvers.Value.OfType<IFileVersionInfoLicenseResolver>().ToList());

    private static IEnumerable<T> GetInstancesFromExecutingAssembly<T>() where T : class
    {
        return Assembly.GetExecutingAssembly().GetInstances<T>();
    }

    private static bool TryFindLicenseUriLicenseResolver(Uri licenseUri, out ILicenseUriLicenseResolver resolver)
    {
        resolver = LicenseUriLicenseResolvers.Value.FirstOrDefault(r => r.CanResolve(licenseUri));
        return resolver != null;
    }

    private static bool TryFindRepositoryUriLicenseResolver( Uri licenseUri, out IRepositoryUriLicenseResolver resolver )
    {
        resolver = RepositoryUriLicenseResolvers.Value.FirstOrDefault(r => r.CanResolve(licenseUri));
        return resolver != null;
    }

    private static bool TryFindProjectUriLicenseResolver(Uri projectUri, out IProjectUriLicenseResolver resolver)
    {
        resolver = ProjectUriLicenseResolvers.Value.FirstOrDefault(r => r.CanResolve(projectUri));
        return resolver != null;
    }

    private static bool TryFindFileVersionInfoLicenseResolver(
        FileVersionInfo fileVersionInfo, out IFileVersionInfoLicenseResolver resolver)
    {
        resolver = FileVersionInfoLicenseResolvers.Value.FirstOrDefault(r => r.CanResolve(fileVersionInfo));
        return resolver != null;
    }

    public static async Task<string> ResolveLicense(this ResolvedFileInfo resolvedFileInfo)
    {
        if (resolvedFileInfo == null) throw new ArgumentNullException(nameof(resolvedFileInfo));
        string license = null;
        if (resolvedFileInfo.NuSpec != null)
            license = await ResolveLicense(resolvedFileInfo.NuSpec);

        return license ?? await ResolveLicense(resolvedFileInfo.VersionInfo);
    }

    private static readonly Dictionary<string, string> LicenseCache = new();

    private static async Task<string> ResolveLicense(NuSpec nuSpec)
    {
        if (LicenseCache.ContainsKey(nuSpec.Id))
            return LicenseCache[nuSpec.Id];
        
        var licenseUrl = nuSpec.LicenseUrl;
        var repositoryUrl = nuSpec.RepositoryUrl;
        var projectUrl = nuSpec.ProjectUrl;

        // Try to get the license from license url
        if (!string.IsNullOrEmpty(licenseUrl))
        {
            if (LicenseCache.ContainsKey(licenseUrl))
                return LicenseCache[licenseUrl];

            var license = await ResolveLicenseFromLicenseUri(new Uri(nuSpec.LicenseUrl));
            if (license != null)
            {
                LicenseCache[licenseUrl] = license;
                LicenseCache[nuSpec.Id] = license;
                return license;
            }
        }

        // Try to get the license from repository url
        if (!string.IsNullOrEmpty(repositoryUrl))
        {
            if (LicenseCache.ContainsKey(repositoryUrl ))
                return LicenseCache[repositoryUrl];
            var license = await ResolveLicenseFromRepositoryUri(new Uri(repositoryUrl));
            if (license != null)
            {
                LicenseCache[repositoryUrl] = license;
                LicenseCache[nuSpec.Id] = license;
                return license;
            }
        }

        // Otherwise try to get the license from project url
        if (string.IsNullOrEmpty(projectUrl)) return null;

        if (LicenseCache.ContainsKey(projectUrl))
            return LicenseCache[projectUrl];

        var license2 = await ResolveLicenseFromProjectUri(new Uri(projectUrl));
        if (license2 == null) return null;

        LicenseCache[nuSpec.Id] = license2;
        LicenseCache[nuSpec.ProjectUrl] = license2;
        return license2;
    }

    private static async Task<string> ResolveLicense(FileVersionInfo fileVersionInfo)
    {
        if (LicenseCache.ContainsKey(fileVersionInfo.FileName))
            return LicenseCache[fileVersionInfo.FileName];
        var license = await ResolveLicenseFromFileVersionInfo(fileVersionInfo);
        if(license == null)
            return null;
        LicenseCache[fileVersionInfo.FileName] = license;
        return license;
    }

    private static async Task<string> ResolveLicenseFromLicenseUri(Uri licenseUri)
    {
        if (TryFindLicenseUriLicenseResolver(licenseUri, out var licenseUriLicenseResolver))
            return await licenseUriLicenseResolver.Resolve(licenseUri);

        // TODO: redirect uris should be checked at the very end to save us from redundant requests (when no resolver for anything can be found)
        var redirectUri = await licenseUri.GetRedirectUri();
        if (redirectUri != null)
            return await ResolveLicenseFromLicenseUri(redirectUri);

        // Finally, if no license uri can be found despite all the redirects, try to blindly get it
        return await licenseUri.GetPlainText();
    }

    private static async Task<string> ResolveLicenseFromRepositoryUri(Uri repositoryUri)
    {
        if (TryFindRepositoryUriLicenseResolver(repositoryUri, out var repositoryUriLicenseResolver))
            return await repositoryUriLicenseResolver.Resolve(repositoryUri);

        // TODO: redirect uris should be checked at the very end to save us from redundant requests (when no resolver for anything can be found)
        var redirectUri = await repositoryUri.GetRedirectUri();
        if (redirectUri != null)
            return await ResolveLicenseFromLicenseUri(redirectUri);

        // Finally, if no license uri can be found despite all the redirects, try to blindly get it
        return await repositoryUri.GetPlainText();
    }

    private static async Task<string> ResolveLicenseFromProjectUri(Uri projectUri)
    {
        if (TryFindProjectUriLicenseResolver(projectUri, out var projectUriLicenseResolver))
            return await projectUriLicenseResolver.Resolve(projectUri);

        // TODO: redirect uris should be checked at the very end to save us from redundant requests (when no resolver for anything can be found)
        var redirectUri = await projectUri.GetRedirectUri();
        if (redirectUri != null)
            return await ResolveLicenseFromProjectUri(redirectUri);

        return null;
    }

    private static async Task<string> ResolveLicenseFromFileVersionInfo(FileVersionInfo fileVersionInfo)
    {
        if (!TryFindFileVersionInfoLicenseResolver(fileVersionInfo, out var fileVersionInfoLicenseResolver))
            return null;

        return await fileVersionInfoLicenseResolver.Resolve(fileVersionInfo);
    }
}