using Microsoft.AspNetCore.Mvc;

namespace RezervasyonSistemi.Controllers
{
    public class CustommerController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
}
