using AutoMapper;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Mappings;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Comment, CommentDto>()
            .ForMember(d => d.AuthorCustomerId, o => o.MapFrom(s => s.Author.CustomerId))
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author.DisplayName));

        CreateMap<Review, ReviewDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.AuthorCustomerId, o => o.MapFrom(s => s.Author.CustomerId))
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author.DisplayName))
            .ForMember(d => d.CommentsCount, o => o.MapFrom(s => s.Comments.Count))
            .ForMember(d => d.DiscussionId,
                o => o.MapFrom(s => s.DiscussionId.HasValue ? s.DiscussionId.Value.ToString() : null));
    }
}