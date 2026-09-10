namespace Template.Service.BusinessModel.Common;

public interface IResponse
{
    bool Success { get; }

    IReadOnlyCollection<Message> Messages { get; }

    ResponseStatus Status { get; }
}

