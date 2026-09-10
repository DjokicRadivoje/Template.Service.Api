using Autofac;
using Autofac.Extensions.DependencyInjection;
using Framework.Logger.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Template.Service.DependencyInjection;
using Template.Service.BusinessLogic;
using Template.Service.BusinessLogic.Interfaces;
using Template.Service.Mapper;
using Template.Service.Services;
using Template.Service.Services.Infrastructure.Http;
using Template.Service.Services.Interfaces;
using Xunit;

namespace Template.Service.Api.Tests.DependencyInjection;

public sealed class DependencyInjectionConfigTests
{
    [Fact]
    public void ConfigureContainer_ResolvesRetainedBusinessFlows()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorrelationId();
        services.AddAutoMapper(typeof(DefaultProfile));
        DependencyInjectionConfig.ConfigureHttpClients(
            services,
            CreateConfiguration());

        var builder = new ContainerBuilder();
        builder.Populate(services);
        DependencyInjectionConfig.ConfigureContainer(builder);
        using var container = builder.Build();

        Assert.IsType<CrmBusinessLogic>(container.Resolve<ICrmBusinessLogic>());
        Assert.IsType<OrganizationBusinessLogic>(
            container.Resolve<IOrganizationBusinessLogic>());
        Assert.IsType<CrmService>(container.Resolve<ICrmService>());
        Assert.IsType<HttpResponseHandler>(container.Resolve<IHttpResponseHandler>());
        Assert.IsType<CrmResponseHandler>(container.Resolve<ICrmResponseHandler>());
    }

    [Fact]
    public void ConfigureHttpClients_RegistersOnlyRetainedNamedClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorrelationId();
        DependencyInjectionConfig.ConfigureHttpClients(
            services,
            CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(
            new Uri("https://crm.example.test/"),
            factory.CreateClient(HttpClientNames.CRMApi).BaseAddress);
        Assert.Equal(
            new Uri("https://crm.example.test/"),
            factory.CreateClient(HttpClientNames.CRMToken).BaseAddress);
    }

    [Fact]
    public void ConfigureHttpClients_MissingCrmBaseAddressThrows()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CrmClient:ClientId"] = "test-client",
                ["CrmClient:ClientSecret"] = "test-secret"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DependencyInjectionConfig.ConfigureHttpClients(
                services,
                configuration));

        Assert.Contains("ApiPaths:CRMApiBaseAddress", exception.Message);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiPaths:CRMApiBaseAddress"] = "https://crm.example.test/",
                ["CrmClient:ClientId"] = "test-client",
                ["CrmClient:ClientSecret"] = "test-secret",
                ["CrmClient:RefreshBeforeExpirySeconds"] = "30"
            })
            .Build();
    }
}

