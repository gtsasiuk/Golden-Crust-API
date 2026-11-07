using System.Diagnostics;
using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Contracts;

namespace Reviews.WebApi.GrpcServices;

public class ReviewsGrpcService(ISender sender) : ReviewsService.ReviewsServiceBase
{
    private const int MaxMenuItems = 100;
    private const int MaxCount = 50;

    public override async Task<LatestReviewsReply> GetLatestForMenuItems(
        GetLatestForMenuItemsRequest request, ServerCallContext context)
    {
        if (request.MenuItemIds.Count == 0)
            throw InvalidArgument("menu_item_ids must not be empty");
        if (request.MenuItemIds.Count > MaxMenuItems)
            throw InvalidArgument($"at most {MaxMenuItems} menu items per request");
        if (request.MenuItemIds.Any(id => id <= 0))
            throw InvalidArgument("all menu_item_ids must be positive");
        if (request.Count is < 1 or > MaxCount)
            throw InvalidArgument($"count must be between 1 and {MaxCount}");

        var ids = request.MenuItemIds.Distinct().ToList();
        Activity.Current?.SetTag("menuitem.count", ids.Count);

        try
        {
            var pages = await Task.WhenAll(ids.Select(id =>
                sender.Send(new GetReviewsListQuery(id, 1, request.Count), context.CancellationToken)));

            var reply = new LatestReviewsReply();
            for (var i = 0; i < ids.Count; i++)
                reply.Items.Add(ToReply(ids[i], pages[i]));
            return reply;
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static MenuItemReviews ToReply(long menuItemId, PagedResult<ReviewDto> page)
    {
        var reply = new MenuItemReviews { MenuItemId = menuItemId, TotalCount = page.TotalCount };

        foreach (var r in page.Items)
        {
            var review = new ReviewReply
            {
                Id = r.Id,
                MenuItemId = r.MenuItemId,
                AuthorName = r.AuthorName,
                Type = r.Type.ToString().ToLowerInvariant(),
                Rating = r.Rating,
                CreatedAt = ToTimestamp(r.CreatedAt)
            };
            if (r.Text is not null) review.Text = r.Text;
            reply.Latest.Add(review);
        }
        return reply;
    }

    private static Timestamp ToTimestamp(DateTime value)
        => Timestamp.FromDateTime(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static RpcException InvalidArgument(string message)
        => new(new Status(StatusCode.InvalidArgument, message));
}