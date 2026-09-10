namespace Template.Service.Services.Infrastructure.Authentication;

public sealed class CrmClientOptions
{
    public const string SectionName = "CrmClient";

    public const int DefaultRefreshBeforeExpirySeconds = 30;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public int RefreshBeforeExpirySeconds { get; init; } =
        DefaultRefreshBeforeExpirySeconds;
}

