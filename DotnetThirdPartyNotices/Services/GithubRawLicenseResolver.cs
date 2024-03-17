using DotnetThirdPartyNotices.Models;
using System.Net.Http.Headers;
using System.Text;

namespace DotnetThirdPartyNotices.Services;

internal class GithubRawLicenseResolver(IHttpClientFactory httpClientFactory) : ILicenseUriLicenseResolver
{
    public Task<bool> CanResolveAsync(Uri uri, ResolverOptions resolverOptions, CancellationToken cancellationToken) => Task.FromResult(uri.Host == "github.com");

    public async Task<string?> ResolveAsync(Uri uri, ResolverOptions resolverOptions, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder(uri) { Host = "raw.githubusercontent.com" };
        uriBuilder.Path = uriBuilder.Path.Replace("/blob", string.Empty);
        var httpClient = httpClientFactory.CreateClient();
        // https://developer.github.com/v3/#user-agent-required
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
        if (!string.IsNullOrEmpty(resolverOptions.GitHubToken))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resolverOptions.GitHubToken);
        var httpResponseMessage = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken);
        if (!httpResponseMessage.IsSuccessStatusCode
            || httpResponseMessage.Content.Headers.ContentType?.MediaType != "text/plain")
            return null;
        return await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
    }
}
