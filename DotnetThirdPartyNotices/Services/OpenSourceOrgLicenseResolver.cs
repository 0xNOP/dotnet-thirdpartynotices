using DotnetThirdPartyNotices.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services;

internal class OpenSourceOrgLicenseResolver : ILicenseUriLicenseResolver
{
    public Task<bool> CanResolveAsync(Uri licenseUri, ResolverOptions resolverOptions, CancellationToken cancellationToken) => Task.FromResult(licenseUri.Host == "opensource.org");

    internal static readonly char[] separator = ['/'];

    public async Task<string?> ResolveAsync(Uri licenseUri, ResolverOptions resolverOptions, CancellationToken cancellationToken)
    {
        var s = licenseUri.AbsolutePath.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (s[0] != "licenses")
            return null;
        var licenseId = s[1];
        HttpClient httpClient = new() { BaseAddress = new Uri("https://api.github.com") };
        // https://developer.github.com/v3/#user-agent-required
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
        if (!string.IsNullOrEmpty(resolverOptions.GitHubToken))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resolverOptions.GitHubToken);
        var httpResponseMessage = await httpClient.GetAsync($"licenses/{licenseId}", cancellationToken);
        if (!httpResponseMessage.IsSuccessStatusCode)
            return null;
        var content = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        var jsonDocument = JsonDocument.Parse(content);
        return jsonDocument.RootElement.GetProperty("body").GetString();
    }
}