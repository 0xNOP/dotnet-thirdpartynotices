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

        return license ?? await ResolveLicenseFromFileVersionInfo(resolvedFileInfo.VersionInfo);
    }

    private static readonly Dictionary<Uri, string> LicenseCache = new Dictionary<Uri, string>();

    private static async Task<string> ResolveLicense(NuSpec nuSpec)
    {
        // Try to get the license from license url
        if (!string.IsNullOrEmpty(nuSpec.LicenseUrl))
        {
            var licenseUri = new Uri(nuSpec.LicenseUrl);
            if (LicenseCache.ContainsKey(licenseUri))
                return LicenseCache[licenseUri];

            var license = await ResolveLicenseFromLicenseUri(licenseUri);
            if (license != null)
            {
                LicenseCache[licenseUri] = license;
                return license;
            }
        }

        // Otherwise try to get the license from project url
        if (string.IsNullOrEmpty(nuSpec.ProjectUrl)) return null;

        var projectUri = new Uri(nuSpec.ProjectUrl);
        if (LicenseCache.ContainsKey(projectUri))
            return LicenseCache[projectUri];

        var license2 = await ResolveLicenseFromProjectUri(projectUri);
        if (license2 == null) return null;

        LicenseCache[projectUri] = license2;
        return license2;
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