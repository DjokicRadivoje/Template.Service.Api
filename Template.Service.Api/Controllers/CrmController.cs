using Microsoft.AspNetCore.Mvc;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Crm;
using Template.Service.BusinessLogic.Interfaces;

namespace Template.Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class CrmController : ControllerBase
{
    private readonly ICrmBusinessLogic _crmBusinessLogic;

    public CrmController(ICrmBusinessLogic crmBusinessLogic)
    {
        _crmBusinessLogic = crmBusinessLogic;
    }

    [HttpGet("mock", Name = "GetCrmMockData")]
    public async Task<ActionResult<Response<List<CrmResponse>>>> GetMockData(
        [FromQuery] CrmRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _crmBusinessLogic.GetMockData(
            request,
            cancellationToken));
    }
}

