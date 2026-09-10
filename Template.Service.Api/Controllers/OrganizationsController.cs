using Microsoft.AspNetCore.Mvc;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Organizations;
using Template.Service.BusinessLogic.Interfaces;

namespace Template.Service.Api.Controllers;

[ApiController]
[Route("api/v1/organizations")]
public sealed class OrganizationsController : ControllerBase
{
    private readonly IOrganizationBusinessLogic _businessLogic;

    public OrganizationsController(IOrganizationBusinessLogic businessLogic)
    {
        _businessLogic = businessLogic;
    }

    /// <summary>RSM-146 — Search organizations available for onboarding</summary>
    /// <remarks>
    /// Returns normalized, channel-safe organization data sourced from CRM, which remains
    /// the master source of record. Supports optional name, registration-number, country,
    /// city, and status filters with standard pagination; a supplied name must contain at
    /// least two characters. Page size is limited to 100, while page 0 requests the full
    /// result set with a maximum of 500 items. The returned ID is the stable external
    /// organization identifier rather than CRM-specific terminology.
    /// </remarks>
    /// <param name="request">Optional organization filters and pagination values; page defaults to 1 and page size to 20.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">Matching organizations were returned in a paged response.</response>
    /// <response code="400">The search filters or pagination values are invalid.</response>
    [HttpGet(Name = "GetOrganizations")]
    [ProducesResponseType(typeof(Response<PagedResponse<OrganizationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<PagedResponse<OrganizationResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Response<PagedResponse<OrganizationResponse>>>> GetOrganizations(
        [FromQuery] OrganizationSearchRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _businessLogic.GetOrganizations(request, cancellationToken));
    }
}

