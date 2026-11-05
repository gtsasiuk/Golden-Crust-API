using MediatR;

namespace Reviews.Application.Common.Interfaces;

// Маркери CQRS: команда змінює стан, запит лише читає
public interface ICommand : IRequest;
public interface ICommand<TResponse> : IRequest<TResponse>;