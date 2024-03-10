using DotnetThirdPartyNotices.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services;

internal class LicenseService(IEnumerable<ILicenseUriLicenseResolver> licenseUriLicenseResolvers, IEnumerable<IProjectUriLicenseResolver> projectUriLicenseResolvers, IEnumerable<IRepositoryUriLicenseResolver> repositoryUriLicenseResolvers, IEnumerable<IFileVersionInfoLicenseResolver> fileVersionInfoLicenseResolvers, IHttpClientFactory httpClientFactory) : ILicenseService
{
    private static readonly Dictionary<string, string> LicenseCache = [];

    public async Task<string?> ResolveFromResolvedFileInfo(ResolvedFileInfo resolvedFileInfo)
    {
        ArgumentNullException.ThrowIfNull(resolvedFileInfo);
        if (resolvedFileInfo.NuSpec != null && LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.Id, out string? value))
            return value;
        return (await ResolveFromLicenseRelativePathAsync(resolvedFileInfo))
            ?? (await ResolveFromLicenseUrlAsync(resolvedFileInfo, false))
            ?? (await ResolveFromRepositoryUrlAsync(resolvedFileInfo, false))
            ?? (await ResolveFromProjectUrlAsync(resolvedFileInfo, false))
            ?? (await ResolveFromPackagePathAsync(resolvedFileInfo))
            ?? (await ResolveFromAssemblyPathAsync(resolvedFileInfo))
            ?? (await ResolveFromFileVersionInfoAsync(resolvedFileInfo))
            ?? (await ResolveFromLicenseUrlAsync(resolvedFileInfo, true))
            ?? (await ResolveFromRepositoryUrlAsync(resolvedFileInfo, true))
            ?? (await ResolveFromProjectUrlAsync(resolvedFileInfo, true));
    }

    private async Task<string?> ResolveFromPackagePathAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.PackagePath))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.PackagePath, out string? value))
            return value;
        var licensePath = Directory.EnumerateFiles(resolvedFileInfo.PackagePath, "license.*", new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        }).FirstOrDefault(x => x.EndsWith("\\license.txt", StringComparison.OrdinalIgnoreCase) || x.EndsWith("\\license.md", StringComparison.OrdinalIgnoreCase));
        if (licensePath == null)
            return null;
        var license = await File.ReadAllTextAsync(licensePath);
        if (resolvedFileInfo.NuSpec != null)
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
        LicenseCache[resolvedFileInfo.PackagePath] = license;
        return license;
    }

    private async Task<string?> ResolveFromAssemblyPathAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.SourcePath))
            return null;
        var assemblyPath = Path.GetDirectoryName(resolvedFileInfo.SourcePath);
        if(assemblyPath == null)
            return null;
        if (LicenseCache.TryGetValue(assemblyPath, out string? value))
            return value;
        var licensePath = Directory.EnumerateFiles(assemblyPath, "license.*", new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        }).FirstOrDefault(x => x.EndsWith("\\license.txt", StringComparison.OrdinalIgnoreCase) || x.EndsWith("\\license.md", StringComparison.OrdinalIgnoreCase));
        if (licensePath == null)
            return null;
        var license = await File.ReadAllTextAsync(licensePath);
        LicenseCache[assemblyPath] = license;
        return license;
    }

    private async Task<string?> ResolveFromFileVersionInfoAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if(resolvedFileInfo.VersionInfo == null)
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.VersionInfo.FileName, out string? value))
            return value;
        foreach (var resolver in fileVersionInfoLicenseResolvers)
        {
            if (!resolver.CanResolve(resolvedFileInfo.VersionInfo))
                continue;
            var license = await resolver.Resolve(resolvedFileInfo.VersionInfo);
            if (license != null)
            {
                LicenseCache[resolvedFileInfo.VersionInfo.FileName] = license;
                return license;
            }
        }
        return null;
    }

    private async Task<string?> ResolveFromLicenseRelativePathAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.PackagePath) || string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.LicenseRelativePath))
            return null;
        var licenseFullPath = Path.Combine(resolvedFileInfo.PackagePath, resolvedFileInfo.NuSpec.LicenseRelativePath);
        if (LicenseCache.TryGetValue(licenseFullPath, out string? value))
            return value;
        if (!licenseFullPath.EndsWith(".txt") && !licenseFullPath.EndsWith(".md") || !File.Exists(licenseFullPath))
            return null;
        var license = await File.ReadAllTextAsync(licenseFullPath);
        if (string.IsNullOrEmpty(license))
            return null;
        LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
        LicenseCache[licenseFullPath] = license;
        return license;
    }

    private async Task<string?> ResolveFromProjectUrlAsync(ResolvedFileInfo resolvedFileInfo, bool useFinalUrl)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.ProjectUrl))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.ProjectUrl, out string? value))
            return value;
        if (!Uri.TryCreate(resolvedFileInfo.NuSpec.ProjectUrl, UriKind.Absolute, out var uri))
            return null;
        var license = useFinalUrl
            ? await ResolveFromFinalUrlAsync(uri, projectUriLicenseResolvers)
            : await ResolveFromUrlAsync(uri, projectUriLicenseResolvers);
        if (license != null)
        {
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[resolvedFileInfo.NuSpec.ProjectUrl] = license;
        }
        return license;
    }

    private async Task<string?> ResolveFromUrlAsync(Uri uri, IEnumerable<IUriLicenseResolver> urlLicenseResolvers)
    {
        foreach (var resolver in urlLicenseResolvers)
        {
            if (!resolver.CanResolve(uri))
                continue;
            var license = await resolver.Resolve(uri);
            if (license != null)
                return license;
        }
        return null;
    }

    private async Task<string?> ResolveFromFinalUrlAsync(Uri uri, IEnumerable<IUriLicenseResolver> urlLicenseResolvers)
    {
        using var httpClient = httpClientFactory.CreateClient();
        var httpResponseMessage = await httpClient.GetAsync(uri);
        if (!httpResponseMessage.IsSuccessStatusCode && uri.AbsolutePath.EndsWith(".txt"))
        {
            // try without .txt extension
            var fixedUri = new UriBuilder(uri);
            fixedUri.Path = fixedUri.Path.Remove(fixedUri.Path.Length - 4);
            httpResponseMessage = await httpClient.GetAsync(fixedUri.Uri);
            if (!httpResponseMessage.IsSuccessStatusCode)
                return null;
        }
        if (httpResponseMessage.RequestMessage != null && httpResponseMessage.RequestMessage.RequestUri != null && httpResponseMessage.RequestMessage.RequestUri != uri)
        {
            foreach (var resolver in urlLicenseResolvers)
            {
                if (!resolver.CanResolve(httpResponseMessage.RequestMessage.RequestUri))
                    continue;
                var license = await resolver.Resolve(httpResponseMessage.RequestMessage.RequestUri);
                if (license != null)
                    return license;
            }
        }
        // Finally, if no license uri can be found despite all the redirects, try to blindly get it
        if (httpResponseMessage.Content.Headers.ContentType?.MediaType != "text/plain")
            return null;
        return await httpResponseMessage.Content.ReadAsStringAsync();
    }

    private async Task<string?> ResolveFromRepositoryUrlAsync(ResolvedFileInfo resolvedFileInfo, bool useFinalUrl)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.RepositoryUrl))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.RepositoryUrl, out string? value))
            return value;
        if (!Uri.TryCreate(resolvedFileInfo.NuSpec.RepositoryUrl, UriKind.Absolute, out var uri))
            return null;
        var license = useFinalUrl
            ? await ResolveFromFinalUrlAsync(uri, repositoryUriLicenseResolvers)
            : await ResolveFromUrlAsync(uri, repositoryUriLicenseResolvers);
        if (license != null)
        {
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[resolvedFileInfo.NuSpec.RepositoryUrl] = license;
        }
        return license;
    }

    private async Task<string?> ResolveFromLicenseUrlAsync(ResolvedFileInfo resolvedFileInfo, bool useFinalUrl)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.LicenseUrl))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.LicenseUrl, out string? value))
            return value;
        if (!Uri.TryCreate(resolvedFileInfo.NuSpec.LicenseUrl, UriKind.Absolute, out var uri))
            return null;
        var license = useFinalUrl
            ? await ResolveFromFinalUrlAsync(uri, licenseUriLicenseResolvers)
            : await ResolveFromUrlAsync(uri, licenseUriLicenseResolvers);
        if (license != null)
        {
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[resolvedFileInfo.NuSpec.LicenseUrl] = license;
        }
        return license;
    }
}
