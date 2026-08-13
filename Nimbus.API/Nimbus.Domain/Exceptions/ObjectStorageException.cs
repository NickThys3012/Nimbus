namespace Nimbus.Domain.Exceptions;

/// <summary>
///     Thrown when the object store cannot complete an operation after its configured retries
///     are exhausted (or for a non-retryable failure). Callers get a handled, typed error instead
///     of an unhandled infrastructure exception (e.g. an S3/MinIO SDK exception) leaking out of
///     the Domain abstraction.
/// </summary>
public class ObjectStorageException : DomainException
{
    public ObjectStorageException(string message) : base(message) {}

    public ObjectStorageException(string message, Exception innerException) : base(message, innerException) {}
}
