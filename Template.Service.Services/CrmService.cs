using System.Globalization;
using Template.Service.BusinessModel.Common;
using Template.Service.DataModel.Crm;
using Template.Service.DataModel.Organizations;
using Template.Service.Services.Infrastructure.Http;
using Template.Service.Services.Interfaces;

namespace Template.Service.Services;

public sealed class CrmService : ICrmService
{
    internal const string MockCustomersPath = "api/mock/customers";
    internal const string OrganizationSearchPath = "V8/custom/accounts";

    private readonly HttpClient _httpClient;
    private readonly IHttpResponseHandler _httpResponseHandler;
    private readonly ICrmResponseHandler _crmResponseHandler;

    public CrmService(
        IHttpClientFactory httpClientFactory,
        IHttpResponseHandler httpResponseHandler,
        ICrmResponseHandler crmResponseHandler)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientNames.CRMApi);
        _httpResponseHandler = httpResponseHandler;
        _crmResponseHandler = crmResponseHandler;
    }

    public Task<Response<List<CrmResponse>>> GetMockData(
        CrmRequest request,
        CancellationToken cancellationToken = default)
    {
        return _httpResponseHandler.HandleAsync<List<CrmResponse>>(
            token => _httpClient.GetAsync(MockCustomersPath, token),
            cancellationToken);
    }

    public Task<CrmServiceResult<OrganizationSearchCrmResponse>> GetOrganizations(
        OrganizationSearchCrmRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = BuildQueryString(
            OrganizationSearchPath,
            new Dictionary<string, string?>
            {
                ["name"] = request.Name,
                ["country"] = request.Country,
                ["city"] = request.City,
                ["status"] = request.Status,
                ["registrationNumber"] = request.RegistrationNumber,
                ["page"] = request.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = request.PageSize.ToString(CultureInfo.InvariantCulture)
            });

        return _crmResponseHandler.HandleResultAsync<OrganizationSearchCrmResponse>(
            token => _httpClient.GetAsync(path, token),
            cancellationToken);
    }

    private static string BuildQueryString(
        string path,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

        return string.IsNullOrEmpty(query)
            ? path
            : $"{path}?{query}";
    }
}

