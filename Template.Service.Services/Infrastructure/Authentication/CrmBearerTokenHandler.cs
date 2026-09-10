using System.Net;
using System.Net.Http.Headers;

namespace Template.Service.Services.Infrastructure.Authentication;

public sealed class CrmBearerTokenHandler : DelegatingHandler
{
    private readonly ICrmAccessTokenProvider _accessTokenProvider;

    public CrmBearerTokenHandler(ICrmAccessTokenProvider accessTokenProvider)
    {
        _accessTokenProvider = accessTokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(
            cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _accessTokenProvider.Invalidate(accessToken);
        }

        return response;
    }
}

