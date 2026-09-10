namespace Template.Service.Services.Infrastructure.Authentication;

public interface ICrmAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default);

    void Invalidate(string accessToken);
}

