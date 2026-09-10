using Framework.Logger.Correlation;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Template.Service.Api.Infrastructure.Telemetry;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Telemetry;

public sealed class ApplicationInsightsCorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsCanonicalCorrelationIdToRequestTelemetry()
    {
        var incomingCorrelationId =
            Guid.NewGuid().ToString("D").ToUpperInvariant();
        var context = CreateContext(incomingCorrelationId);
        var requestTelemetry = new RequestTelemetry();
        context.Features.Set(requestTelemetry);
        var nextWasCalled = false;
        var middleware = new ApplicationInsightsCorrelationIdMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextWasCalled);
        Assert.Equal(
            Guid.Parse(incomingCorrelationId).ToString("D"),
            requestTelemetry.Properties[
                CorrelationIdConstants.LoggingPropertyName]);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotReplaceExistingCorrelationId()
    {
        var existingCorrelationId = Guid.NewGuid().ToString("D");
        var context = CreateContext(Guid.NewGuid().ToString("D"));
        var requestTelemetry = new RequestTelemetry();
        requestTelemetry.Properties[
            CorrelationIdConstants.LoggingPropertyName] = existingCorrelationId;
        context.Features.Set(requestTelemetry);
        var middleware = new ApplicationInsightsCorrelationIdMiddleware(
            _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            existingCorrelationId,
            requestTelemetry.Properties[
                CorrelationIdConstants.LoggingPropertyName]);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotAddInvalidCorrelationId()
    {
        var context = CreateContext("not-a-correlation-guid");
        var requestTelemetry = new RequestTelemetry();
        context.Features.Set(requestTelemetry);
        var middleware = new ApplicationInsightsCorrelationIdMiddleware(
            _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.DoesNotContain(
            CorrelationIdConstants.LoggingPropertyName,
            requestTelemetry.Properties);
    }

    [Fact]
    public async Task InvokeAsync_ContinuesWhenRequestTelemetryIsUnavailable()
    {
        var context = CreateContext(Guid.NewGuid().ToString("D"));
        var nextWasCalled = false;
        var middleware = new ApplicationInsightsCorrelationIdMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextWasCalled);
    }

    private static DefaultHttpContext CreateContext(string traceIdentifier)
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier
        };
    }
}

