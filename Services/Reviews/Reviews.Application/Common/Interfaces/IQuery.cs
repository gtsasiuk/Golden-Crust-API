using MediatR;

namespace Reviews.Application.Common.Interfaces;

public interface IQuery<TResponse> : IRequest<TResponse>;