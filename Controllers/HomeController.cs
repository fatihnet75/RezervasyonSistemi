using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;

namespace RezervasyonSistemi.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IEmailService _emailService;

    public HomeController(ILogger<HomeController> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Propiteris()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Contact(ContactFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var subject = $"İletişim Formu - {model.Subject}";
            var body = $@"
                <h2>Yeni İletişim Formu Mesajı</h2>
                <p><strong>Ad Soyad:</strong> {model.Name}</p>
                <p><strong>E-posta:</strong> {model.Email}</p>
                <p><strong>Telefon:</strong> {model.Phone}</p>
                <p><strong>Konu:</strong> {model.Subject}</p>
                <p><strong>Mesaj:</strong></p>
                <p>{model.Message}</p>";

            await _emailService.SendEmailAsync("dfabilisim@gmail.com", subject, body);

            TempData["SuccessMessage"] = "Mesajınız başarıyla iletildi. En kısa sürede size dönüş yapacağız.";
            return RedirectToAction(nameof(Contact));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İletişim formu gönderilirken hata oluştu");
            ModelState.AddModelError("", "Mesajınız gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.");
            return View(model);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
