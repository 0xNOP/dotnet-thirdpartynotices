using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace DotnetThirdPartyNotices;

internal class GithubService : IDisposable
{
    private readonly HttpClient _httpClient;

    public GithubService()
    {
        _httpClient = new HttpClient {BaseAddress = new Uri("https://api.github.com")};
        // https://developer.github.com/v3/#user-agent-required
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public async Task<string> GetLicenseContentFromId(string licenseId)
    {
        var json = await _httpClient.GetStringAsync($"licenses/{licenseId}");
        var jsonDocument = JsonDocument.Parse(json);
        return jsonDocument.RootElement.GetProperty("body").GetString();
    }

    public async Task<string> GetLicenseContentFromRepositoryPath(string repositoryPath)
    {
        repositoryPath = repositoryPath.TrimEnd('/');
        if (repositoryPath.EndsWith(".git"))
            repositoryPath = repositoryPath[..^4];
        var json = await _httpClient.GetStringAsync($"repos{repositoryPath}/license");
        var jsonDocument = JsonDocument.Parse(json);

        var rootElement = jsonDocument.RootElement;
        var encoding = rootElement.GetProperty("encoding").GetString();
        var content = rootElement.GetProperty("content").GetString();

        if (encoding != "base64") return content;

        var bytes = Convert.FromBase64String(content);
        return Encoding.UTF8.GetString(bytes);
    }

}