using DotnetThirdPartyNotices.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DotnetThirdPartyNotices.Services;

internal partial class LicenseService(ILogger<LicenseService> logger, IEnumerable<ILicenseUriLicenseResolver> licenseUriLicenseResolvers, IEnumerable<IProjectUriLicenseResolver> projectUriLicenseResolvers, IEnumerable<IRepositoryUriLicenseResolver> repositoryUriLicenseResolvers, IEnumerable<IFileVersionInfoLicenseResolver> fileVersionInfoLicenseResolvers, IHttpClientFactory httpClientFactory) : ILicenseService
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
            ?? (await ResolveFromSourcePathAsync(resolvedFileInfo))
            ?? (await ResolveFromFileVersionInfoAsync(resolvedFileInfo))
            ?? (await ResolveFromLicenseUrlAsync(resolvedFileInfo, true))
            ?? (await ResolveFromRepositoryUrlAsync(resolvedFileInfo, true))
            ?? (await ResolveFromProjectUrlAsync(resolvedFileInfo, true));
    }

    private static string? UnifyLicense(string? license)
    {
        if (string.IsNullOrWhiteSpace(license))
            return null;
        // remove empty lines from the beggining and at the end of the license
        license = license.Trim('\r', '\n');
        // unify new line characters
        license = SingleLineFeedRegex().Replace(license, "\r\n");
        license = SingleCarriageReturnRegex().Replace(license, "\r\n");
        //remove control characters
        license = ControlCharacterRegex().Replace(license, string.Empty);
        //remove leading spaces in the whole license
        var lines = license.Split('\n');
        var leadingSpacesCount = lines.Aggregate((int?)null, (current, line) =>
        {
            if (current == 0 || string.IsNullOrWhiteSpace(line))
                return current;
            int spacesCount = line.TakeWhile(x => x == ' ').Count();
            return current == null ? spacesCount : Math.Min(spacesCount, current.Value);
        });
        if (leadingSpacesCount > 0)
            license = string.Join('\n', lines.Select(x => x.Length == 0 ? x : x[Math.Min(x.Length, leadingSpacesCount.Value)..]));
        return license;
    }

    private async Task<string?> ResolveFromPackagePathAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.PackagePath))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.PackagePath, out string? value))
            return value;
        var licensePath = Directory.EnumerateFiles(resolvedFileInfo.PackagePath, "*.*", new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = false
        }).FirstOrDefault(LicenseFileRegex().IsMatch);
        if (licensePath == null)
            return null;
        var license = await File.ReadAllTextAsync(licensePath);
        license = UnifyLicense(license);
        if (license == null)
            return null;
        if (resolvedFileInfo.NuSpec != null)
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
        LicenseCache[resolvedFileInfo.PackagePath] = license;
        return license;
    }

    private async Task<string?> ResolveFromSourcePathAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.SourcePath))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.SourcePath, out string? value))
            return value;
        try
        {
            var directoryPath = Path.GetDirectoryName(resolvedFileInfo.SourcePath);
            if (directoryPath == null)
                return null;
            var fileName = Path.GetFileNameWithoutExtension(resolvedFileInfo.SourcePath) ?? string.Empty;
            Regex regex = new($"\\\\(|{Regex.Escape(fileName)}[+-_])license\\.(txt|md)$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(300));
            var licensePath = Directory.EnumerateFiles(directoryPath, "*.*", new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            }).FirstOrDefault(regex.IsMatch);
            if (licensePath == null)
                return null;
            var license = await File.ReadAllTextAsync(licensePath);
            license = UnifyLicense(license);
            if (license == null)
                return null;
            LicenseCache[directoryPath] = license;
            return license;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve {path}", resolvedFileInfo.SourcePath);
            return null;
        }
    }

    private async Task<string?> ResolveFromFileVersionInfoAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if(resolvedFileInfo.VersionInfo == null)
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.VersionInfo.FileName, out string? value))
            return value;
        try
        {
            foreach (var resolver in fileVersionInfoLicenseResolvers)
            {
                if (!resolver.CanResolve(resolvedFileInfo.VersionInfo))
                    continue;
                var license = await resolver.Resolve(resolvedFileInfo.VersionInfo);
                license = UnifyLicense(license);
                if (license != null)
                {
                    LicenseCache[resolvedFileInfo.VersionInfo.FileName] = license;
                    return license;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve {path}", resolvedFileInfo.VersionInfo.FileName);
            return null;
        }
    }

    private async Task<string?> ResolveFromLicenseRelativePathAsync(ResolvedFileInfo resolvedFileInfo)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.PackagePath) || string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.LicenseRelativePath))
            return null;
        var licenseFullPath = Path.Combine(resolvedFileInfo.PackagePath, resolvedFileInfo.NuSpec.LicenseRelativePath);
        try
        {
            if (LicenseCache.TryGetValue(licenseFullPath, out string? value))
                return value;
            if (!licenseFullPath.EndsWith(".txt") && !licenseFullPath.EndsWith(".md") || !File.Exists(licenseFullPath))
                return null;
            var license = await File.ReadAllTextAsync(licenseFullPath);
            license = UnifyLicense(license);
            if (license == null)
                return null;
            license = license.Trim();
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[licenseFullPath] = license;
            return license;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve {path}", licenseFullPath);
            return null;
        }
    }

    private async Task<string?> ResolveFromProjectUrlAsync(ResolvedFileInfo resolvedFileInfo, bool useFinalUrl)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.ProjectUrl))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.ProjectUrl, out string? value))
            return value;
        if (!Uri.TryCreate(resolvedFileInfo.NuSpec.ProjectUrl, UriKind.Absolute, out var uri))
            return null;
        try
        {
            var license = useFinalUrl
            ? await ResolveFromFinalUrlAsync(uri, projectUriLicenseResolvers)
            : await ResolveFromUrlAsync(uri, projectUriLicenseResolvers);
            license = UnifyLicense(license);
            if (license == null)
                return null;
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[resolvedFileInfo.NuSpec.ProjectUrl] = license;
            return license;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve {url}", resolvedFileInfo.NuSpec.ProjectUrl);
            return null;
        }
    }

    private async Task<string?> ResolveFromUrlAsync(Uri uri, IEnumerable<IUriLicenseResolver> urlLicenseResolvers)
    {
        foreach (var resolver in urlLicenseResolvers)
        {
            if (!resolver.CanResolve(uri))
                continue;
            var license = await resolver.Resolve(uri);
            license = UnifyLicense(license);
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
                license = UnifyLicense(license);
                if (license != null)
                    return license;
            }
        }
        // Finally, if no license uri can be found despite all the redirects, try to blindly get it
        if (httpResponseMessage.Content.Headers.ContentType?.MediaType != "text/plain")
            return null;
        var license2 = await httpResponseMessage.Content.ReadAsStringAsync();
        return UnifyLicense(license2);
    }

    private async Task<string?> ResolveFromRepositoryUrlAsync(ResolvedFileInfo resolvedFileInfo, bool useFinalUrl)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.RepositoryUrl))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.RepositoryUrl, out string? value))
            return value;
        if (!Uri.TryCreate(resolvedFileInfo.NuSpec.RepositoryUrl, UriKind.Absolute, out var uri))
            return null;
        try
        {
            var license = useFinalUrl
            ? await ResolveFromFinalUrlAsync(uri, repositoryUriLicenseResolvers)
            : await ResolveFromUrlAsync(uri, repositoryUriLicenseResolvers);
            license = UnifyLicense(license);
            if (license == null)
                return null;
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[resolvedFileInfo.NuSpec.RepositoryUrl] = license;
            return license;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve {url}", resolvedFileInfo.NuSpec.RepositoryUrl);
            return null;
        }
    }

    private async Task<string?> ResolveFromLicenseUrlAsync(ResolvedFileInfo resolvedFileInfo, bool useFinalUrl)
    {
        if (string.IsNullOrEmpty(resolvedFileInfo.NuSpec?.LicenseUrl))
            return null;
        if (LicenseCache.TryGetValue(resolvedFileInfo.NuSpec.LicenseUrl, out string? value))
            return value;
        if (!Uri.TryCreate(resolvedFileInfo.NuSpec.LicenseUrl, UriKind.Absolute, out var uri))
            return null;
        try
        {
            var license = useFinalUrl
            ? await ResolveFromFinalUrlAsync(uri, licenseUriLicenseResolvers)
            : await ResolveFromUrlAsync(uri, licenseUriLicenseResolvers);
            license = UnifyLicense(license);
            if (license == null)
                return null;
            LicenseCache[resolvedFileInfo.NuSpec.Id] = license;
            LicenseCache[resolvedFileInfo.NuSpec.LicenseUrl] = license;
            return license;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve {url}", resolvedFileInfo.NuSpec.LicenseUrl);
            return null;
        }
    }
    [GeneratedRegex(@"\\license\.(txt|md)$", RegexOptions.IgnoreCase, 300)]
    private static partial Regex LicenseFileRegex();
    [GeneratedRegex(@"(\f|\uFEFF|\u200B)", RegexOptions.None, 300)]
    private static partial Regex ControlCharacterRegex();
    [GeneratedRegex(@"(?<!\r)\n", RegexOptions.None, 300)]
    private static partial Regex SingleLineFeedRegex();
    [GeneratedRegex(@"\r(?!\n)", RegexOptions.None, 300)]
    private static partial Regex SingleCarriageReturnRegex();
}
