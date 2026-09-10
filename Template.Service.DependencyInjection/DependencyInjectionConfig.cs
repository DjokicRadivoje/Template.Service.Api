using Autofac;
using Framework.Logger.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Template.Service.BusinessLogic;
using Template.Service.BusinessLogic.Interfaces;
using Template.Service.Services;
using Template.Service.Services.Infrastructure.Authentication;
using Template.Service.Services.Infrastructure.Http;
using Template.Service.Services.Interfaces;

namespace Template.Service.DependencyInjection;

public static class DependencyInjectionConfig
{
    private const string CRMApiBaseAddressKey =
        "ApiPaths:CRMApiBaseAddress";

    private const int HttpClientTimeoutSeconds = 30;
    private const int MaximumRefreshBeforeExpirySeconds = 300;

    public static void ConfigureHttpClients(
        IServiceCollection services,
        IConfiguration configuration,
        bool allowInsecureCrm = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var crmClientOptions = CreateCrmClientOptions(configuration);
        services.TryAddSingleton(crmClientOptions);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<ICrmAccessTokenProvider, CrmAccessTokenProvider>();
        services.TryAddTransient<CrmBearerTokenHandler>();

        var crmBaseAddress = GetRequiredAbsoluteUri(
            configuration,
            CRMApiBaseAddressKey,
            allowHttp: allowInsecureCrm);

        services
            .AddHttpClient(
                HttpClientNames.CRMToken,
                httpClient =>
                {
                    httpClient.BaseAddress = crmBaseAddress;
                    httpClient.Timeout = TimeSpan.FromSeconds(
                        HttpClientTimeoutSeconds);
                })
            .RedactLoggedHeaders(["Authorization"]);

        services
            .AddHttpClient(
                HttpClientNames.CRMApi,
                httpClient =>
                {
                    httpClient.BaseAddress = crmBaseAddress;
                    httpClient.Timeout = TimeSpan.FromSeconds(
                        HttpClientTimeoutSeconds);
                })
            .RedactLoggedHeaders(["Authorization"])
            .AddHttpMessageHandler<CrmBearerTokenHandler>()
            .AddCorrelationIdHandler();
    }

    public static void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterType<CrmBusinessLogic>().As<ICrmBusinessLogic>();
        builder.RegisterType<CrmService>().As<ICrmService>();
        builder.RegisterType<OrganizationBusinessLogic>().As<IOrganizationBusinessLogic>();
        builder.RegisterType<HttpResponseHandler>().As<IHttpResponseHandler>();
        builder.RegisterType<CrmResponseHandler>().As<ICrmResponseHandler>();
    }

    private static CrmClientOptions CreateCrmClientOptions(
        IConfiguration configuration)
    {
        var sectionName = CrmClientOptions.SectionName;
        var clientId = configuration[$"{sectionName}:ClientId"];
        var clientSecret = configuration[$"{sectionName}:ClientSecret"];
        var refreshValue = configuration[$"{sectionName}:RefreshBeforeExpirySeconds"];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            //throw new InvalidOperationException(
            //    $"Configuration value '{sectionName}:ClientId' is required.");
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            //throw new InvalidOperationException(
            //    $"Configuration value '{sectionName}:ClientSecret' is required and must come from a secret provider.");
        }

        var refreshBeforeExpirySeconds =
            CrmClientOptions.DefaultRefreshBeforeExpirySeconds;
        if (refreshValue is not null
            && (!int.TryParse(refreshValue, out refreshBeforeExpirySeconds)
                || refreshBeforeExpirySeconds is < 0 or > MaximumRefreshBeforeExpirySeconds))
        {
            throw new InvalidOperationException(
                $"Configuration value '{sectionName}:RefreshBeforeExpirySeconds' must be between 0 and {MaximumRefreshBeforeExpirySeconds}.");
        }

        return new CrmClientOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            RefreshBeforeExpirySeconds = refreshBeforeExpirySeconds
        };
    }

    private static Uri GetRequiredAbsoluteUri(
        IConfiguration configuration,
        string key,
        bool allowHttp)
    {
        var configuredValue = configuration[key];
        if (!Uri.TryCreate(configuredValue, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !(allowHttp && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' must be a valid absolute {(allowHttp ? "HTTP or HTTPS" : "HTTPS")} URI.");
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{uri.AbsoluteUri}/", UriKind.Absolute);
    }
}

