namespace Nimbus.Application.Common.Exceptions;

public class DuplicateException : Exception
{
    public DuplicateException(string propertyName, string message)
        : base($"{propertyName}: {message}") {}
}
