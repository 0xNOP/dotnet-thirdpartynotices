using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services;

internal class OpenSourceOrgLicenseResolver : ILicenseUriLicenseResolver
{
    public bool CanResolve(Uri licenseUri) => licenseUri.Host == "opensource.org";

    internal static readonly char[] separator = ['/'];

    public async Task<string?> Resolve(Uri licenseUri)
    {
        var s = licenseUri.AbsolutePath.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (s[0] != "licenses")
            return null;
        var licenseId = s[1];
        HttpClient httpClient = new() { BaseAddress = new Uri("https://api.github.com") };
        // https://developer.github.com/v3/#user-agent-required
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
        var httpResponseMessage = await httpClient.GetAsync($"licenses/{licenseId}");
        if (!httpResponseMessage.IsSuccessStatusCode)
            return null;
        var content = await httpResponseMessage.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        return jsonDocument.RootElement.GetProperty("body").GetString();
    }
}