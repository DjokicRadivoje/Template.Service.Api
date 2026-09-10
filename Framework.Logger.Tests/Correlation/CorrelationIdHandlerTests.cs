using System.Net;
using Framework.Logger.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Logger.Tests.Correlation;

public sealed class CorrelationIdHandlerTests
{
    [Fact]
    public async Task ServiceRegistration_DefaultHttpClientUsesCorrelationHandler()
    {
        var contextId = Guid.NewGuid().ToString("D");
        var terminalHandler = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorrelationId();
        services
            .AddHttpClient(string.Empty)
            .ConfigurePrimaryHttpMessageHandler(() => terminalHandler)
            .AddCorrelationIdHandler();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { TraceIdentifier = contextId };
        using var client = provider.GetRequiredService<HttpClient>();

        using var response = await client.GetAsync("https://example.test/");

        Assert.Equal(contextId, terminalHandler.CorrelationId);
    }

    [Fact]
    public async Task SendAsync_ActiveHttpContextOverridesExistingHeader()
    {
        var contextId = Guid.NewGuid().ToString("D");
        var context = new DefaultHttpContext
        {
            TraceIdentifier = contextId
        };
        var terminalHandler = new CapturingHandler();
        var handler = CreateHandler(context, terminalHandler, out var logger);
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdConstants.HeaderName,
            Guid.NewGuid().ToString("D"));
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        using var response = await client.SendAsync(request);

        Assert.Equal(contextId, terminalHandler.CorrelationId);
        Assert.Equal(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            terminalHandler.TraceParent);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SendAsync_WithoutHttpContextPreservesValidExplicitHeader()
    {
        var explicitId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var expectedId = Guid.Parse(explicitId).ToString("D");
        var terminalHandler = new CapturingHandler();
        var handler = CreateHandler(null, terminalHandler, out var logger);
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdConstants.HeaderName,
            explicitId);

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedId, terminalHandler.CorrelationId);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-id")]
    public async Task SendAsync_WithoutHttpContextGeneratesFallbackAndLogsWarning(
        string? explicitValue)
    {
        var terminalHandler = new CapturingHandler();
        var handler = CreateHandler(null, terminalHandler, out var logger);
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

        if (explicitValue is not null)
        {
            request.Headers.TryAddWithoutValidation(
                CorrelationIdConstants.HeaderName,
                explicitValue);
        }

        using var response = await client.SendAsync(request);

        Assert.True(Guid.TryParseExact(terminalHandler.CorrelationId, "D", out _));
        var warning = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning);
        if (explicitValue is not null)
        {
            Assert.DoesNotContain(explicitValue, warning.Message, StringComparison.Ordinal);
        }
    }

    private static CorrelationIdHandler CreateHandler(
        HttpContext? context,
        HttpMessageHandler terminalHandler,
        out TestLogger<CorrelationIdHandler> logger)
    {
        logger = new TestLogger<CorrelationIdHandler>();
        return new CorrelationIdHandler(
            new HttpContextAccessor { HttpContext = context },
            logger)
        {
            InnerHandler = terminalHandler
        };
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CorrelationId { get; private set; }
        public string? TraceParent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CorrelationId = request.Headers.GetValues(
                CorrelationIdConstants.HeaderName).Single();
            TraceParent = request.Headers.TryGetValues("traceparent", out var values)
                ? values.Single()
                : null;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
