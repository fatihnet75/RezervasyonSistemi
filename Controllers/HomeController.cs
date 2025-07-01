using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;

namespace RezervasyonSistemi.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
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

        // Test email endpoint
        [HttpPost]
        public async Task<IActionResult> TestEmail([FromServices] IEmailService emailService)
        {
            try
            {
                await emailService.SendEmailAsync("test@example.com", "Test Email", "Bu bir test emailidir.");
                return Json(new { success = true, message = "Test email başarıyla gönderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Email gönderimi başarısız: {ex.Message}" });
            }
        }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
