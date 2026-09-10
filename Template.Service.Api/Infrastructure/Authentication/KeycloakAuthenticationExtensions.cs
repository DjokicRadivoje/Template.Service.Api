using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Template.Service.Api.Infrastructure.Authentication;

public static class KeycloakAuthenticationExtensions
{
    private const int MaximumClockSkewSeconds = 300;

    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetRequiredSection(
            KeycloakAuthenticationOptions.SectionName);
        var keycloakOptions = section.Get<KeycloakAuthenticationOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{KeycloakAuthenticationOptions.SectionName}' is invalid.");

        Validate(keycloakOptions);

        services.Configure<KeycloakAuthenticationOptions>(section);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority.TrimEnd('/');
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = keycloakOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(keycloakOptions.ClockSkewSeconds)
                };
                options.Events = CreateSafeLoggingEvents();
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static void Validate(KeycloakAuthenticationOptions options)
    {
        if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
            || !string.Equals(authority.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{KeycloakAuthenticationOptions.SectionName}:Authority' must be an absolute HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException(
                $"'{KeycloakAuthenticationOptions.SectionName}:Audience' is required.");
        }

        if (options.ClockSkewSeconds is < 0 or > MaximumClockSkewSeconds)
        {
            throw new InvalidOperationException(
                $"'{KeycloakAuthenticationOptions.SectionName}:ClockSkewSeconds' must be between 0 and {MaximumClockSkewSeconds}.");
        }
    }

    private static JwtBearerEvents CreateSafeLoggingEvents()
    {
        return new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                logger.LogWarning(
                    "JWT authentication failed for {Path}. Error type: {ErrorType}",
                    context.Request.Path,
                    context.Exception.GetType().Name);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                logger.LogInformation(
                    "JWT authentication challenge for {Path}",
                    context.Request.Path);
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");
                logger.LogWarning(
                    "JWT authorization forbidden for {Path}",
                    context.Request.Path);
                return Task.CompletedTask;
            }
        };
    }
}

