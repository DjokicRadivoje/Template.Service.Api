using System.Text.Json;
using Microsoft.Extensions.Logging;
using Template.Service.BusinessModel.Common;

namespace Template.Service.Services.Infrastructure.Http
{
    public class HttpResponseHandler : IHttpResponseHandler
    {
        private readonly ILogger<HttpResponseHandler> _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public HttpResponseHandler(ILogger<HttpResponseHandler> logger)
        {
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<Response<T>> HandleAsync<T>(
            Func<CancellationToken, Task<HttpResponseMessage>> action,
            CancellationToken cancellationToken = default)
        {
            var response = new Response<T>();

            try
            {
                using var httpResponse = await action(cancellationToken);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var statusCode = (int)httpResponse.StatusCode;

                    _logger.LogWarning(
                        "External HTTP call returned status code {StatusCode} ({ReasonPhrase}).",
                        statusCode,
                        httpResponse.ReasonPhrase);

                    response.Messages.Add(CreateError(
                        $"HTTP_{statusCode}",
                        $"External service returned HTTP status {statusCode}."));

                    return response;
                }

                try
                {
                    await using var contentStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
                    response.Data = await JsonSerializer.DeserializeAsync<T>(
                        contentStream,
                        _serializerOptions,
                        cancellationToken);

                    if (response.Data is null)
                    {
                        response.Messages.Add(CreateError(
                            HttpResponseMessageCodes.DeserializationError,
                            "External service returned an empty or invalid response."));
                    }
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Failed to deserialize the external service response.");
                    response.Messages.Add(CreateError(
                        HttpResponseMessageCodes.DeserializationError,
                        "External service response could not be processed."));
                }
                catch (NotSupportedException exception)
                {
                    _logger.LogError(exception, "The external service response type is not supported for deserialization.");
                    response.Messages.Add(CreateError(
                        HttpResponseMessageCodes.DeserializationError,
                        "External service response could not be processed."));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("External HTTP call was cancelled by the caller.");
                throw;
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(exception, "External HTTP call timed out.");
                response.Messages.Add(CreateError(
                    HttpResponseMessageCodes.ServiceTimeout,
                    "External service request timed out."));
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "External HTTP call failed.");
                response.Messages.Add(CreateError(
                    HttpResponseMessageCodes.ServiceUnavailable,
                    "External service is currently unavailable."));
            }

            return response;
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
    }
}

