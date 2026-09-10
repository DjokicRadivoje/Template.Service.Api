using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Template.Service.BusinessModel.Common;

namespace Template.Service.Api.Infrastructure.Http;

public sealed class ResponseHttpStatusFilter : IResultFilter
{
    private readonly IResponseHttpStatusCodeResolver _statusCodeResolver;

    public ResponseHttpStatusFilter(
        IResponseHttpStatusCodeResolver statusCodeResolver)
    {
        _statusCodeResolver = statusCodeResolver;
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: IResponse response } objectResult)
        {
            return;
        }

        var currentStatusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
        if (currentStatusCode != StatusCodes.Status200OK)
        {
            return;
        }

        objectResult.StatusCode = _statusCodeResolver.Resolve(response);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}

