using System.Text.Json;
using Microsoft.Extensions.Logging;
using Template.Service.BusinessModel.Common;
using Template.Service.DataModel.Crm;

namespace Template.Service.Services.Infrastructure.Http;

public sealed class CrmResponseHandler : ICrmResponseHandler
{
    internal const string CrmApiError = "CRM_API_ERROR";
    private readonly ILogger<CrmResponseHandler> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CrmResponseHandler(ILogger<CrmResponseHandler> logger)
    {
        _logger = logger;
    }

    public async Task<CrmServiceResult<T>> HandleResultAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpResponse = await action(cancellationToken);
            var statusCode = (int)httpResponse.StatusCode;

            if (httpResponse.IsSuccessStatusCode)
            {
                try
                {
                    await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
                    var data = await JsonSerializer.DeserializeAsync<T>(
                        stream,
                        _serializerOptions,
                        cancellationToken);

                    if (data is null)
                    {
                        _logger.LogError(
                            "CRM returned an empty success response with status code {StatusCode}.",
                            statusCode);
                        return new CrmServiceResult<T>
                        {
                            StatusCode = statusCode,
                            TransportFailure = CrmTransportFailure.Deserialization
                        };
                    }

                    if (data is CrmApiResponse { Ok: false } envelope)
                    {
                        _logger.LogWarning(
                            "CRM returned a rejected success envelope with status code {StatusCode} and detail {CrmDetail}.",
                            statusCode,
                            envelope.Error);
                        return new CrmServiceResult<T>
                        {
                            StatusCode = statusCode,
                            Error = new CrmErrorResponse
                            {
                                Code = CrmApiError,
                                Detail = envelope.Error
                            }
                        };
                    }

                    return new CrmServiceResult<T>
                    {
                        Data = data,
                        StatusCode = statusCode
                    };
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Failed to deserialize the CRM success response.");
                    return new CrmServiceResult<T>
                    {
                        StatusCode = statusCode,
                        TransportFailure = CrmTransportFailure.Deserialization
                    };
                }
                catch (NotSupportedException exception)
                {
                    _logger.LogError(exception, "The CRM success response type is not supported for deserialization.");
                    return new CrmServiceResult<T>
                    {
                        StatusCode = statusCode,
                        TransportFailure = CrmTransportFailure.Deserialization
                    };
                }
            }

            var error = await DeserializeErrorAsync(httpResponse, cancellationToken);
            _logger.LogWarning(
                "CRM call returned status code {StatusCode} ({ReasonPhrase}), code {CrmCode}, and detail {CrmDetail}.",
                statusCode,
                httpResponse.ReasonPhrase,
                error?.Code,
                error?.Detail);

            return new CrmServiceResult<T>
            {
                StatusCode = statusCode,
                Error = error
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("CRM call was cancelled by the caller.");
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "CRM call timed out.");
            return new CrmServiceResult<T>
            {
                TransportFailure = CrmTransportFailure.Timeout
            };
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "CRM call failed.");
            return new CrmServiceResult<T>
            {
                TransportFailure = CrmTransportFailure.Unavailable
            };
        }
    }

    public async Task<Response<T>> HandleAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default)
        where T : CrmApiResponse
    {
        var response = new Response<T>();

        try
        {
            using var httpResponse = await action(cancellationToken);
            var statusCode = (int)httpResponse.StatusCode;

            try
            {
                await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
                response.Data = await JsonSerializer.DeserializeAsync<T>(
                    stream,
                    _serializerOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "Failed to deserialize the CRM response.");
                response.Messages.Add(CreateError(
                    HttpResponseMessageCodes.DeserializationError,
                    "CRM response could not be processed."));
            }
            catch (NotSupportedException exception)
            {
                _logger.LogError(exception, "The CRM response type is not supported for deserialization.");
                response.Messages.Add(CreateError(
                    HttpResponseMessageCodes.DeserializationError,
                    "CRM response could not be processed."));
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CRM call returned status code {StatusCode} ({ReasonPhrase}) and error {CrmError}.",
                    statusCode,
                    httpResponse.ReasonPhrase,
                    response.Data?.Error);
                response.Messages.Add(CreateError(
                    $"HTTP_{statusCode}",
                    $"CRM returned HTTP status {statusCode}."));
            }

            if (response.Data is null && response.Messages.Count == 0)
            {
                response.Messages.Add(CreateError(
                    HttpResponseMessageCodes.DeserializationError,
                    "CRM returned an empty or invalid response."));
            }
            else if (response.Data is { Ok: false })
            {
                response.Messages.Add(CreateError(
                    CrmApiError,
                    "CRM rejected the request."));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("CRM call was cancelled by the caller.");
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "CRM call timed out.");
            response.Messages.Add(CreateError(
                HttpResponseMessageCodes.ServiceTimeout,
                "CRM request timed out."));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "CRM call failed.");
            response.Messages.Add(CreateError(
                HttpResponseMessageCodes.ServiceUnavailable,
                "CRM is currently unavailable."));
        }

        return response;
    }

    private static Message CreateError(string code, string text) => new()
    {
        Type = MessageType.Error,
        Code = code,
        Text = text
    };

    private async Task<CrmErrorResponse?> DeserializeErrorAsync(
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            if (stream.CanSeek && stream.Length == 0)
            {
                return null;
            }

            return await JsonSerializer.DeserializeAsync<CrmErrorResponse>(
                stream,
                _serializerOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Failed to deserialize the CRM error response.");
            return null;
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "The CRM error response type is not supported for deserialization.");
            return null;
        }
    }
}

