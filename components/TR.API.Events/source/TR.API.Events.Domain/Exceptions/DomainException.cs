namespace TR.API.Events.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public class NotFoundException(string message) : DomainException(message);

public class ValidationException : DomainException
{
    public IReadOnlyCollection<string> Errors { get; }

    public ValidationException(string message, IEnumerable<string>? errors = null)
        : base(message)
    {
        Errors = errors?.ToList().AsReadOnly() ?? (IReadOnlyCollection<string>)Array.Empty<string>();
    }
}
