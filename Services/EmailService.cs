using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace RezervasyonSistemi.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var smtpSettings = _configuration.GetSection("Email");
        var smtpServer = smtpSettings["SmtpServer"];
        var smtpPort = int.Parse(smtpSettings["SmtpPort"] ?? "587");
        var smtpUsername = smtpSettings["Username"];
        var smtpPassword = smtpSettings["Password"];
        var fromEmail = smtpSettings["FromEmail"];
        var fromName = smtpSettings["FromName"];

        using var client = new SmtpClient(smtpServer, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = true
        };

        var message = new MailMessage
        {
            From = new MailAddress(fromEmail ?? smtpUsername, fromName ?? "Rezervasyon Sistemi"),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(to));

        await client.SendMailAsync(message);
    }
} 