namespace Template.Service.BusinessModel.Common
{
    public class Message
    {
        public MessageType Type { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }
}

