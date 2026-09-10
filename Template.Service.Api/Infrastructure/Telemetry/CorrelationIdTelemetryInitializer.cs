using System.Diagnostics;
using Framework.Logger.Correlation;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;

namespace Template.Service.Api.Infrastructure.Telemetry;

public sealed class CorrelationIdTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdTelemetryInitializer(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not ISupportProperties telemetryWithProperties
            || telemetryWithProperties.Properties.ContainsKey(
                CorrelationIdConstants.LoggingPropertyName))
        {
            return;
        }

        var correlationId = ResolveCorrelationId();
        if (correlationId is null)
        {
            return;
        }

        telemetryWithProperties.Properties.Add(
            CorrelationIdConstants.LoggingPropertyName,
            correlationId);
    }

    private string? ResolveCorrelationId()
    {
        if (TryNormalize(
                _httpContextAccessor.HttpContext?.TraceIdentifier,
                out var contextCorrelationId))
        {
            return contextCorrelationId;
        }

        return TryNormalize(
            Activity.Current?.GetBaggageItem(
                CorrelationIdConstants.LoggingPropertyName),
            out var activityCorrelationId)
            ? activityCorrelationId
            : null;
    }

    private static bool TryNormalize(
        string? value,
        out string correlationId)
    {
        if (Guid.TryParseExact(
                value,
                CorrelationIdConstants.GuidFormat,
                out var parsedCorrelationId))
        {
            correlationId = parsedCorrelationId.ToString(
                CorrelationIdConstants.GuidFormat);
            return true;
        }

        correlationId = string.Empty;
        return false;
    }
}

