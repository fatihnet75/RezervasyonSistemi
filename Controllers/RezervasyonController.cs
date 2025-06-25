using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace RezervasyonSistemi.Controllers
{
    public class RezervasyonController : Controller
    {
        private readonly MongoDBService _mongoDBService;
        public RezervasyonController(MongoDBService mongoDBService)
        {
            _mongoDBService = mongoDBService;
        }

        public async Task<IActionResult> Index(string city, string district, string status, bool showPast = false)
        {
            // Kullanıcının giriş yapıp yapmadığını session ile kontrol et
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            
            if (string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Rezervasyonlarınızı görüntülemek için giriş yapmalısınız.";
                return RedirectToAction("Login", "Custommer");
            }

            var user = await _mongoDBService.GetUserByEmailAsync(userEmail);
            if (user == null)
            {
                TempData["Error"] = "Kullanıcı bilgileriniz bulunamadı. Lütfen tekrar giriş yapın.";
                return RedirectToAction("Login", "Custommer");
            }

            var businesses = await _mongoDBService.GetAllBusniesAsync();
            
            // Sadece giriş yapan kullanıcının rezervasyonlarını al
            var userReservations = await _mongoDBService.GetRezervasyonlarByMusteriEmailAsync(userEmail);
            
            // Debug bilgileri
            ViewBag.TotalReservations = userReservations.Count;
            ViewBag.UserReservationsCount = userReservations.Count;
            ViewBag.CurrentUserEmail = userEmail;
            ViewBag.AllEmails = string.Join(", ", userReservations.Select(r => r.MusteriEmail).Distinct());
            ViewBag.UserReservations = string.Join(", ", userReservations.Select(r => $"{r.MusteriEmail}-{r.Durum}"));
            ViewBag.IsAuthenticated = true;
            
            var vm = new RezervasyonViewModel
            {
                Businesses = businesses,
                Reservations = userReservations,
                CurrentUser = user
            };
            ViewBag.SelectedCity = city;
            ViewBag.SelectedDistrict = district;
            ViewBag.SelectedStatus = status;
            ViewBag.ShowPast = showPast;
            ViewBag.CurrentUser = user; // Kullanıcı bilgilerini ViewBag'e ekle
            return View(vm);
        }

        // Rezervasyon oluşturma formu (işletme seçildikten sonra)
        public async Task<IActionResult> Create(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                // Tüm işletmeleri getir
                var businesses = await _mongoDBService.GetAllBusniesAsync();
                ViewBag.AllBusinesses = businesses;
                return View(new Busnies()); // Boş model, işletme seçimi için
            }
            else
            {
                // Seçili işletmeyi getir
                var business = await _mongoDBService.GetBusniesByIdAsync(id);
                if (business == null)
                {
                    return NotFound();
                }
                return View(business);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(string id, string service, string date, string time, string musteriAdSoyad, string musteriEmail, string notlar = "")
        {
            // Kullanıcının giriş yapıp yapmadığını session ile kontrol et
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "Rezervasyon oluşturmak için giriş yapmalısınız.";
                return RedirectToAction("Login", "Custommer");
            }

            var business = await _mongoDBService.GetBusniesByIdAsync(id);
            if (business == null)
            {
                TempData["Error"] = "İşletme bulunamadı.";
                return RedirectToAction("Index");
            }
            var hizmet = business.Hizmetler.FirstOrDefault(h => h.Ad == service);
            var emailToUse = userEmail; // Session'dan gelen email'i kullan
            var rezervasyon = new RezervasyonSistemi.Models.Rezervasyon
            {
                IsletmeId = id,
                MusteriAdSoyad = musteriAdSoyad,
                MusteriEmail = emailToUse,
                Tarih = DateTime.Parse(date),
                Saat = time,
                HizmetAd = service,
                HizmetSuresi = hizmet?.Sure ?? "1 saat",
                Durum = "Bekliyor",
                Notlar = notlar ?? "",
                CreatedAt = DateTime.UtcNow
            };
            await _mongoDBService.CreateRezervasyonAsync(rezervasyon);
            TempData["Success"] = "Rezervasyon talebiniz alınmıştır. İşletme onayladığında bilgilendirileceksiniz.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ProfilResmiYukle(IFormFile profilResmi)
        {
            // Kullanıcının giriş yapıp yapmadığını session ile kontrol et
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            
            if (string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Profil resmi yüklemek için giriş yapmalısınız." });
            }

            if (profilResmi == null || profilResmi.Length == 0)
                return Json(new { success = false, message = "Dosya seçilmedi." });

            try
            {
                var ext = System.IO.Path.GetExtension(profilResmi.FileName);
                var fileName = $"user_{userId}{ext}";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "users", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                
                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await profilResmi.CopyToAsync(stream);
                }
                
                // Veritabanına yolunu kaydet
                var profilResmiUrl = $"/img/users/{fileName}";
                await _mongoDBService.UpdateUserProfileImageAsync(userId, profilResmiUrl);
                
                return Json(new { success = true, url = profilResmiUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string rezervasyonId)
        {
            // Kullanıcının giriş yapıp yapmadığını session ile kontrol et
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { success = false, message = "Rezervasyon silmek için giriş yapmalısınız." });
            }

            try
            {
                // Rezervasyonun kullanıcıya ait olup olmadığını kontrol et
                var rezervasyon = await _mongoDBService.GetRezervasyonByIdAsync(rezervasyonId);
                if (rezervasyon == null)
                {
                    return Json(new { success = false, message = "Rezervasyon bulunamadı." });
                }

                if (rezervasyon.MusteriEmail != userEmail)
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

    public class RezervasyonViewModel
    {
        public List<Busnies> Businesses { get; set; }
        public List<Rezervasyon> Reservations { get; set; }
        public User CurrentUser { get; set; }
    }
}
