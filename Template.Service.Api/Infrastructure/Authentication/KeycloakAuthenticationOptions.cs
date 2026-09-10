namespace Template.Service.Api.Infrastructure.Authentication;

public sealed class KeycloakAuthenticationOptions
{
    public const string SectionName = "Authentication:Keycloak";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string RoleClientId { get; set; } = string.Empty;

    public int ClockSkewSeconds { get; set; } = 60;
}

