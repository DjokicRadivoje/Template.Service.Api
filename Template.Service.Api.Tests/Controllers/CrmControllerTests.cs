using Microsoft.AspNetCore.Mvc;
using Template.Service.Api.Controllers;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Crm;
using Template.Service.BusinessLogic.Interfaces;
using Xunit;

namespace Template.Service.Api.Tests.Controllers;

public sealed class CrmControllerTests
{
    [Fact]
    public async Task GetMockData_ReturnsBusinessLogicResponseAsOkResult()
    {
        var expectedResponse = new Response<List<CrmResponse>>();
        var controller = new CrmController(
            new CrmBusinessLogicStub(expectedResponse));

        var actionResult = await controller.GetMockData(
            new CrmRequest(),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expectedResponse, okResult.Value);
    }

    private sealed class CrmBusinessLogicStub : ICrmBusinessLogic
    {
        private readonly Response<List<CrmResponse>> _response;

        public CrmBusinessLogicStub(Response<List<CrmResponse>> response)
        {
            _response = response;
        }

        public Task<Response<List<CrmResponse>>> GetMockData(
            CrmRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_response);
        }
    }
}

