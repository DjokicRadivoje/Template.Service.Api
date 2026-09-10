using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Template.Service.Services.Infrastructure.Http;

namespace Template.Service.Services.Infrastructure.Authentication;

public sealed class CrmAccessTokenProvider : ICrmAccessTokenProvider, IDisposable
{
    internal const string TokenPath = "access_token";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CrmClientOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CrmAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CachedAccessToken? _cachedAccessToken;
    private bool _disposed;

    public CrmAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        CrmClientOptions options,
        TimeProvider timeProvider,
        ILogger<CrmAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cachedToken = Volatile.Read(ref _cachedAccessToken);
        if (IsUsable(cachedToken))
        {
            return cachedToken!.Value;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            cachedToken = Volatile.Read(ref _cachedAccessToken);
            if (IsUsable(cachedToken))
            {
                return cachedToken!.Value;
            }

            var refreshedToken = await RequestAccessTokenAsync(cancellationToken);
            Volatile.Write(ref _cachedAccessToken, refreshedToken);
            return refreshedToken.Value;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return;
        }

        var cachedToken = Volatile.Read(ref _cachedAccessToken);
        if (cachedToken is null
            || !string.Equals(
                cachedToken.Value,
                accessToken,
                StringComparison.Ordinal))
        {
            return;
        }

        Interlocked.CompareExchange(
            ref _cachedAccessToken,
            null,
            cachedToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshLock.Dispose();
    }

    private bool IsUsable(CachedAccessToken? cachedToken)
    {
        return cachedToken is not null
            && _timeProvider.GetUtcNow() < cachedToken.RefreshAt;
    }

    private async Task<CachedAccessToken> RequestAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        //var debugBody = await content.ReadAsStringAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = content
        };

        try
        {
            var httpClient = _httpClientFactory.CreateClient(
                HttpClientNames.CRMToken);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CRM token request returned HTTP status {StatusCode}.",
                    (int)response.StatusCode);
                throw new AccessTokenProviderException(
                    $"The CRM access token endpoint returned HTTP status {(int)response.StatusCode}.");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<CrmTokenResponse>(
                cancellationToken: cancellationToken);
            ValidateTokenResponse(tokenResponse);

            var issuedAt = _timeProvider.GetUtcNow();
            var lifetime = TimeSpan.FromSeconds(tokenResponse!.ExpiresIn);
            var configuredRefreshBuffer = TimeSpan.FromSeconds(
                _options.RefreshBeforeExpirySeconds);
            var effectiveRefreshBuffer = configuredRefreshBuffer <= lifetime / 2
                ? configuredRefreshBuffer
                : lifetime / 2;

            return new CachedAccessToken(
                tokenResponse.AccessToken!,
                issuedAt + lifetime - effectiveRefreshBuffer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AccessTokenProviderException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or OperationCanceledException
                or System.Text.Json.JsonException
                or NotSupportedException)
        {
            _logger.LogWarning(
                "CRM access token could not be obtained. Error type: {ErrorType}",
                exception.GetType().Name);
            throw new AccessTokenProviderException(
                "The CRM access token could not be obtained.",
                exception);
        }
    }

    private static void ValidateTokenResponse(CrmTokenResponse? response)
    {
        if (response is null
            || string.IsNullOrWhiteSpace(response.AccessToken)
            || response.ExpiresIn <= 0
            || !string.Equals(
                response.TokenType,
                "Bearer",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new AccessTokenProviderException(
                "The CRM access token endpoint returned an invalid response.");
        }
    }

    private sealed record CachedAccessToken(
        string Value,
        DateTimeOffset RefreshAt);

    private sealed class CrmTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}

