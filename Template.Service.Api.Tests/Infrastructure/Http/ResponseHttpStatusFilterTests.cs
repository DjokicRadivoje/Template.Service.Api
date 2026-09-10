using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Template.Service.Api.Infrastructure.Http;
using Template.Service.BusinessModel.Common;
using Template.Service.Services.Infrastructure.Http;
using Xunit;

namespace Template.Service.Api.Tests.Infrastructure.Http;

public sealed class ResponseHttpStatusFilterTests
{
    private readonly ResponseHttpStatusFilter _filter = new(
        new ResponseHttpStatusCodeResolver());

    [Fact]
    public void OnResultExecuting_MapsStandardOkResponse()
    {
        var response = CreateErrorResponse(HttpResponseMessageCodes.ServiceTimeout);
        var result = new OkObjectResult(response);
        var context = CreateContext(result);

        _filter.OnResultExecuting(context);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, result.StatusCode);
        Assert.Same(response, result.Value);
    }

    [Fact]
    public void OnResultExecuting_DoesNotOverrideExplicitNonOkStatus()
    {
        var response = CreateErrorResponse(HttpResponseMessageCodes.ServiceTimeout);
        var result = new NotFoundObjectResult(response);
        var context = CreateContext(result);

        _filter.OnResultExecuting(context);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public void OnResultExecuting_DoesNotChangeNonStandardResponse()
    {
        var value = new { Message = "plain response" };
        var result = new OkObjectResult(value);
        var context = CreateContext(result);

        _filter.OnResultExecuting(context);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(value, result.Value);
    }

    [Fact]
    public void OnResultExecuting_MapsCreatedOutcome()
    {
        var response = new Response<object>
        {
            Status = ResponseStatus.Created,
            Data = new object()
        };
        var result = new OkObjectResult(response);
        var context = CreateContext(result);

        _filter.OnResultExecuting(context);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    private static ResultExecutingContext CreateContext(IActionResult result)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ResultExecutingContext(
            actionContext,
            [],
            result,
            new object());
    }

    private static Response<object> CreateErrorResponse(string code)
    {
        return new Response<object>
        {
            Messages =
            [
                new Message
                {
                    Type = MessageType.Error,
                    Code = code,
                    Text = "Test error"
                }
            ]
        };
    }
}

