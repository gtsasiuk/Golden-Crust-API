using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Contracts;
using Orders.Domain;

namespace Orders.Api.GrpcServices;

public class OrdersGrpcService(IOrderService orders) : OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "id must be positive"));

        Activity.Current?.SetTag("order.id", request.Id);

        try
        {
            var dto = await orders.GetByIdAsync(request.Id, context.CancellationToken);
            return ToReply(dto);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    private static OrderReply ToReply(OrderDto d)
    {
        var reply = new OrderReply
        {
            Id = d.Id,
            CustomerId = d.CustomerId,
            Status = d.Status,
            TotalAmount = ToMoney(d.TotalAmount),
            OrderDate = ToTimestamp(d.OrderDate),
            DeliveryAddress = d.DeliveryAddress
        };

        if (d.PaymentStatus is not null) reply.PaymentStatus = d.PaymentStatus;

        reply.Items.AddRange(d.Items.Select(i => new OrderItemReply
        {
            MenuItemId = i.MenuItemId,
            ProductName = i.ProductName,
            UnitPrice = ToMoney(i.UnitPrice),
            Quantity = i.Quantity,
            LineTotal = ToMoney(i.LineTotal)
        }));
        return reply;
    }

    private static Money ToMoney(decimal value)
    {
        var units = decimal.Truncate(value);
        return new Money { Units = (long)units, Nanos = (int)((value - units) * 1_000_000_000m) };
    }

    private static Timestamp ToTimestamp(DateTime value)
        => Timestamp.FromDateTime(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));
}