using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Framework.Logger.Correlation;

public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CorrelationIdHandler> _logger;

    public CorrelationIdHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CorrelationIdHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = ResolveCorrelationId(request);

        request.Headers.Remove(CorrelationIdConstants.HeaderName);
        request.Headers.TryAddWithoutValidation(
            CorrelationIdConstants.HeaderName,
            correlationId);

        return base.SendAsync(request, cancellationToken);
    }

    private string ResolveCorrelationId(HttpRequestMessage request)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            if (TryNormalize(httpContext.TraceIdentifier, out var contextCorrelationId))
            {
                return contextCorrelationId;
            }

            var generatedCorrelationId = CreateCorrelationId();
            httpContext.TraceIdentifier = generatedCorrelationId;
            httpContext.Request.Headers[CorrelationIdConstants.HeaderName] = generatedCorrelationId;
            httpContext.Response.Headers[CorrelationIdConstants.HeaderName] = generatedCorrelationId;

            _logger.LogWarning(
                "The active HTTP context contained an invalid correlation ID. A new correlation ID {CorrelationId} was generated.",
                generatedCorrelationId);

            return generatedCorrelationId;
        }

        if (request.Headers.TryGetValues(
                CorrelationIdConstants.HeaderName,
                out var headerValues))
        {
            var values = headerValues.ToArray();
            if (values.Length == 1 && TryNormalize(values[0], out var requestCorrelationId))
            {
                return requestCorrelationId;
            }
        }

        var fallbackCorrelationId = CreateCorrelationId();

        _logger.LogWarning(
            "No active HTTP correlation context or valid explicit {HeaderName} header was available. A new correlation ID {CorrelationId} was generated for the outgoing request.",
            CorrelationIdConstants.HeaderName,
            fallbackCorrelationId);

        return fallbackCorrelationId;
    }

    private static bool TryNormalize(string? value, out string correlationId)
    {
        if (Guid.TryParseExact(
                value,
                CorrelationIdConstants.GuidFormat,
                out var parsedCorrelationId))
        {
            correlationId = parsedCorrelationId.ToString(CorrelationIdConstants.GuidFormat);
            return true;
        }

        correlationId = string.Empty;
        return false;
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString(CorrelationIdConstants.GuidFormat);
    }
}
