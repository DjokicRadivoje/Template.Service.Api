using System.Diagnostics;
using Framework.Logger.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Framework.Logger.Tests.Correlation;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_UsesAndNormalizesValidIncomingId()
    {
        var incomingId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var expectedId = Guid.Parse(incomingId).ToString("D");
        var context = CreateContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = incomingId;
        var logger = new TestLogger<CorrelationIdMiddleware>();
        string? observedTraceIdentifier = null;
        string? observedScopeId = null;

        var middleware = new CorrelationIdMiddleware(requestContext =>
        {
            observedTraceIdentifier = requestContext.TraceIdentifier;
            observedScopeId = logger.GetCurrentScopeValue(
                CorrelationIdConstants.LoggingPropertyName);
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, logger);

        Assert.Equal(expectedId, observedTraceIdentifier);
        Assert.Equal(expectedId, observedScopeId);
        Assert.Equal(expectedId, context.Request.Headers[CorrelationIdConstants.HeaderName]);
        Assert.Equal(expectedId, context.Response.Headers[CorrelationIdConstants.HeaderName]);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesIdWhenHeaderIsMissing()
    {
        var context = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(
            context,
            NullLogger<CorrelationIdMiddleware>.Instance);

        Assert.True(Guid.TryParseExact(context.TraceIdentifier, "D", out _));
        Assert.Equal(
            context.TraceIdentifier,
            context.Response.Headers[CorrelationIdConstants.HeaderName]);
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToCurrentActivity()
    {
        var context = CreateContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        using var activity = new Activity("incoming-request").Start();

        await middleware.InvokeAsync(
            context,
            NullLogger<CorrelationIdMiddleware>.Instance);

        Assert.Equal(
            context.TraceIdentifier,
            activity.GetTagItem(CorrelationIdConstants.LoggingPropertyName));
        Assert.Equal(
            context.TraceIdentifier,
            activity.GetBaggageItem(CorrelationIdConstants.LoggingPropertyName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("7f3a54c142054b8eae14702de765b383")]
    public async Task InvokeAsync_ReplacesInvalidHeaderAndLogsWarning(string invalidValue)
    {
        var context = CreateContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = invalidValue;
        var logger = new TestLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, logger);

        Assert.True(Guid.TryParseExact(context.TraceIdentifier, "D", out _));
        Assert.Equal(
            context.TraceIdentifier,
            context.Request.Headers[CorrelationIdConstants.HeaderName]);
        var warning = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning);
        if (!string.IsNullOrEmpty(invalidValue))
        {
            Assert.DoesNotContain(invalidValue, warning.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task InvokeAsync_ReplacesMultipleHeaderValues()
    {
        var context = CreateContext();
        context.Request.Headers.Append(
            CorrelationIdConstants.HeaderName,
            new[]
            {
                Guid.NewGuid().ToString("D"),
                Guid.NewGuid().ToString("D")
            });
        var logger = new TestLogger<CorrelationIdMiddleware>();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, logger);

        Assert.True(Guid.TryParseExact(context.TraceIdentifier, "D", out _));
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesDistinctIdsForParallelRequests()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var contexts = Enumerable.Range(0, 20)
            .Select(_ => CreateContext())
            .ToArray();

        await Task.WhenAll(contexts.Select(context => middleware.InvokeAsync(
            context,
            NullLogger<CorrelationIdMiddleware>.Instance)));

        Assert.Equal(
            contexts.Length,
            contexts.Select(context => context.TraceIdentifier).Distinct().Count());
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }
}
