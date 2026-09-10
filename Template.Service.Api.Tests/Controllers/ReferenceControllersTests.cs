using Microsoft.AspNetCore.Mvc;
using Template.Service.Api.Controllers;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Organizations;
using Template.Service.BusinessLogic.Interfaces;
using Xunit;

namespace Template.Service.Api.Tests.Controllers;

public sealed class ReferenceControllersTests
{
    [Fact]
    public async Task OrganizationsController_DelegatesSearchRequest()
    {
        var businessLogic = new OrganizationBusinessLogicStub();
        var controller = new OrganizationsController(businessLogic);
        var request = new OrganizationSearchRequest
        {
            Name = "ABC",
            City = "Skopje",
            Status = "ACTIVE"
        };

        var result = await controller.GetOrganizations(
            request,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<Response<PagedResponse<OrganizationResponse>>>(
            okResult.Value);
        var organization = Assert.Single(response.Data!.Items);
        Assert.Equal("account-1", organization.Id);
        Assert.Equal("ABC Consulting", organization.Name);
        Assert.Same(request, businessLogic.Request);
    }

    private sealed class OrganizationBusinessLogicStub : IOrganizationBusinessLogic
    {
        public OrganizationSearchRequest? Request { get; private set; }

        public Task<Response<PagedResponse<OrganizationResponse>>> GetOrganizations(
            OrganizationSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new Response<PagedResponse<OrganizationResponse>>
            {
                Data = new PagedResponse<OrganizationResponse>
                {
                    Items =
                    [
                        new OrganizationResponse
                        {
                            Id = "account-1",
                            Name = "ABC Consulting"
                        }
                    ],
                    Page = request.Page,
                    PageSize = request.PageSize,
                    Total = 1
                }
            });
        }
    }
}

