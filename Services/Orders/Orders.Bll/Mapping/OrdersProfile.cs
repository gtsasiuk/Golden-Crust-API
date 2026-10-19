using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Domain;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Orders.Bll.Mapping;

public class OrdersProfile : Profile
{
    public OrdersProfile()
    {
        // Domain → DTO (відповідь API)
        CreateMap<Customer, CustomerDto>();
        CreateMap<MenuItem, MenuItemDto>();
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Order, OrderDto>().ForMember(d => d.PaymentStatus, o => o.Ignore());   // заповнює сервіс
        CreateMap<OrderSummary, OrderSummaryDto>();

        // DTO → Domain (вхід API). Id і службові поля належать БД, тому їх ігноруємо.
        CreateMap<CreateCustomerDto, Customer>()
            .ForMember(c => c.Id, o => o.Ignore())
            .ForMember(c => c.CreatedAt, o => o.Ignore())
            .ForMember(c => c.UpdatedAt, o => o.Ignore());
        CreateMap<UpdateCustomerDto, Customer>()
            .ForMember(c => c.Id, o => o.Ignore())
            .ForMember(c => c.CreatedAt, o => o.Ignore())
            .ForMember(c => c.UpdatedAt, o => o.Ignore());
    }
}