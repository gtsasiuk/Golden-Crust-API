using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.IdentityServer.Data;

namespace Platform.IdentityServer.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IIdentityServerInteractionService interaction,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public string? ClientName { get; private set; }
    public string? ErrorMessage { get; private set; }

    public class InputModel
    {
        [Required] public string UserName { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl)
    {
        Input.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        await LoadClientNameAsync(Input.ReturnUrl);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var returnUrl = Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl! : "/";
        Input.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            await LoadClientNameAsync(returnUrl);
            return Page();
        }

        var user = await userManager.FindByNameAsync(Input.UserName)
                   ?? await userManager.FindByEmailAsync(Input.UserName);

        if (user is not null)
        {
            var result = await signInManager.PasswordSignInAsync(
                user, Input.Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                logger.LogInformation("User {UserName} signed in", user.UserName);
                return Redirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                ErrorMessage = "Акаунт тимчасово заблоковано через забагато невдалих спроб.";
                await LoadClientNameAsync(returnUrl);
                return Page();
            }
        }

        // Однакове повідомлення для неіснуючого користувача й невірного пароля
        ErrorMessage = "Невірний логін або пароль.";
        await LoadClientNameAsync(returnUrl);
        return Page();
    }

    private async Task LoadClientNameAsync(string? returnUrl)
    {
        var context = await interaction.GetAuthorizationContextAsync(returnUrl, HttpContext.RequestAborted);
        ClientName = context?.Client?.ClientName ?? context?.Client?.ClientId;
    }
}