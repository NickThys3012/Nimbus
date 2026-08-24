namespace Nimbus.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) {}

    protected DomainException(string message, Exception innerException) : base(message, innerException) {}
}
