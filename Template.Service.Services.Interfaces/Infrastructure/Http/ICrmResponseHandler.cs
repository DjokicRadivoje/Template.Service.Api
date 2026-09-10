using Template.Service.BusinessModel.Common;
using Template.Service.DataModel.Crm;

namespace Template.Service.Services.Infrastructure.Http;

public interface ICrmResponseHandler
{
    Task<CrmServiceResult<T>> HandleResultAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default);

    Task<Response<T>> HandleAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default)
        where T : CrmApiResponse;
}

