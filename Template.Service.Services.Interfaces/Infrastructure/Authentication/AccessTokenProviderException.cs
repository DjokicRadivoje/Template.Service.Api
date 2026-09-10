namespace Template.Service.Services.Infrastructure.Authentication;

public sealed class AccessTokenProviderException : Exception
{
    public AccessTokenProviderException(string message)
        : base(message)
    {
    }

    public AccessTokenProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

