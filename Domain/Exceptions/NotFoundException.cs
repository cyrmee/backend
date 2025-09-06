namespace Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException() : base("Resource was not found")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotFoundException(string name, object key)
        : base($"{name} with key ({key}) was not found.")
    {
    }
}