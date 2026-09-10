using Template.Service.BusinessModel.Common;

namespace Template.Service.Api.Infrastructure.Http;

public interface IResponseHttpStatusCodeResolver
{
    int Resolve(IResponse response);
}

