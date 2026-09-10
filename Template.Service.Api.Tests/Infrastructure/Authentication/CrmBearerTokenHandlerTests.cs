using System.Net;
using Template.Service.Services.Infrastructure.Authentication;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Authentication;

public sealed class CrmBearerTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsCrmBearerToken()
    {
        var tokenProvider = new StubCrmAccessTokenProvider();
        var primaryHandler = new RecordingHandler(HttpStatusCode.OK);
        using var handler = new CrmBearerTokenHandler(tokenProvider)
        {
            InnerHandler = primaryHandler
        };
        using var invoker = new HttpMessageInvoker(handler);

        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://crm.example.test/customers"),
            CancellationToken.None);

        Assert.Equal("Bearer", primaryHandler.AuthorizationScheme);
        Assert.Equal("crm-token", primaryHandler.AuthorizationParameter);
        Assert.Empty(tokenProvider.InvalidatedTokens);
    }

    [Fact]
    public async Task SendAsync_UnauthorizedInvalidatesTokenWithoutRetryingRequest()
    {
        var tokenProvider = new StubCrmAccessTokenProvider();
        var primaryHandler = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var handler = new CrmBearerTokenHandler(tokenProvider)
        {
            InnerHandler = primaryHandler
        };
        using var invoker = new HttpMessageInvoker(handler);

        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://crm.example.test/customers"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(new[] { "crm-token" }, tokenProvider.InvalidatedTokens);
        Assert.Equal(1, primaryHandler.RequestCount);
    }

    private sealed class StubCrmAccessTokenProvider : ICrmAccessTokenProvider
    {
        public List<string> InvalidatedTokens { get; } = new();

        public Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("crm-token");
        }

        public void Invalidate(string accessToken)
        {
            InvalidatedTokens.Add(accessToken);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public RecordingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public int RequestCount { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}

