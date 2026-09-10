using Template.Service.BusinessModel.Common;

namespace Template.Service.BusinessLogic;

internal static class BusinessLogicResponseHelper
{
    #region Public methods

    public static bool ApplyServiceFailure<TService, TBusiness>(
        Response<TService> serviceResponse,
        Response<TBusiness> businessResponse,
        string failureCode,
        string failureText,
        string? validationCode = null,
        string? notFoundCode = null,
        string? conflictCode = null,
        string? unprocessableCode = null)
    {
        businessResponse.Messages.AddRange(serviceResponse.Messages);
        if (serviceResponse.Success && serviceResponse.Data is not null)
        {
            return false;
        }

        var code = failureCode;
        if (HasHttpCode(serviceResponse, 400))
        {
            businessResponse.Status = ResponseStatus.BadRequest;
            code = validationCode ?? failureCode;
        }
        else if (HasHttpCode(serviceResponse, 404))
        {
            businessResponse.Status = ResponseStatus.NotFound;
            code = notFoundCode ?? failureCode;
        }
        else if (HasHttpCode(serviceResponse, 409))
        {
            businessResponse.Status = ResponseStatus.Conflict;
            code = conflictCode ?? failureCode;
        }
        else if (HasHttpCode(serviceResponse, 422))
        {
            businessResponse.Status = ResponseStatus.UnprocessableEntity;
            code = unprocessableCode ?? failureCode;
        }

        AddErrorIfMissing(businessResponse, code, failureText);
        return true;
    }

    public static void SetValidationError<T>(
        Response<T> response,
        string code,
        string text)
    {
        response.Status = ResponseStatus.BadRequest;
        response.Messages.Add(CreateError(code, text));
    }

    public static void SetUnexpectedError<T>(Response<T> response, string text)
    {
        response.Messages.Add(CreateError(
            ResponseMessageCodes.BusinessLogicError,
            text));
    }

    #endregion

    #region Private methods

    private static bool HasHttpCode<T>(Response<T> response, int statusCode)
    {
        return response.Messages.Any(message =>
            string.Equals(
                message.Code,
                $"HTTP_{statusCode}",
                StringComparison.Ordinal));
    }

    private static void AddErrorIfMissing<T>(
        Response<T> response,
        string code,
        string text)
    {
        if (response.Messages.Any(message =>
                string.Equals(message.Code, code, StringComparison.Ordinal)))
        {
            return;
        }

        response.Messages.Add(CreateError(code, text));
    }

    private static Message CreateError(string code, string text)
    {
        return new Message
        {
            Type = MessageType.Error,
            Code = code,
            Text = text
        };
    }

    #endregion
}

