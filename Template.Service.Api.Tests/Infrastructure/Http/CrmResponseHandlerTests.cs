using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Template.Service.DataModel.Crm;
using Template.Service.Services.Infrastructure.Http;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Http;

public sealed class CrmResponseHandlerTests
{
    [Fact]
    public async Task HandleResultAsync_FlatSuccessResponse_DeserializesWithoutEnvelope()
    {
        var handler = CreateHandler();

        var result = await handler.HandleResultAsync<FlatResponse>(
            _ => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "id": "example-1",
                  "status": "UPDATED"
                }
                """)));

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("example-1", result.Data?.Id);
        Assert.Equal("UPDATED", result.Data?.Status);
    }

    [Fact]
    public async Task HandleResultAsync_UnifiedError_PreservesInternalErrorContract()
    {
        var handler = CreateHandler();

        var result = await handler.HandleResultAsync<FlatResponse>(
            _ => Task.FromResult(JsonResponse(
                HttpStatusCode.UnprocessableEntity,
                """
                {
                  "code": "VALIDATION_ERROR",
                  "message": "Request validation failed",
                  "correlationId": "example-correlation-id",
                  "detail": "invalid_request"
                }
                """)));

        Assert.False(result.Success);
        Assert.Equal(422, result.StatusCode);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Equal("Request validation failed", result.Error?.Message);
        Assert.Equal("invalid_request", result.Error?.Detail);
    }

    [Theory]
    [InlineData(400, "VALIDATION_ERROR")]
    [InlineData(401, "UNAUTHORIZED")]
    [InlineData(403, "FORBIDDEN")]
    [InlineData(404, "NOT_FOUND")]
    [InlineData(422, "BUSINESS_RULE")]
    [InlineData(500, "INTERNAL_ERROR")]
    public async Task HandleResultAsync_UnifiedError_PreservesStatusAndCanonicalCode(
        int statusCode,
        string errorCode)
    {
        var handler = CreateHandler();
        var json = $$"""
            {
              "code": "{{errorCode}}",
              "message": "CRM internal message",
              "detail": "internal_detail"
            }
            """;

        var result = await handler.HandleResultAsync<FlatResponse>(
            _ => Task.FromResult(JsonResponse((HttpStatusCode)statusCode, json)));

        Assert.False(result.Success);
        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(errorCode, result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_OkResponse_DeserializesCrmEnvelope()
    {
        var handler = CreateHandler();

        var response = await handler.HandleAsync<TestEnvelopeResponse>(
            _ => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "ok": true,
                  "id": "example-1",
                  "operation": "UNCHANGED"
                }
                """)));

        Assert.True(response.Success);
        Assert.True(response.Data?.Ok);
        Assert.Equal("example-1", response.Data?.Id);
        Assert.Equal("UNCHANGED", response.Data?.Operation);
    }

    [Fact]
    public async Task HandleAsync_CrmBusinessError_PreservesHttpStatusAndEnvelopeError()
    {
        var handler = CreateHandler();

        var response = await handler.HandleAsync<TestEnvelopeResponse>(
            _ => Task.FromResult(JsonResponse(
                HttpStatusCode.UnprocessableEntity,
                """
                { "ok": false, "error": "example_business_error" }
                """)));

        Assert.False(response.Success);
        Assert.False(response.Data?.Ok);
        Assert.Equal("example_business_error", response.Data?.Error);
        Assert.Contains(response.Messages, message => message.Code == "HTTP_422");
        Assert.Contains(response.Messages, message => message.Code == "CRM_API_ERROR");
    }

    private static CrmResponseHandler CreateHandler() =>
        new(NullLogger<CrmResponseHandler>.Instance);

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FlatResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }

    private sealed class TestEnvelopeResponse : CrmApiResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;
    }
}

