using Microsoft.AspNetCore.Mvc;

namespace RezervasyonSistemi.Controllers
{
    public class IsletmePanelController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
} 