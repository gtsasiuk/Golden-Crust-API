namespace Auth.Api.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

// Development-заглушка: замість листа пише в лог. Текст листа видно в Aspire Dashboard → Structured logs.
public sealed class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[MOCK EMAIL] To: {To} | Subject: {Subject}{NewLine}{Body}",
            to, subject, Environment.NewLine, body);
        return Task.CompletedTask;
    }
}