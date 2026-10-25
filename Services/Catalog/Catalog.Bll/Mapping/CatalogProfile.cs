using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public class CatalogProfile : Profile
{
    public CatalogProfile()
    {
        CreateMap<MenuCategory, CategoryDto>();
        CreateMap<MenuCategory, CategoryWithItemsDto>();

        CreateMap<MenuItem, MenuItemListDto>()
            .ForMember(d => d.PrimaryImageUrl, o => o.MapFrom(s =>
                s.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault()));

        CreateMap<MenuItem, MenuItemDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name))
            .ForMember(d => d.Description, o => o.MapFrom(s => s.Detail != null ? s.Detail.Description : null))
            .ForMember(d => d.Ingredients, o => o.MapFrom(s => s.Detail != null ? s.Detail.Ingredients : null))
            .ForMember(d => d.WeightGrams, o => o.MapFrom(s => s.Detail != null ? s.Detail.WeightGrams : null))
            .ForMember(d => d.Calories, o => o.MapFrom(s => s.Detail != null ? s.Detail.Calories : null))
            .ForMember(d => d.Images, o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder)))
            .ForMember(d => d.Tags, o => o.MapFrom(s => s.ItemTags.Select(t => t.Tag)));

        CreateMap<MenuItemImage, ImageDto>();
        CreateMap<Tag, TagDto>();

        CreateMap<MenuItemTag, TagMenuItemDto>()
            .ForMember(d => d.Item, o => o.MapFrom(s => s.MenuItem));
    }
}