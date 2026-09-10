using Template.Service.BusinessModel.Common;

namespace Template.Service.Services.Infrastructure.Http
{
    public interface IHttpResponseHandler
    {
        Task<Response<T>> HandleAsync<T>(
            Func<CancellationToken, Task<HttpResponseMessage>> action,
            CancellationToken cancellationToken = default);
    }
}

