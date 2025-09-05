using Domain.Interfaces;

namespace Infrastructure.Services;

public class EmailService() : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        await SendEmailAsync(to, subject, body, false);
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml)
    {
        // TODO: Implement actual email sending logic
        // This is a placeholder implementation
        // In a real application, you would use:
        // - SendGrid
        // - SMTP
        // - Amazon SES
        // - Azure Communication Services
        // etc.

        Console.WriteLine($"Sending email to: {to}");
        Console.WriteLine($"Subject: {subject}");
        Console.WriteLine($"Body: {body}");
        Console.WriteLine($"Is HTML: {isHtml}");

        // Simulate async operation
        await Task.Delay(100);
    }
}
