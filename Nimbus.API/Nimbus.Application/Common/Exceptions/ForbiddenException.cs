namespace Nimbus.Application.Common.Exceptions;

/// <summary>The resource exists but the caller may not use it. Answers 403, unlike <see cref="NotFoundException" />'s 404.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string name, object key)
        : base($"Access to '{name}' with key '{key}' is not allowed.") {}
}
