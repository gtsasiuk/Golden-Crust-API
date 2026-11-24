namespace Auth.Api.Options;

public class SeedAdminSettings
{
    public const string SectionName = "SeedAdmin";

    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}