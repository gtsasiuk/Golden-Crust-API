using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Bll.Services;

public class CustomerService(IUnitOfWork uow, IMapper mapper) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<IReadOnlyList<CustomerDto>>(await uow.Customers.GetAllAsync(ct));

    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var customer = await uow.Customers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Customer {id} not found");
        return mapper.Map<CustomerDto>(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = mapper.Map<Customer>(dto);
        customer.Email = customer.Email.Trim();
        customer.FullName = customer.FullName.Trim();

        // Дубль email відсікає UNIQUE в БД: DAL перетворить помилку 2627 на BusinessConflictException (→ 409)
        customer.Id = await uow.Customers.AddAsync(customer, ct);
        return await GetByIdAsync(customer.Id, ct);
    }

    public async Task UpdateAsync(long id, UpdateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = mapper.Map<Customer>(dto);
        customer.Id = id;
        customer.Email = customer.Email.Trim();
        customer.FullName = customer.FullName.Trim();

        if (!await uow.Customers.UpdateAsync(customer, ct))
            throw new NotFoundException($"Customer {id} not found");
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        if (!await uow.Customers.DeleteAsync(id, ct))
            throw new NotFoundException($"Customer {id} not found");
    }
}