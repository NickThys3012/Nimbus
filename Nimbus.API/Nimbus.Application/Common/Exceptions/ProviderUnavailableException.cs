namespace Nimbus.Application.Common.Exceptions;

/// <summary>An outside provider could not give an answer (down, too slow, an error). Answers 502: not the caller's fault, nothing was changed.</summary>
public class ProviderUnavailableException : Exception
{
    public ProviderUnavailableException(string provider)
        : base($"{provider} could not be reached.") {}
}
