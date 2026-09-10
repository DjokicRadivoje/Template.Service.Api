using Microsoft.AspNetCore.Http;
using Template.Service.Api.Infrastructure.Http;
using Template.Service.BusinessModel.Common;
using Template.Service.Services.Infrastructure.Http;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Http;

public sealed class ResponseHttpStatusCodeResolverTests
{
    private readonly ResponseHttpStatusCodeResolver _resolver = new();

    [Fact]
    public void Resolve_Success_ReturnsOk()
    {
        var response = new Response<object>();

        var statusCode = _resolver.Resolve(response);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Fact]
    public void Resolve_BusinessLogicError_ReturnsInternalServerError()
    {
        var response = CreateErrorResponse(ResponseMessageCodes.BusinessLogicError);

        var statusCode = _resolver.Resolve(response);

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    [Fact]
    public void Resolve_ServiceTimeout_ReturnsGatewayTimeout()
    {
        var response = CreateErrorResponse(HttpResponseMessageCodes.ServiceTimeout);

        var statusCode = _resolver.Resolve(response);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, statusCode);
    }

    [Fact]
    public void Resolve_OtherError_ReturnsBadGateway()
    {
        var response = CreateErrorResponse("OTHER_ERROR");

        var statusCode = _resolver.Resolve(response);

        Assert.Equal(StatusCodes.Status502BadGateway, statusCode);
    }

    [Fact]
    public void Resolve_BusinessLogicErrorHasPriorityOverTimeout()
    {
        var response = CreateErrorResponse(
            HttpResponseMessageCodes.ServiceTimeout,
            ResponseMessageCodes.BusinessLogicError);

        var statusCode = _resolver.Resolve(response);

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    [Theory]
    [InlineData(ResponseStatus.Created, StatusCodes.Status201Created)]
    [InlineData(ResponseStatus.BadRequest, StatusCodes.Status400BadRequest)]
    [InlineData(ResponseStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResponseStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ResponseStatus.UnprocessableEntity, StatusCodes.Status422UnprocessableEntity)]
    public void Resolve_ExplicitResponseStatus_ReturnsMappedHttpStatus(
        ResponseStatus responseStatus,
        int expectedStatusCode)
    {
        var response = CreateErrorResponse(ResponseMessageCodes.BusinessLogicError);
        response.Status = responseStatus;

        var statusCode = _resolver.Resolve(response);

        Assert.Equal(expectedStatusCode, statusCode);
    }

    private static Response<object> CreateErrorResponse(params string[] codes)
    {
        return new Response<object>
        {
            Messages = codes.Select(code => new Message
            {
                Type = MessageType.Error,
                Code = code,
                Text = "Test error"
            }).ToList()
        };
    }
}

