using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.Extensions;

internal static class UriExtensions
{
    public static bool IsGithubUri(this Uri uri) => uri.Host == "github.com";

    public static Uri ToRawGithubUserContentUri(this Uri uri)
    {
        if (!IsGithubUri(uri))
            throw new InvalidOperationException();

        var uriBuilder = new UriBuilder(uri) { Host = "raw.githubusercontent.com" };
        uriBuilder.Path = uriBuilder.Path.Replace("/blob", string.Empty);
        return uriBuilder.Uri;
    }

    public static async Task<Uri> GetRedirectUri(this Uri uri)
    {
        using var httpClientHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        using var httpClient = new HttpClient(httpClientHandler);
        var httpResponseMessage = await httpClient.GetAsync(uri);

        var statusCode = (int)httpResponseMessage.StatusCode;

        if (statusCode is < 300 or > 399)
        {
            return null;
        }

        var redirectUri = httpResponseMessage.Headers.Location;
        if (!redirectUri.IsAbsoluteUri)
        {
            redirectUri = new Uri(httpResponseMessage.RequestMessage.RequestUri, redirectUri);
        }

        return redirectUri;
    }

    public static async Task<string> GetPlainText(this Uri uri)
    {
        using var httpClient = new HttpClient();
        var httpResponseMessage = await httpClient.GetAsync(uri);
        if (!httpResponseMessage.IsSuccessStatusCode && uri.AbsolutePath.EndsWith(".txt"))
        {
            // try without .txt extension
            var fixedUri = new UriBuilder(uri);
            fixedUri.Path = fixedUri.Path.Remove(fixedUri.Path.Length - 4);
            httpResponseMessage = await httpClient.GetAsync(fixedUri.Uri);
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                return null;
            }
        }

        if (httpResponseMessage.Content.Headers.ContentType.MediaType != "text/plain")
        {
            return null;
        }

        return await httpResponseMessage.Content.ReadAsStringAsync();
    }
}