using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Framework.Logger.Correlation;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Authentication;

public sealed class KeycloakAuthenticationIntegrationTests :
    IClassFixture<KeycloakWebApplicationFactory>
{
    private readonly KeycloakWebApplicationFactory _factory;

    public KeycloakAuthenticationIntegrationTests(
        KeycloakWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthorizedResponse_ReturnsValidIncomingCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString("D");
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/test/authentication-probe");
        request.Headers.Add(
            CorrelationIdConstants.HeaderName,
            correlationId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            correlationId,
            Assert.Single(response.Headers.GetValues(
                CorrelationIdConstants.HeaderName)));
    }

    [Fact]
    public async Task UnauthorizedResponse_GeneratesCorrelationIdWhenHeaderIsMissing()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/test/authentication-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var correlationId = Assert.Single(response.Headers.GetValues(
            CorrelationIdConstants.HeaderName));
        Assert.True(Guid.TryParseExact(correlationId, "D", out _));
    }

    [Fact]
    public async Task RequestTelemetry_ContainsValidIncomingCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString("D");
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/test/authentication-probe/telemetry-correlation");
        request.Headers.Add(
            CorrelationIdConstants.HeaderName,
            correlationId);

        using var response = await client.SendAsync(request);
        var telemetryCorrelationId =
            await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlationId, telemetryCorrelationId);
        Assert.Equal(
            correlationId,
            Assert.Single(response.Headers.GetValues(
                CorrelationIdConstants.HeaderName)));
    }

    [Fact]
    public async Task RequestTelemetry_ContainsGeneratedCorrelationId()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/test/authentication-probe/telemetry-correlation");
        var telemetryCorrelationId =
            await response.Content.ReadAsStringAsync();
        var responseCorrelationId = Assert.Single(
            response.Headers.GetValues(CorrelationIdConstants.HeaderName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Guid.TryParseExact(
            telemetryCorrelationId,
            "D",
            out _));
        Assert.Equal(responseCorrelationId, telemetryCorrelationId);
    }

    [Fact]
    public async Task CrmController_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/Crm/mock");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/organizations", false, null)]
    public async Task CrmBusinessEndpoints_WithoutToken_ReturnUnauthorized(
        string method,
        string path,
        bool hasBody,
        string? contentType)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (hasBody)
        {
            request.Content = new StringContent(
                "{}",
                Encoding.UTF8,
                contentType!);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ControllerWithoutAuthorizeAttribute_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/test/authentication-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_ForTemplateAudience_ReturnsOk()
    {
        using var client = CreateAuthenticatedClient(
            _factory.CreateToken());

        using var response = await client.GetAsync("/test/authentication-probe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(TokenFailure.WrongIssuer)]
    [InlineData(TokenFailure.WrongAudience)]
    [InlineData(TokenFailure.Expired)]
    [InlineData(TokenFailure.WrongSignature)]
    public async Task InvalidToken_ReturnsUnauthorized(TokenFailure failure)
    {
        var token = failure switch
        {
            TokenFailure.WrongIssuer => _factory.CreateToken(
                issuer: "https://untrusted.example.test/realms/api-template"),
            TokenFailure.WrongAudience => _factory.CreateToken(
                audience: "another-api"),
            TokenFailure.Expired => _factory.CreateToken(
                expires: DateTime.UtcNow.AddMinutes(-5)),
            TokenFailure.WrongSignature => _factory.CreateToken(
                signingKey: KeycloakWebApplicationFactory.CreateSigningKey(
                    "a-different-test-signing-key-with-at-least-32-bytes")),
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };
        using var client = CreateAuthenticatedClient(token);

        using var response = await client.GetAsync("/test/authentication-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentSwagger_ContainsBearerSecurityDefinition()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        var swaggerDocument = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"Bearer\"", swaggerDocument);
        Assert.Contains("\"scheme\": \"bearer\"", swaggerDocument);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public enum TokenFailure
    {
        WrongIssuer,
        WrongAudience,
        Expired,
        WrongSignature
    }
}

public sealed class KeycloakWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://identity.example.test/realms/api-template";
    public const string Audience = "service-api-template";

    private static readonly SymmetricSecurityKey TrustedSigningKey =
        CreateSigningKey("service-api-template-test-signing-key-with-at-least-32-bytes");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "Authentication:Keycloak:Authority",
            Issuer);
        builder.UseSetting(
            "Authentication:Keycloak:Audience",
            Audience);
        builder.UseSetting(
            "Authentication:Keycloak:RoleClientId",
            Audience);
        builder.UseSetting(
            "Authentication:Keycloak:ClockSkewSeconds",
            "60");
        builder.UseSetting(
            "CrmClient:ClientId",
            "crm-integration-test-client");
        builder.UseSetting(
            "CrmClient:ClientSecret",
            "crm-integration-test-secret");
        builder.UseSetting(
            "CrmClient:RefreshBeforeExpirySeconds",
            "30");
        builder.UseSetting(
            "ApiPaths:CRMApiBaseAddress",
            "https://crm.example.test/");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Keycloak:Authority"] = Issuer,
                ["Authentication:Keycloak:Audience"] = Audience,
                ["Authentication:Keycloak:RoleClientId"] = Audience,
                ["Authentication:Keycloak:ClockSkewSeconds"] = "60",
                ["ApiPaths:CRMApiBaseAddress"] = "https://crm.example.test/",
                ["CrmClient:ClientId"] = "crm-integration-test-client",
                ["CrmClient:ClientSecret"] = "crm-integration-test-secret",
                ["CrmClient:RefreshBeforeExpirySeconds"] = "30"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services
                .AddControllers()
                .PartManager.ApplicationParts.Add(
                    new AssemblyPart(typeof(AuthenticationProbeController).Assembly));

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    var discoveryConfiguration = new OpenIdConnectConfiguration
                    {
                        Issuer = Issuer
                    };
                    discoveryConfiguration.SigningKeys.Add(TrustedSigningKey);

                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            discoveryConfiguration);
                });
        });
    }

    public string CreateToken(
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: new[] { new Claim("sub", "integration-test-client") },
            notBefore: expiresAt.AddMinutes(-10),
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                signingKey ?? TrustedSigningKey,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static SymmetricSecurityKey CreateSigningKey(string value)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value));
    }
}

[ApiController]
[Route("test/authentication-probe")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AuthenticationProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("telemetry-correlation")]
    public IActionResult GetTelemetryCorrelation()
    {
        var requestTelemetry =
            HttpContext.Features.Get<RequestTelemetry>();
        var correlationId = string.Empty;
        requestTelemetry?.Properties.TryGetValue(
            CorrelationIdConstants.LoggingPropertyName,
            out correlationId);

        return Content(correlationId ?? string.Empty);
    }
}

