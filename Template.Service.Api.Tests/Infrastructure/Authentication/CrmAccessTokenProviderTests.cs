using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Template.Service.DataModel.Crm;
using Template.Service.Services.Infrastructure.Authentication;
using Template.Service.Services.Infrastructure.Http;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Authentication;

public sealed class CrmAccessTokenProviderTests
{
    [Fact]
    public async Task GetTokenAsync_CachesTokenAndUsesDedicatedUnauthenticatedClient()
    {
        var handler = new TokenEndpointHandler(requestNumber =>
            CreateTokenResponse($"crm-token-{requestNumber}", 3600));
        using var provider = CreateProvider(handler);

        var firstToken = await provider.GetAccessTokenAsync();
        var secondToken = await provider.GetAccessTokenAsync();

        Assert.Equal("crm-token-1", firstToken);
        Assert.Equal(firstToken, secondToken);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(
            "https://crm.example.test/access_token",
            handler.LastRequestUri?.AbsoluteUri);
        Assert.Equal(
            "application/x-www-form-urlencoded",
            handler.LastContentType);
        Assert.Null(handler.AuthorizationScheme);
        Assert.Contains("grant_type=client_credentials", handler.LastRequestBody);
        Assert.Contains("client_id=crm-client", handler.LastRequestBody);
        Assert.Contains("client_secret=crm-client-secret", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetTokenAsync_RefreshesThirtySecondsBeforeExpiry()
    {
        var timeProvider = new ManualTimeProvider();
        var handler = new TokenEndpointHandler(requestNumber =>
            CreateTokenResponse($"crm-token-{requestNumber}", 3600));
        using var provider = CreateProvider(handler, timeProvider);

        var firstToken = await provider.GetAccessTokenAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(3569));
        var cachedToken = await provider.GetAccessTokenAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var refreshedToken = await provider.GetAccessTokenAsync();

        Assert.Equal("crm-token-1", firstToken);
        Assert.Equal(firstToken, cachedToken);
        Assert.Equal("crm-token-2", refreshedToken);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetTokenAsync_ConcurrentCallsSendOneTokenRequest()
    {
        var handler = new TokenEndpointHandler(
            requestNumber => CreateTokenResponse($"crm-token-{requestNumber}", 3600),
            TimeSpan.FromMilliseconds(100));
        using var provider = CreateProvider(handler);

        var tokenTasks = Enumerable.Range(0, 20)
            .Select(_ => provider.GetAccessTokenAsync())
            .ToArray();
        var tokens = await Task.WhenAll(tokenTasks);

        Assert.All(tokens, token => Assert.Equal("crm-token-1", token));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("{\"expires_in\":3600,\"token_type\":\"Bearer\"}")]
    [InlineData("{\"access_token\":\"token\",\"expires_in\":0,\"token_type\":\"Bearer\"}")]
    [InlineData("{\"access_token\":\"token\",\"expires_in\":3600,\"token_type\":\"MAC\"}")]
    [InlineData("not-json")]
    public async Task GetTokenAsync_InvalidResponseThrowsControlledException(
        string responseBody)
    {
        var handler = new TokenEndpointHandler(_ => responseBody);
        using var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AccessTokenProviderException>(() =>
            provider.GetAccessTokenAsync());

        Assert.DoesNotContain("crm-client-secret", exception.ToString());
        Assert.DoesNotContain("access_token", exception.ToString());
    }

    [Fact]
    public async Task Invalidate_ExpiresOnlyTheMatchingCachedToken()
    {
        var handler = new TokenEndpointHandler(requestNumber =>
            CreateTokenResponse($"crm-token-{requestNumber}", 3600));
        using var provider = CreateProvider(handler);

        var firstToken = await provider.GetAccessTokenAsync();
        provider.Invalidate("different-token");
        var stillCachedToken = await provider.GetAccessTokenAsync();
        provider.Invalidate(firstToken);
        var refreshedToken = await provider.GetAccessTokenAsync();

        Assert.Equal(firstToken, stillCachedToken);
        Assert.Equal("crm-token-2", refreshedToken);
        Assert.Equal(2, handler.RequestCount);
    }

    private static CrmAccessTokenProvider CreateProvider(
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://crm.example.test/")
        };

        return new CrmAccessTokenProvider(
            new SingleClientFactory(httpClient),
            new CrmClientOptions
            {
                ClientId = "crm-client",
                ClientSecret = "crm-client-secret",
                RefreshBeforeExpirySeconds = 30
            },
            timeProvider ?? new ManualTimeProvider(),
            NullLogger<CrmAccessTokenProvider>.Instance);
    }

    private static string CreateTokenResponse(string token, int expiresIn)
    {
        return $"{{\"access_token\":\"{token}\",\"expires_in\":{expiresIn},\"token_type\":\"Bearer\"}}";
    }

    private sealed class TokenEndpointHandler : HttpMessageHandler
    {
        private readonly Func<int, string> _responseFactory;
        private readonly TimeSpan _delay;
        private int _requestCount;

        public TokenEndpointHandler(
            Func<int, string> responseFactory,
            TimeSpan? delay = null)
        {
            _responseFactory = responseFactory;
            _delay = delay ?? TimeSpan.Zero;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public HttpMethod? LastMethod { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        public string? LastContentType { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_delay != TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseFactory(requestNumber),
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public SingleClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name)
        {
            Assert.Equal(HttpClientNames.CRMToken, name);
            return _httpClient;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}

