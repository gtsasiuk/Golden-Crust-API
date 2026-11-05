using AutoMapper;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.WebApi.Models;

namespace Reviews.WebApi.Mapping;

public class RequestProfile : Profile
{
    public RequestProfile() => CreateMap<CreateReviewRequest, CreateReviewCommand>();
}