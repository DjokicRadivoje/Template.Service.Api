using System.Diagnostics;
using Framework.Logger.Correlation;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Template.Service.Api.Infrastructure.Telemetry;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Telemetry;

public sealed class CorrelationIdTelemetryInitializerTests
{
    [Fact]
    public void Initialize_UsesAndNormalizesActiveHttpContextCorrelationId()
    {
        var incomingCorrelationId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = incomingCorrelationId
        };
        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };
        using var activity = new Activity("incoming-request").Start();
        activity.SetBaggage(
            CorrelationIdConstants.LoggingPropertyName,
            Guid.NewGuid().ToString("D"));
        var telemetry = new RequestTelemetry();
        var initializer = new CorrelationIdTelemetryInitializer(accessor);

        initializer.Initialize(telemetry);

        Assert.Equal(
            Guid.Parse(incomingCorrelationId).ToString("D"),
            telemetry.Properties[CorrelationIdConstants.LoggingPropertyName]);
    }

    [Fact]
    public void Initialize_AddsCorrelationIdFromCurrentActivityBaggageWhenContextIsUnavailable()
    {
        var correlationId = Guid.NewGuid().ToString("D");
        using var activity = new Activity("incoming-request").Start();
        activity.SetBaggage(CorrelationIdConstants.LoggingPropertyName, correlationId);
        var telemetry = new RequestTelemetry();
        var initializer = new CorrelationIdTelemetryInitializer(
            new HttpContextAccessor());

        initializer.Initialize(telemetry);

        Assert.Equal(
            correlationId,
            telemetry.Properties[CorrelationIdConstants.LoggingPropertyName]);
    }

    [Fact]
    public void Initialize_DoesNotReplaceExistingCorrelationId()
    {
        var existingCorrelationId = Guid.NewGuid().ToString("D");
        using var activity = new Activity("incoming-request").Start();
        activity.SetBaggage(
            CorrelationIdConstants.LoggingPropertyName,
            Guid.NewGuid().ToString("D"));
        var telemetry = new DependencyTelemetry();
        telemetry.Properties[CorrelationIdConstants.LoggingPropertyName] =
            existingCorrelationId;
        var initializer = new CorrelationIdTelemetryInitializer(
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = Guid.NewGuid().ToString("D")
                }
            });

        initializer.Initialize(telemetry);

        Assert.Equal(
            existingCorrelationId,
            telemetry.Properties[CorrelationIdConstants.LoggingPropertyName]);
    }

    [Fact]
    public void Initialize_DoesNotAddCorrelationIdWhenNoCanonicalValueExists()
    {
        var telemetry = new RequestTelemetry();
        var initializer = new CorrelationIdTelemetryInitializer(
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "not-a-correlation-guid"
                }
            });

        initializer.Initialize(telemetry);

        Assert.DoesNotContain(
            CorrelationIdConstants.LoggingPropertyName,
            telemetry.Properties);
    }
}

