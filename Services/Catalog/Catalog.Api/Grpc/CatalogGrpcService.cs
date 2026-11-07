using System.Diagnostics;
using Catalog.Bll.Dto;
using Catalog.Bll.Services;
using Catalog.Contracts;
using Catalog.Domain.Exceptions;
using Grpc.Core;

namespace Catalog.Api.GrpcServices;

public class CatalogGrpcService(IMenuItemService menu) : CatalogService.CatalogServiceBase
{
    private const int MaxBatchSize = 100;

    public override async Task<MenuItemReply> GetMenuItem(GetMenuItemRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw InvalidArgument("id must be positive");

        Activity.Current?.SetTag("menuitem.id", request.Id);

        try
        {
            var dto = await menu.GetByIdAsync(request.Id, context.CancellationToken);
            return ToReply(dto);
        }
        catch (EntityNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<MenuItemsReply> GetMenuItemsByIds(GetMenuItemsByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count == 0)
            throw InvalidArgument("ids must not be empty");
        if (request.Ids.Count > MaxBatchSize)
            throw InvalidArgument($"at most {MaxBatchSize} ids per request");
        if (request.Ids.Any(id => id <= 0))
            throw InvalidArgument("all ids must be positive");

        var ids = request.Ids.Distinct().ToList();
        Activity.Current?.SetTag("menuitem.count", ids.Count);

        var reply = new MenuItemsReply();

        foreach (var id in ids)
        {
            try
            {
                reply.Items.Add(ToReply(await menu.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (EntityNotFoundException)
            {

            }
        }
        return reply;
    }

    private static MenuItemReply ToReply(MenuItemDto d)
    {
        var reply = new MenuItemReply
        {
            Id = d.Id,
            Name = d.Name,
            CategoryName = d.CategoryName,
            Price = MoneyMapping.ToMoney(d.Price),
            IsAvailable = d.IsAvailable
        };

        if (d.Description is not null) reply.Description = d.Description;
        var primaryImage = d.Images.FirstOrDefault(i => i.IsPrimary)?.Url;
        if (primaryImage is not null) reply.PrimaryImageUrl = primaryImage;

        reply.Tags.AddRange(d.Tags.Select(t => t.Name));
        return reply;
    }

    private static RpcException InvalidArgument(string message)
        => new(new Status(StatusCode.InvalidArgument, message));
}