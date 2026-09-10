using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Crm;

namespace Template.Service.BusinessLogic.Interfaces;

public interface ICrmBusinessLogic
{
    Task<Response<List<CrmResponse>>> GetMockData(
        CrmRequest request,
        CancellationToken cancellationToken = default);
}

