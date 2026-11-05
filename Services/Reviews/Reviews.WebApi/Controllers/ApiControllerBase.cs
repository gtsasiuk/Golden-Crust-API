using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Reviews.WebApi.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    private IMapper? _mapper;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
    protected IMapper Mapper => _mapper ??= HttpContext.RequestServices.GetRequiredService<IMapper>();
}