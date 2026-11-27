using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.IdentityServer.Data;

namespace Platform.IdentityServer.Pages.Account;

public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction) : PageModel
{
    // IdentityServer перенаправляє сюди з /connect/endsession?logoutId=...
    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        var context = await interaction.GetLogoutContextAsync(logoutId, HttpContext.RequestAborted);

        if (User.Identity?.IsAuthenticated == true)
            await signInManager.SignOutAsync();

        if (!string.IsNullOrEmpty(context?.PostLogoutRedirectUri))
            return Redirect(context.PostLogoutRedirectUri);

        return RedirectToPage("/Index");
    }
}