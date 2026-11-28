using Aspire.Hosting.ApplicationModel;

public static class JwtResourceExtensions
{
    public const string Issuer = "GoldenCrust.Auth";
    public const string Audience = "GoldenCrust.Api";

    // Єдине місце, де задається, чим усі сервіси перевіряють локальний JWT
    public static IResourceBuilder<T> WithJwtSettings<T>(
        this IResourceBuilder<T> builder, IResourceBuilder<ParameterResource> secret)
        where T : IResourceWithEnvironment
        => builder
            .WithEnvironment("Jwt__Issuer", Issuer)
            .WithEnvironment("Jwt__Audience", Audience)
            .WithEnvironment("Jwt__SecretKey", secret);
}