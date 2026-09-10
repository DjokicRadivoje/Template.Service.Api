using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Framework.Logger.Correlation;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var hasIncomingHeader = context.Request.Headers.TryGetValue(
            CorrelationIdConstants.HeaderName,
            out var headerValues);

        var parsedCorrelationId = Guid.Empty;
        var hasValidIncomingId = hasIncomingHeader
            && headerValues.Count == 1
            && Guid.TryParseExact(
                headerValues[0],
                CorrelationIdConstants.GuidFormat,
                out parsedCorrelationId);

        var correlationId = hasValidIncomingId
            ? parsedCorrelationId.ToString(CorrelationIdConstants.GuidFormat)
            : Guid.NewGuid().ToString(CorrelationIdConstants.GuidFormat);

        context.TraceIdentifier = correlationId;
        context.Request.Headers[CorrelationIdConstants.HeaderName] = correlationId;
        context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;

        Activity.Current?.SetBaggage(
            CorrelationIdConstants.LoggingPropertyName,
            correlationId);
        Activity.Current?.SetTag(
            CorrelationIdConstants.LoggingPropertyName,
            correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdConstants.LoggingPropertyName] = correlationId
        });

        if (hasIncomingHeader && !hasValidIncomingId)
        {
            logger.LogWarning(
                "Invalid or multiple {HeaderName} header received. A new correlation ID was generated.",
                CorrelationIdConstants.HeaderName);
        }

        await _next(context);
    }
}
