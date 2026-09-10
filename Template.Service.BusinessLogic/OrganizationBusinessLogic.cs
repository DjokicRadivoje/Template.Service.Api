using Microsoft.Extensions.Logging;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Organizations;
using Template.Service.BusinessLogic.Infrastructure;
using Template.Service.BusinessLogic.Interfaces;
using Template.Service.Services.Interfaces;

namespace Template.Service.BusinessLogic;

public sealed class OrganizationBusinessLogic : IOrganizationBusinessLogic
{
    private const int MinimumSearchLength = 2;
    private const int MaximumPageSize = 100;
    private const int MaximumUnpagedResultSize = 500;

    private readonly ICrmService _crmService;
    private readonly ILogger<OrganizationBusinessLogic> _logger;

    #region Public constructor

    public OrganizationBusinessLogic(
        ICrmService crmService,
        ILogger<OrganizationBusinessLogic> logger)
    {
        _crmService = crmService;
        _logger = logger;
    }

    #endregion

    #region Public methods

    public async Task<Response<PagedResponse<OrganizationResponse>>> GetOrganizations(
        OrganizationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new Response<PagedResponse<OrganizationResponse>>();
        var validationError = Validate(request);
        if (validationError is not null)
        {
            BusinessLogicResponseHelper.SetValidationError(
                response,
                ResponseMessageCodes.OrganizationSearchInvalid,
                validationError);
            return response;
        }

        try
        {
            var dataRequest = new DataModel.Organizations.OrganizationSearchCrmRequest
            {
                Name = request.Name,
                RegistrationNumber = request.RegistrationNumber,
                Country = request.Country,
                City = request.City,
                Status = string.IsNullOrWhiteSpace(request.Status)
                    ? null
                    : "ACTIVE",
                Page = request.Page,
                PageSize = request.PageSize
            };
            var serviceResponse = await _crmService.GetOrganizations(
                dataRequest,
                cancellationToken);

            if (CrmErrorMapper.Apply(
                    serviceResponse,
                    response,
                    ResponseMessageCodes.OrganizationRetrievalFailed,
                    "Organizations could not be retrieved.",
                    validationCode: ResponseMessageCodes.OrganizationSearchInvalid))
            {
                return response;
            }

            var crmResponse = serviceResponse.Data!;
            response.Data = new PagedResponse<OrganizationResponse>
            {
                Items = crmResponse.Items
                    .Select(item => new OrganizationResponse
                    {
                        Id = item.CrmAccountId,
                        Name = item.Name,
                        RegistrationNumber = item.RegistrationNumber,
                        TaxNumber = item.TaxNumber,
                        City = item.City,
                        Country = item.Country,
                        Status = item.Status
                    })
                    .ToList(),
                Page = crmResponse.Page,
                PageSize = crmResponse.PageSize,
                Total = crmResponse.Total
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Organization retrieval was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while retrieving organizations.");
            BusinessLogicResponseHelper.SetUnexpectedError(
                response,
                "An unexpected error occurred while retrieving organizations.");
        }

        return response;
    }

    #endregion

    #region Private methods

    private static string? Validate(OrganizationSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name)
            && request.Name.Trim().Length < MinimumSearchLength)
        {
            return $"Organization name must contain at least {MinimumSearchLength} characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !string.Equals(
                request.Status.Trim(),
                "ACTIVE",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Organization status must be ACTIVE when supplied.";
        }

        if (request.Page < 0)
        {
            return "Page cannot be negative.";
        }

        var maximumPageSize = request.Page == 0
            ? MaximumUnpagedResultSize
            : MaximumPageSize;
        if (request.PageSize is < 1 || request.PageSize > maximumPageSize)
        {
            return $"Page size must be between 1 and {maximumPageSize}.";
        }

        return null;
    }

    #endregion
}

