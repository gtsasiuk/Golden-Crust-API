namespace Reviews.Domain.Exceptions;

public class NotFoundException(string message) : Exception(message)
{
    public NotFoundException(string entity, object key) : this($"{entity} '{key}' was not found") { }
}