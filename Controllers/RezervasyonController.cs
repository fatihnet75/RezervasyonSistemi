using Microsoft.AspNetCore.Mvc;

namespace RezervasyonSistemi.Controllers
{
    public class RezervasyonController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
