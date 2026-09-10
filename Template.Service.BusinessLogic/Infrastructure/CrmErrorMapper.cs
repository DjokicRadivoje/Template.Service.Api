using Template.Service.BusinessModel.Common;
using Template.Service.Services.Infrastructure.Http;

namespace Template.Service.BusinessLogic.Infrastructure;

internal static class CrmErrorMapper
{
    public static bool Apply<TService, TBusiness>(
        CrmServiceResult<TService> serviceResult,
        Response<TBusiness> businessResponse,
        string failureCode,
        string failureText,
        string? validationCode = null,
        string? notFoundCode = null,
        string? conflictCode = null,
        string? unprocessableCode = null)
    {
        if (serviceResult.Success)
        {
            return false;
        }

        var publicCode = failureCode;
        switch (serviceResult.StatusCode)
        {
            case 400:
                businessResponse.Status = ResponseStatus.BadRequest;
                publicCode = validationCode ?? failureCode;
                break;
            case 404:
                businessResponse.Status = ResponseStatus.NotFound;
                publicCode = notFoundCode ?? failureCode;
                break;
            case 409:
                businessResponse.Status = ResponseStatus.Conflict;
                publicCode = conflictCode ?? failureCode;
                break;
            case 422:
                businessResponse.Status = ResponseStatus.UnprocessableEntity;
                publicCode = ResolveUnprocessableCode(
                    serviceResult,
                    failureCode,
                    validationCode,
                    notFoundCode,
                    unprocessableCode);
                break;
        }

        businessResponse.Messages.Add(new Message
        {
            Type = MessageType.Error,
            Code = publicCode,
            Text = failureText
        });
        return true;
    }

    private static string ResolveUnprocessableCode<T>(
        CrmServiceResult<T> serviceResult,
        string failureCode,
        string? validationCode,
        string? notFoundCode,
        string? unprocessableCode)
    {
        if (string.Equals(
                serviceResult.Error?.Code,
                "VALIDATION_ERROR",
                StringComparison.OrdinalIgnoreCase))
        {
            return validationCode ?? unprocessableCode ?? failureCode;
        }

        if (string.Equals(
                serviceResult.Error?.Code,
                "NOT_FOUND",
                StringComparison.OrdinalIgnoreCase))
        {
            return notFoundCode ?? unprocessableCode ?? failureCode;
        }

        return unprocessableCode ?? failureCode;
    }
}

