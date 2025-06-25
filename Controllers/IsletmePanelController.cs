using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;

namespace RezervasyonSistemi.Controllers
{
    public class IsletmePanelController : Controller
    {
        private readonly MongoDBService _mongoDBService;
        public IsletmePanelController(MongoDBService mongoDBService)
        {
            _mongoDBService = mongoDBService;
        }

        public async Task<IActionResult> Index()
        {
            string email = HttpContext.Session.GetString("BusniesEmail") ?? "demo@isletme.com";
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null)
                return RedirectToAction("Login", "Busnies");
            var rezervasyonlar = await _mongoDBService.GetRezervasyonlarByIsletmeIdAsync(busnies.Id);
            ViewBag.Busnies = busnies;
            ViewBag.Rezervasyonlar = rezervasyonlar;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> HizmetEkle(string ad, string sure)
        {
            string email = HttpContext.Session.GetString("BusniesEmail") ?? "demo@isletme.com";
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null) return Json(new { success = false, message = "İşletme bulunamadı." });
            var service = new BusniesService { Ad = ad, Sure = sure };
            var result = await _mongoDBService.AddBusniesServiceAsync(busnies.Id, service);
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> HizmetSil(string ad)
        {
            string email = HttpContext.Session.GetString("BusniesEmail") ?? "demo@isletme.com";
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null) return Json(new { success = false, message = "İşletme bulunamadı." });
            var result = await _mongoDBService.RemoveBusniesServiceAsync(busnies.Id, ad);
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> HizmetGuncelle(string oldAd, string yeniAd, string yeniSure)
        {
            string email = HttpContext.Session.GetString("BusniesEmail") ?? "demo@isletme.com";
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null) return Json(new { success = false, message = "İşletme bulunamadı." });
            var updated = new BusniesService { Ad = yeniAd, Sure = yeniSure };
            var result = await _mongoDBService.UpdateBusniesServiceAsync(busnies.Id, oldAd, updated);
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> ProfilResmiYukle(IFormFile profilResmi)
        {
            if (profilResmi == null || profilResmi.Length == 0)
                return Json(new { success = false, message = "Dosya seçilmedi." });

            string email = HttpContext.Session.GetString("BusniesEmail") ?? "demo@isletme.com";
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null) return Json(new { success = false, message = "İşletme bulunamadı." });

            var ext = System.IO.Path.GetExtension(profilResmi.FileName);
            var fileName = $"busnies_{busnies.Id}{ext}";
            var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "busnies", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await profilResmi.CopyToAsync(stream);
            }
            // Veritabanına yolunu kaydet
            busnies.ProfilResmiUrl = $"/img/busnies/{fileName}";
            await _mongoDBService.UpdateBusniesProfileImageAsync(busnies.Id, busnies.ProfilResmiUrl);
            return Json(new { success = true, url = busnies.ProfilResmiUrl });
        }

        [HttpPost]
        public async Task<IActionResult> RezervasyonDurumGuncelle(string rezervasyonId, string yeniDurum)
        {
            var result = await _mongoDBService.UpdateRezervasyonDurumAsync(rezervasyonId, yeniDurum);
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> RezervasyonSil(string rezervasyonId)
        {
            string email = HttpContext.Session.GetString("BusniesEmail") ?? "demo@isletme.com";
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null) 
                return Json(new { success = false, message = "İşletme bulunamadı." });

            try
            {
                // Rezervasyonun işletmeye ait olup olmadığını kontrol et
                var rezervasyon = await _mongoDBService.GetRezervasyonByIdAsync(rezervasyonId);
                if (rezervasyon == null)
                {
                    return Json(new { success = false, message = "Rezervasyon bulunamadı." });
                }

                if (rezervasyon.IsletmeId != busnies.Id)
                {
                    return Json(new { success = false, message = "Bu rezervasyonu silme yetkiniz yok." });
                }

                var result = await _mongoDBService.DeleteRezervasyonAsync(rezervasyonId);
                if (result)
                {
                    return Json(new { success = true, message = "Rezervasyon başarıyla silindi." });
                }
                else
                {
                    return Json(new { success = false, message = "Rezervasyon silinemedi." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }
    }
} 