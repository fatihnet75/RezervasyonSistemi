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
        try
        {
            var smtpSettings = _configuration.GetSection("Email");
            var smtpServer = smtpSettings["SmtpServer"];
            var smtpPort = int.Parse(smtpSettings["SmtpPort"] ?? "587");
            var smtpUsername = smtpSettings["Username"];
            var smtpPassword = smtpSettings["Password"];
            var fromEmail = smtpSettings["FromEmail"];
            var fromName = smtpSettings["FromName"];

            // Debug bilgisi
            System.Diagnostics.Debug.WriteLine($"Email gönderimi başlatılıyor:");
            System.Diagnostics.Debug.WriteLine($"To: {to}");
            System.Diagnostics.Debug.WriteLine($"Subject: {subject}");
            System.Diagnostics.Debug.WriteLine($"SMTP Server: {smtpServer}");
            System.Diagnostics.Debug.WriteLine($"SMTP Port: {smtpPort}");
            System.Diagnostics.Debug.WriteLine($"Username: {smtpUsername}");
            
            // Console'a da yazdır
            Console.WriteLine($"=== EMAIL SERVICE DEBUG ===");
            Console.WriteLine($"To: {to}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"SMTP Server: {smtpServer}");
            Console.WriteLine($"SMTP Port: {smtpPort}");
            Console.WriteLine($"Username: {smtpUsername}");
            Console.WriteLine($"FromEmail: {fromEmail}");
            Console.WriteLine($"FromName: {fromName}");
            Console.WriteLine($"============================");

                    using var client = new SmtpClient(smtpServer, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = true,
            Timeout = 10000 // 10 saniye timeout
        };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUsername, fromName ?? "Rezervasyon Sistemi"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(to));
            
            Console.WriteLine($"MailMessage oluşturuldu:");
            Console.WriteLine($"From: {message.From}");
            Console.WriteLine($"To: {message.To}");
            Console.WriteLine($"Subject: {message.Subject}");
            Console.WriteLine($"Body Length: {message.Body?.Length ?? 0}");

            await client.SendMailAsync(message);
            
            System.Diagnostics.Debug.WriteLine($"Email başarıyla gönderildi: {to}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Email gönderimi başarısız: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine($"=== EMAIL HATA ===");
            Console.WriteLine($"Hata: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine($"==================");
            throw;
        }
    }
} 