namespace Domain.Exceptions;

public class ConflictException : Exception
{
    public ConflictException() : base("Resource already exists")
    {
    }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConflictException(string name, object key)
        : base($"{name} with key ({key}) already exists.")
    {
    }
}