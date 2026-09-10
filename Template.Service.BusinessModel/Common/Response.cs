using System.Text.Json.Serialization;

namespace Template.Service.BusinessModel.Common
{
    public class Response<T> : IResponse
    {
        public T? Data { get; set; }

        public List<Message> Messages { get; set; } = new();

        [JsonIgnore]
        public ResponseStatus Status { get; set; }

        public bool Success =>
            !Messages.Any(x => x.Type == MessageType.Error);

        public bool HasWarnings =>
            Messages.Any(x => x.Type == MessageType.Warning);

        IReadOnlyCollection<Message> IResponse.Messages => Messages;
    }
}

