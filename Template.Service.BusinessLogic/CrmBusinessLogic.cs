using AutoMapper;
using Microsoft.Extensions.Logging;
using Template.Service.BusinessModel.Common;
using Template.Service.BusinessModel.Crm;
using Template.Service.BusinessLogic.Interfaces;
using Template.Service.Services.Interfaces;

namespace Template.Service.BusinessLogic;

public sealed class CrmBusinessLogic : ICrmBusinessLogic
{
    private readonly ICrmService _crmService;
    private readonly ILogger<CrmBusinessLogic> _logger;
    private readonly IMapper _mapper;

    #region Public constructor

    public CrmBusinessLogic(
        ICrmService crmService,
        ILogger<CrmBusinessLogic> logger,
        IMapper mapper)
    {
        _crmService = crmService;
        _logger = logger;
        _mapper = mapper;
    }

    #endregion

    #region Public methods

    public async Task<Response<List<CrmResponse>>> GetMockData(
        CrmRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new Response<List<CrmResponse>>();

        try
        {
            var serviceRequest = _mapper.Map<DataModel.Crm.CrmRequest>(request);
            var serviceResponse = await _crmService.GetMockData(
                serviceRequest,
                cancellationToken);

            response.Messages.AddRange(serviceResponse.Messages);
            if (!serviceResponse.Success || serviceResponse.Data is null)
            {
                _logger.LogWarning(
                    "CRM mock request failed for customer ID {CustomerId}.",
                    request.CustomerId);
                return response;
            }

            response.Data = _mapper.Map<List<CrmResponse>>(serviceResponse.Data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "CRM mock request was cancelled for customer ID {CustomerId}.",
                request.CustomerId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while processing CRM mock request for customer ID {CustomerId}.",
                request.CustomerId);

            response.Messages.Add(new Message
            {
                Type = MessageType.Error,
                Code = ResponseMessageCodes.BusinessLogicError,
                Text = "An unexpected error occurred while processing the CRM mock request."
            });
        }

        return response;
    }

    #endregion

    #region Private methods

    #endregion
}

