using System.Security.Claims;
using Orders.Bll.Services;
using Orders.Domain;
using Platform.ServiceDefaults;

namespace Orders.Api.Security;

public interface IAccessGuard
{
    Task EnsureCustomerAccessAsync(ClaimsPrincipal user, long customerId, CancellationToken ct);
    Task EnsureOrderAccessAsync(ClaimsPrincipal user, long orderId, CancellationToken ct);
    void EnsureOwnEmail(ClaimsPrincipal user, string email);
}

public sealed class AccessGuard(ICustomerService customers, IOrderService orders) : IAccessGuard
{
    private const string Denied = "You can only access your own data";

    public async Task EnsureCustomerAccessAsync(ClaimsPrincipal user, long customerId, CancellationToken ct)
    {
        if (user.IsAdmin()) return;

        try
        {
            var customer = await customers.GetByIdAsync(customerId, ct);
            if (IsSameEmail(customer.Email, user)) return;
        }
        catch (NotFoundException) {  }

        throw new ForbiddenException(Denied);
    }

    public async Task EnsureOrderAccessAsync(ClaimsPrincipal user, long orderId, CancellationToken ct)
    {
        if (user.IsAdmin()) return;

        try
        {
            var order = await orders.GetByIdAsync(orderId, ct);
            await EnsureCustomerAccessAsync(user, order.CustomerId, ct);
            return;
        }
        catch (NotFoundException) {  }

        throw new ForbiddenException(Denied);
    }

    public void EnsureOwnEmail(ClaimsPrincipal user, string email)
    {
        if (user.IsAdmin()) return;
        if (!IsSameEmail(email.Trim(), user))
            throw new ForbiddenException("You can only manage a customer profile with your own email");
    }

    private static bool IsSameEmail(string email, ClaimsPrincipal user)
    {
        var claim = user.GetEmail();
        return !string.IsNullOrEmpty(claim) && string.Equals(email, claim, StringComparison.OrdinalIgnoreCase);
    }
}