using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Auth.Api.Services;

public static class IdentityTokenEncoder
{
    public static string Encode(string token)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? Decode(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}