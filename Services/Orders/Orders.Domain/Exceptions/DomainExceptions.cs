namespace Orders.Domain;

public class NotFoundException(string message, Exception? inner = null) : Exception(message, inner);          // → 404
public class BusinessConflictException(string message, Exception? inner = null) : Exception(message, inner);  // → 409
public class ValidationException(string message, Exception? inner = null) : Exception(message, inner);        // → 400
public class ForbiddenException(string message, Exception? inner = null) : Exception(message, inner);          // → 403