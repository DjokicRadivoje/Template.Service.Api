using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Template.Service.BusinessLogic;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Crm;
using Template.Service.BusinessModel.Organizations;
using Template.Service.Mapper;
using Template.Service.Services.Infrastructure.Http;
using Template.Service.Services.Interfaces;
using Xunit;

namespace Template.Service.Api.Tests.BusinessLogic;

public sealed class CrmBusinessLogicFlowTests
{
    private readonly IMapper _mapper = new MapperConfiguration(
        configuration => configuration.AddProfile<DefaultProfile>())
        .CreateMapper();

    [Fact]
    public async Task GetMockData_MapsRequestAndResponse()
    {
        Template.Service.DataModel.Crm.CrmRequest? capturedRequest = null;
        var service = new StubCrmService
        {
            GetMockDataHandler = request =>
            {
                capturedRequest = request;
                return new Response<List<Template.Service.DataModel.Crm.CrmResponse>>
                {
                    Data =
                    [
                        new Template.Service.DataModel.Crm.CrmResponse
                        {
                            Id = 42,
                            Name = "Example customer"
                        }
                    ]
                };
            }
        };
        var businessLogic = new CrmBusinessLogic(
            service,
            NullLogger<CrmBusinessLogic>.Instance,
            _mapper);

        var response = await businessLogic.GetMockData(
            new CrmRequest { CustomerId = 42 });

        Assert.True(response.Success);
        Assert.Equal(42, capturedRequest?.CustomerId);
        var customer = Assert.Single(response.Data!);
        Assert.Equal(42, customer.Id);
        Assert.Equal("Example customer", customer.Name);
    }

    [Fact]
    public async Task GetOrganizations_MapsBusinessRequestAndDataResponse()
    {
        Template.Service.DataModel.Organizations.OrganizationSearchCrmRequest? capturedRequest = null;
        var service = new StubCrmService
        {
            GetOrganizationsHandler = request =>
            {
                capturedRequest = request;
                return new CrmServiceResult<
                    Template.Service.DataModel.Organizations.OrganizationSearchCrmResponse>
                {
                    StatusCode = 200,
                    Data = new Template.Service.DataModel.Organizations.OrganizationSearchCrmResponse
                    {
                        Items =
                        [
                            new Template.Service.DataModel.Organizations.OrganizationCrmResponse
                            {
                                CrmAccountId = "account-1",
                                Name = "ABC Consulting",
                                RegistrationNumber = "1234567",
                                TaxNumber = "MK123456789",
                                City = "Skopje",
                                Country = "MK",
                                Status = "ACTIVE"
                            }
                        ],
                        Page = 1,
                        PageSize = 20,
                        Total = 1
                    }
                };
            }
        };
        var businessLogic = new OrganizationBusinessLogic(
            service,
            NullLogger<OrganizationBusinessLogic>.Instance);

        var response = await businessLogic.GetOrganizations(
            new OrganizationSearchRequest
            {
                Name = "ABC",
                Country = "MK",
                City = "Skopje",
                Status = "active",
                RegistrationNumber = "1234567",
                Page = 1,
                PageSize = 20
            });

        Assert.True(response.Success);
        Assert.Equal("ACTIVE", capturedRequest?.Status);
        var organization = Assert.Single(response.Data!.Items);
        Assert.Equal("account-1", organization.Id);
        Assert.Equal("MK123456789", organization.TaxNumber);
    }

    [Fact]
    public async Task GetOrganizations_UnsupportedStatusDoesNotCallCrm()
    {
        var service = new StubCrmService();
        var businessLogic = new OrganizationBusinessLogic(
            service,
            NullLogger<OrganizationBusinessLogic>.Instance);

        var response = await businessLogic.GetOrganizations(
            new OrganizationSearchRequest { Status = "INACTIVE" });

        Assert.False(response.Success);
        Assert.Equal(ResponseStatus.BadRequest, response.Status);
        Assert.Contains(
            response.Messages,
            message => message.Code == ResponseMessageCodes.OrganizationSearchInvalid);
        Assert.Equal(0, service.OrganizationCallCount);
    }

    [Fact]
    public async Task GetOrganizations_CrmValidationErrorMapsSafePublicError()
    {
        var service = new StubCrmService
        {
            GetOrganizationsHandler = _ => new CrmServiceResult<
                Template.Service.DataModel.Organizations.OrganizationSearchCrmResponse>
            {
                StatusCode = 422,
                Error = new Template.Service.DataModel.Crm.CrmErrorResponse
                {
                    Code = "VALIDATION_ERROR",
                    Detail = "invalid_page_size"
                }
            }
        };
        var businessLogic = new OrganizationBusinessLogic(
            service,
            NullLogger<OrganizationBusinessLogic>.Instance);

        var response = await businessLogic.GetOrganizations(
            new OrganizationSearchRequest());

        Assert.False(response.Success);
        Assert.Equal(ResponseStatus.UnprocessableEntity, response.Status);
        Assert.Contains(
            response.Messages,
            message => message.Code == ResponseMessageCodes.OrganizationSearchInvalid);
        Assert.DoesNotContain(
            response.Messages,
            message => message.Text.Contains(
                "invalid_page_size",
                StringComparison.Ordinal));
    }

    private sealed class StubCrmService : ICrmService
    {
        public Func<Template.Service.DataModel.Crm.CrmRequest,
            Response<List<Template.Service.DataModel.Crm.CrmResponse>>>? GetMockDataHandler { get; init; }

        public Func<Template.Service.DataModel.Organizations.OrganizationSearchCrmRequest,
            CrmServiceResult<Template.Service.DataModel.Organizations.OrganizationSearchCrmResponse>>?
            GetOrganizationsHandler { get; init; }

        public int OrganizationCallCount { get; private set; }

        public Task<Response<List<Template.Service.DataModel.Crm.CrmResponse>>> GetMockData(
            Template.Service.DataModel.Crm.CrmRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                GetMockDataHandler?.Invoke(request)
                ?? new Response<List<Template.Service.DataModel.Crm.CrmResponse>>());
        }

        public Task<CrmServiceResult<Template.Service.DataModel.Organizations.OrganizationSearchCrmResponse>>
            GetOrganizations(
                Template.Service.DataModel.Organizations.OrganizationSearchCrmRequest request,
                CancellationToken cancellationToken = default)
        {
            OrganizationCallCount++;
            return Task.FromResult(
                GetOrganizationsHandler?.Invoke(request)
                ?? new CrmServiceResult<
                    Template.Service.DataModel.Organizations.OrganizationSearchCrmResponse>());
        }
    }
}

