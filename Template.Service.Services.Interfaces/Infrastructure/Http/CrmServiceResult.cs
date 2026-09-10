using Template.Service.DataModel.Crm;

namespace Template.Service.Services.Infrastructure.Http;

public sealed class CrmServiceResult<T>
{
    public T? Data { get; init; }

    public int? StatusCode { get; init; }

    public CrmErrorResponse? Error { get; init; }

    public CrmTransportFailure TransportFailure { get; init; }

    public bool Success =>
        TransportFailure == CrmTransportFailure.None
        && StatusCode is >= 200 and <= 299
        && Error is null
        && Data is not null;
}

public enum CrmTransportFailure
{
    None,
    Timeout,
    Unavailable,
    Deserialization
}

