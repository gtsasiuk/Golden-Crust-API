using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Platform.IdentityServer.Pages;

public class ErrorModel(IIdentityServerInteractionService interaction) : PageModel
{
    public string? Error { get; private set; }
    public string? Description { get; private set; }
    public string? RequestId { get; private set; }

    public async Task OnGetAsync(string? errorId)
    {
        var message = await interaction.GetErrorContextAsync(errorId, HttpContext.RequestAborted);

        Error = message?.Error;
        Description = message?.ErrorDescription;
        RequestId = message?.RequestId ?? HttpContext.TraceIdentifier;
    }
}