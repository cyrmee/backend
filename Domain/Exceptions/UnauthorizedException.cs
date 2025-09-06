namespace Domain.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("You are not authorized to access this resource")
    {
    }

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}