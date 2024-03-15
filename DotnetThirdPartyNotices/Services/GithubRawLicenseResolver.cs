using DotnetThirdPartyNotices.Models;
using System.Net.Http.Headers;
using System.Text;

namespace DotnetThirdPartyNotices.Services;

internal class GithubRawLicenseResolver(IHttpClientFactory httpClientFactory, DynamicSettings dynamicSettings) : ILicenseUriLicenseResolver
{
    public bool CanResolve(Uri uri) => uri.Host == "github.com";

    public async Task<string?> Resolve(Uri uri)
    {
        var uriBuilder = new UriBuilder(uri) { Host = "raw.githubusercontent.com" };
        uriBuilder.Path = uriBuilder.Path.Replace("/blob", string.Empty);
        var httpClient = httpClientFactory.CreateClient();
        // https://developer.github.com/v3/#user-agent-required
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
        if (!string.IsNullOrEmpty(dynamicSettings.GitHubToken))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dynamicSettings.GitHubToken);
        var httpResponseMessage = await httpClient.GetAsync(uriBuilder.Uri);
        if (!httpResponseMessage.IsSuccessStatusCode
            || httpResponseMessage.Content.Headers.ContentType?.MediaType != "text/plain")
            return null;
        return await httpResponseMessage.Content.ReadAsStringAsync();
    }
}
