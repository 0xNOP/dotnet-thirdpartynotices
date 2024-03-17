using DotnetThirdPartyNotices.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Services;

internal class GithubRepositoryLicenseResolver(IHttpClientFactory httpClientFactory) : IProjectUriLicenseResolver, IRepositoryUriLicenseResolver
{
    public Task<bool> CanResolveAsync(Uri uri, ResolverOptions resolverOptions, CancellationToken cancellationToken) => Task.FromResult(uri.Host == "github.com");

    public async Task<string?> ResolveAsync(Uri licenseUri, ResolverOptions resolverOptions, CancellationToken cancellationToken)
    {
        var repositoryPath = licenseUri.AbsolutePath.TrimEnd('/');
        if (repositoryPath.EndsWith(".git"))
            repositoryPath = repositoryPath[..^4];
        var httpClient = httpClientFactory.CreateClient();
        UriBuilder uriBuilder = new("https://api.github.com")
        {
            Path = $"repos{repositoryPath}/license"
        };
        // https://developer.github.com/v3/#user-agent-required
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
        if (!string.IsNullOrEmpty(resolverOptions.GitHubToken))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resolverOptions.GitHubToken);
        var response = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDocument = JsonDocument.Parse(json);
        var rootElement = jsonDocument.RootElement;
        var encoding = rootElement.GetProperty("encoding").GetString();
        var content = rootElement.GetProperty("content").GetString();
        if (content == null || encoding != "base64")
            return content;
        var bytes = Convert.FromBase64String(content);
        return Encoding.UTF8.GetString(bytes);
    }
}