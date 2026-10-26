using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public class TagService(IUnitOfWork uow, IMapper mapper) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<List<TagDto>>(await uow.Tags.GetAllAsync(ct));

    public async Task<TagDto> CreateAsync(CreateTagDto input, CancellationToken ct = default)
    {
        var name = input.Name.Trim();
        if (await uow.Tags.ExistsAsync(t => t.Name == name, ct))
            throw new ConflictException($"Tag '{name}' already exists");

        var tag = new Tag(name);
        await uow.Tags.AddAsync(tag, ct);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<TagDto>(tag);
    }

    public async Task<IReadOnlyList<TagMenuItemDto>> GetMenuItemsAsync(long tagId, DateTime? since, CancellationToken ct = default)
    {
        if (await uow.Tags.GetByIdAsync(tagId, ct) is null)
            throw new EntityNotFoundException(nameof(Tag), tagId);

        return mapper.Map<List<TagMenuItemDto>>(await uow.ItemTags.GetByTagAsync(tagId, since, ct));
    }
}