using Template.Service.BusinessModel.Common;
using Template.Service.DataModel.Crm;
using Template.Service.DataModel.Organizations;
using Template.Service.Services.Infrastructure.Http;

namespace Template.Service.Services.Interfaces;

public interface ICrmService
{
    Task<Response<List<CrmResponse>>> GetMockData(
        CrmRequest request,
        CancellationToken cancellationToken = default);

    Task<CrmServiceResult<OrganizationSearchCrmResponse>> GetOrganizations(
        OrganizationSearchCrmRequest request,
        CancellationToken cancellationToken = default);
}

