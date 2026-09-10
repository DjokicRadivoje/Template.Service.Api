using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Template.Service.Api.Infrastructure.Authentication;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Authentication;

public sealed class KeycloakAuthenticationRegistrationTests
{
    [Fact]
    public void AddKeycloakAuthentication_ThrowsWhenConfigurationSectionIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddKeycloakAuthentication(configuration));

        Assert.Contains(KeycloakAuthenticationOptions.SectionName, exception.Message);
    }

    [Theory]
    [InlineData("http://identity.example.test/realms/api-template", "service-api-template", 60)]
    [InlineData("https://identity.example.test/realms/api-template", "", 60)]
    [InlineData("https://identity.example.test/realms/api-template", "service-api-template", -1)]
    [InlineData("https://identity.example.test/realms/api-template", "service-api-template", 301)]
    public void AddKeycloakAuthentication_ThrowsWhenConfigurationIsInvalid(
        string authority,
        string audience,
        int clockSkewSeconds)
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(authority, audience, clockSkewSeconds);

        Assert.Throws<InvalidOperationException>(() =>
            services.AddKeycloakAuthentication(configuration));
    }

    [Fact]
    public void AddKeycloakAuthentication_AcceptsValidConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            "https://identity.example.test/realms/api-template",
            "service-api-template",
            60);

        var result = services.AddKeycloakAuthentication(configuration);

        Assert.Same(services, result);
    }

    private static IConfiguration CreateConfiguration(
        string authority,
        string audience,
        int clockSkewSeconds)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{KeycloakAuthenticationOptions.SectionName}:Authority"] = authority,
                [$"{KeycloakAuthenticationOptions.SectionName}:Audience"] = audience,
                [$"{KeycloakAuthenticationOptions.SectionName}:RoleClientId"] = "service-api-template",
                [$"{KeycloakAuthenticationOptions.SectionName}:ClockSkewSeconds"] =
                    clockSkewSeconds.ToString()
            })
            .Build();
    }
}

