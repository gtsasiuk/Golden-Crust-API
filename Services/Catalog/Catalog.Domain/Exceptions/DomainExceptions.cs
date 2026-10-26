namespace Catalog.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public class EntityNotFoundException(string entity, object key)
	: Exception($"{entity} with id '{key}' was not found");

public class ConflictException(string message) : Exception(message);