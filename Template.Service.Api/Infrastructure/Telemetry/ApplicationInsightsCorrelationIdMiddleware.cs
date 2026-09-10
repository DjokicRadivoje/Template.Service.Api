using Framework.Logger.Correlation;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http.Features;

namespace Template.Service.Api.Infrastructure.Telemetry;

public sealed class ApplicationInsightsCorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public ApplicationInsightsCorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (Guid.TryParseExact(
                context.TraceIdentifier,
                CorrelationIdConstants.GuidFormat,
                out var parsedCorrelationId))
        {
            var requestTelemetry =
                context.Features.Get<RequestTelemetry>();

            requestTelemetry?.Properties.TryAdd(
                CorrelationIdConstants.LoggingPropertyName,
                parsedCorrelationId.ToString(
                    CorrelationIdConstants.GuidFormat));
        }

        await _next(context);
    }
}

public static class ApplicationInsightsCorrelationIdExtensions
{
    public static IApplicationBuilder UseApplicationInsightsCorrelationId(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApplicationInsightsCorrelationIdMiddleware>();
    }
}

