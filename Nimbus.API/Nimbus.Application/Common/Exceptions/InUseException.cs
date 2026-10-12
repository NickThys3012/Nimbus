namespace Nimbus.Application.Common.Exceptions;

/// <summary>The resource cannot be removed because other data still depends on it (HTTP 409).</summary>
public class InUseException : Exception
{
    public InUseException(string message) : base(message) {}
}
