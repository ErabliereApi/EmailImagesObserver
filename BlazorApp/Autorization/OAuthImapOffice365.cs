using BlazorApp.AzureComputerVision;
using BlazorApp.Extension;
using MailKit.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace BlazorApp.Autorization;

public class OAuthImapOffice365
{
    private readonly IConfiguration _config;
    private readonly LoginInfo _options;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthImapOffice365> _logger;

    public OAuthImapOffice365(
        IConfiguration config, 
        IOptions<LoginInfo> options, 
        IMemoryCache cache, 
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthImapOffice365> logger)
    {
        _config = config;
        _options = options.Value;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // TODO
    // https://stackoverflow.com/questions/73039215/authentication-failure-for-imap-using-client-credential-flow-for-oauth2-0-java
    // https://www.youtube.com/watch?v=bMYA-146dmM

    public async Task<SaslMechanism> GetAuthOptions()
    {
        var request = await GenerateTokenRequestForClientFlowAsync(
            $"{_config.GetAAD("Instance")}{_config.GetAAD("TenantId")}",
            "https://outlook.office365.com/.default",
            $"{_config.GetAAD("ClientSecret")}");

        await DumpTokenRequest(request);

        var client = _httpClientFactory.CreateClient("default");
        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string error = "";
            if (response.Content != null)
            {
                error = (await response.Content.ReadAsStringAsync()) ?? "";
            }
            throw new InvalidOperationException($"Error in http request: {error}");
        }

        var stringResponse = await response.Content.ReadAsStringAsync();
        var obj = JsonConvert.DeserializeAnonymousType(stringResponse, new
        {
            access_token = ""
        })!;

        _logger.LogError("{accessToken}", obj.access_token);

        var oauth2 = new SaslMechanismOAuth2(
            _config["LoginInfo:EmailLogin"], obj.access_token);

        return oauth2;
    }

    private async Task DumpTokenRequest(HttpRequestMessage request)
    {
        var content = await request.Content.ReadAsStringAsync();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Request to:\n {request.RequestUri}");
        var queryStringParsed = QueryHelpers.ParseQuery(content);
        foreach (var element in queryStringParsed)
        {
            var value = element.Value;
            if (element.Key.Contains("secret"))
            {
                value = new String('*', element.Value.Single().Length);
            }
            sb.AppendLine($"{element.Key} = {value}");
        }
        _logger.LogDebug(sb.ToString());
    }

    /// <summary>
    /// Generate the token request httpmessage for a simple client flow.
    /// </summary>
    /// <param name="clientSecret">Client secret if provided</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<HttpRequestMessage> GenerateTokenRequestForClientFlowAsync(string authority, string scope, string clientSecret)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>()
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = scope,
            ["client_secret "] = clientSecret,
            ["client_id"] = _config.GetAAD("ClientId") ?? throw new KeyNotFoundException("AzureAD:ClientId")
        };

        var content = new FormUrlEncodedContent(parameters);
        var tokenRequestUrl = await GetTokenUrlAsync(authority);
        var request = new HttpRequestMessage(HttpMethod.Post, tokenRequestUrl);

        request.Content = content;
        return request;
    }

    private async Task<string> GetTokenUrlAsync(string authority)
    {
        authority = authority.TrimEnd('/');
        WellKnownEndpoint endpoints = await GetEndpointForAuthorityAsync(authority);
        return endpoints.TokenEndpointUrl;
    }

    private async Task<WellKnownEndpoint> GetEndpointForAuthorityAsync(string authority)
    {
        WellKnownEndpoint? endpoints = null;

        if (!_cache.TryGetValue(authority, out endpoints))
        {
            endpoints = await DownloadEndpointsForAuthorityAsync(authority);
            _cache.Set(authority.TrimEnd('/'), endpoints);
        }

        if (endpoints == null) 
        {
            throw new InvalidOperationException("endpoints cannot be null");
        }

        return endpoints;
    }

    private async Task<WellKnownEndpoint> DownloadEndpointsForAuthorityAsync(string authority)
    {
        var client = _httpClientFactory.CreateClient(nameof(OAuthImapOffice365));
        var url = $"{authority}/.well-known/openid-configuration";
        var response = await client.GetStringAsync(url);
        var responseJson = JsonConvert.DeserializeAnonymousType(response, new
        {
            token_endpoint = "",
            authorization_endpoint = "",
        }) ?? throw new InvalidOperationException("responseJson is null");
        return new WellKnownEndpoint(responseJson.authorization_endpoint, responseJson.token_endpoint);
    }

    private record WellKnownEndpoint
    {
        public WellKnownEndpoint(string autorizeUrl, string tokenEndpointUrl)
        {
            AutorizeUrl = autorizeUrl;
            TokenEndpointUrl = tokenEndpointUrl;
        }

        public string AutorizeUrl { get; }
        public string TokenEndpointUrl { get; }
    }
}
