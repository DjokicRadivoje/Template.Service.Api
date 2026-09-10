using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Template.Service.DataModel.Crm;
using Template.Service.DataModel.Organizations;
using Template.Service.Services;
using Template.Service.Services.Infrastructure.Http;
using Xunit;

namespace Template.Service.Api.Tests.Services;

public sealed class CrmServiceTests
{
    [Fact]
    public async Task GetMockData_UsesCrmClientAndExpectedPath()
    {
        using var handler = new StaticJsonHttpMessageHandler(
            """
            [
              { "id": 42, "name": "Example customer" }
            ]
            """);
        using var httpClient = CreateHttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var service = CreateService(factory);

        var response = await service.GetMockData(
            new CrmRequest { CustomerId = 42 });

        Assert.True(response.Success);
        Assert.Equal(HttpClientNames.CRMApi, Assert.Single(factory.RequestedNames));
        Assert.Equal("/api/mock/customers", handler.RequestUri?.AbsolutePath);
        Assert.Equal(42, Assert.Single(response.Data!).Id);
    }

    [Fact]
    public async Task GetOrganizations_BuildsQueryAndDeserializesResponse()
    {
        using var handler = new StaticJsonHttpMessageHandler(
            """
            {
              "items": [
                {
                  "crmAccountId": "account-1",
                  "name": "ABC Consulting",
                  "registrationNumber": "1234567",
                  "taxNumber": "MK123456789",
                  "city": "Skopje",
                  "country": "MK",
                  "status": "ACTIVE"
                }
              ],
              "page": 1,
              "pageSize": 20,
              "total": 1
            }
            """);
        using var httpClient = CreateHttpClient(handler);
        var service = CreateService(new StubHttpClientFactory(httpClient));

        var response = await service.GetOrganizations(
            new OrganizationSearchCrmRequest
            {
                Name = "ABC Consulting",
                Country = "MK",
                Page = 1,
                PageSize = 20
            });

        Assert.True(response.Success);
        Assert.Contains("name=ABC%20Consulting", handler.RequestUri?.Query);
        Assert.Contains("country=MK", handler.RequestUri?.Query);
        var organization = Assert.Single(response.Data!.Items);
        Assert.Equal("account-1", organization.CrmAccountId);
        Assert.Equal("MK123456789", organization.TaxNumber);
    }

    [Fact]
    public async Task GetOrganizations_DeserializesUnifiedCrmValidationError()
    {
        using var handler = new StaticJsonHttpMessageHandler(
            HttpStatusCode.UnprocessableEntity,
            """
            {
              "code": "VALIDATION_ERROR",
              "message": "Request validation failed",
              "correlationId": "example-correlation-id",
              "detail": "invalid_page_size"
            }
            """);
        using var httpClient = CreateHttpClient(handler);
        var service = CreateService(new StubHttpClientFactory(httpClient));

        var response = await service.GetOrganizations(
            new OrganizationSearchCrmRequest());

        Assert.False(response.Success);
        Assert.Equal(422, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", response.Error?.Code);
        Assert.Equal("invalid_page_size", response.Error?.Detail);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://crm.example.test/")
    };

    private static CrmService CreateService(IHttpClientFactory factory) => new(
        factory,
        new HttpResponseHandler(NullLogger<HttpResponseHandler>.Instance),
        new CrmResponseHandler(NullLogger<CrmResponseHandler>.Instance));

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public List<string> RequestedNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            RequestedNames.Add(name);
            return _httpClient;
        }
    }

    private sealed class StaticJsonHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _json;

        public StaticJsonHttpMessageHandler(string json)
            : this(HttpStatusCode.OK, json)
        {
        }

        public StaticJsonHttpMessageHandler(HttpStatusCode statusCode, string json)
        {
            _statusCode = statusCode;
            _json = json;
        }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    _json,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}

