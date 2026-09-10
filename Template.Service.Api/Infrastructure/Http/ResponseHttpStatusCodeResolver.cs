using Template.Service.BusinessModel.Common;
using Template.Service.Services.Infrastructure.Http;

namespace Template.Service.Api.Infrastructure.Http;

public sealed class ResponseHttpStatusCodeResolver : IResponseHttpStatusCodeResolver
{
    public int Resolve(IResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var explicitStatusCode = response.Status switch
        {
            ResponseStatus.Created => StatusCodes.Status201Created,
            ResponseStatus.BadRequest => StatusCodes.Status400BadRequest,
            ResponseStatus.NotFound => StatusCodes.Status404NotFound,
            ResponseStatus.Conflict => StatusCodes.Status409Conflict,
            ResponseStatus.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
            _ => (int?)null
        };
        if (explicitStatusCode.HasValue)
        {
            return explicitStatusCode.Value;
        }

        if (response.Success)
        {
            return StatusCodes.Status200OK;
        }

        if (response.Messages.Any(message =>
                message.Code == ResponseMessageCodes.BusinessLogicError))
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (response.Messages.Any(message =>
                message.Code == HttpResponseMessageCodes.ServiceTimeout))
        {
            return StatusCodes.Status504GatewayTimeout;
        }

        return StatusCodes.Status502BadGateway;
    }
}

