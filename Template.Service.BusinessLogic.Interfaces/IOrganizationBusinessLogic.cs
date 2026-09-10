using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Organizations;

namespace Template.Service.BusinessLogic.Interfaces;

public interface IOrganizationBusinessLogic
{
    Task<Response<PagedResponse<OrganizationResponse>>> GetOrganizations(
        OrganizationSearchRequest request,
        CancellationToken cancellationToken = default);
}

