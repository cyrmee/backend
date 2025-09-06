using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class EmailService(ILogger<EmailService> logger) : IEmailService
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

        logger.LogInformation("Sending email to: {To}", to);
        logger.LogInformation("Subject: {Subject}", subject);
        logger.LogInformation("Body: {Body}", body);
        logger.LogInformation("Is HTML: {IsHtml}", isHtml);

        // Simulate async operation
        await Task.Delay(100);
    }
}