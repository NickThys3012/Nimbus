namespace Nimbus.Application.Common.Exceptions;

public class ProcessingException : Exception
{
    public ProcessingException(string message) : base(message) {}
    public ProcessingException(string name, object key)
        : base($"Entity '{name}' with key '{key}' couldn't be processed.") {}
}
