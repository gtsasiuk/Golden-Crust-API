namespace Orders.Domain;

public class NotFoundException(string message) : Exception(message);          // → 404
public class BusinessConflictException(string message) : Exception(message);  // → 409
public class ValidationException(string message) : Exception(message);        // → 400