using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace RezervasyonSistemi.Controllers
{
    public class RezervasyonController : Controller
    {
        private readonly MongoDBService _mongoDBService;
        private readonly IEmailService _emailService;
        
        public RezervasyonController(MongoDBService mongoDBService, IEmailService emailService)
        {
            _mongoDBService = mongoDBService;
            _emailService = emailService;
        }

        // Authentication kontrolü için yardımcı metod
        private async Task<User> GetAuthenticatedUserAsync()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            
            if (string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var user = await _mongoDBService.GetUserByEmailAsync(userEmail);
            if (user == null || user.Id != userId)
            {
                // Session'ı temizle
                HttpContext.Session.Clear();
                return null;
            }

            return user;
        }

        public async Task<IActionResult> Index(string status, bool showPast = false)
        {
            // Authentication kontrolü
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                TempData["Error"] = "Rezervasyonlarınızı görüntülemek için giriş yapmalısınız.";
                return RedirectToAction("Login", "Custommer");
            }

            var businesses = await _mongoDBService.GetAllBusniesAsync();
            
            // Sadece giriş yapan kullanıcının rezervasyonlarını al
            var userReservations = await _mongoDBService.GetRezervasyonlarByMusteriEmailAsync(user.Email);
            
            // Debug bilgileri
            ViewBag.TotalReservations = userReservations.Count;
            ViewBag.UserReservationsCount = userReservations.Count;
            ViewBag.CurrentUserEmail = user.Email;
            ViewBag.AllEmails = string.Join(", ", userReservations.Select(r => r.MusteriEmail).Distinct());
            ViewBag.UserReservations = string.Join(", ", userReservations.Select(r => $"{r.MusteriEmail}-{r.Durum}"));
            ViewBag.IsAuthenticated = true;
            
            var vm = new RezervasyonViewModel
            {
                Businesses = businesses,
                Reservations = userReservations,
                CurrentUser = user
            };
            ViewBag.SelectedStatus = status;
            ViewBag.ShowPast = showPast;
            ViewBag.CurrentUser = user; // Kullanıcı bilgilerini ViewBag'e ekle
            return View(vm);
        }

        // Rezervasyon oluşturma formu (işletme seçildikten sonra)
        public async Task<IActionResult> Create(string? id, string? selectedDate)
        {
            // Authentication kontrolü
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                TempData["Error"] = "Rezervasyon oluşturmak için giriş yapmalısınız.";
                return RedirectToAction("Login", "Custommer");
            }
            
            // Kullanıcı bilgilerini ViewBag'e ekle
            ViewBag.UserEmail = user.Email;
            ViewBag.CurrentUser = user;
            
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

                // Eğer tarih seçilmişse, o tarihteki dolu saatleri getir
                if (!string.IsNullOrEmpty(selectedDate) && DateTime.TryParse(selectedDate, out DateTime tarih))
                {
                    var doluSaatler = await _mongoDBService.GetDoluSaatlerListesiAsync(id, tarih);
                    ViewBag.DoluSaatler = doluSaatler;
                }
                else
                {
                    ViewBag.DoluSaatler = new List<string>();
                }

                return View(business);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> Create(string id, string service, string date, string time, string musteriAdSoyad, string musteriEmail, string notlar = "")
        {
            // Authentication kontrolü
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                TempData["Error"] = "Rezervasyon oluşturmak için giriş yapmalısınız.";
                return RedirectToAction("Login", "Custommer");
            }

            // Debug bilgisi - gelen parametreleri logla
            System.Diagnostics.Debug.WriteLine($"Create POST - Gelen parametreler:");
            System.Diagnostics.Debug.WriteLine($"id: {id}");
            System.Diagnostics.Debug.WriteLine($"service: {service}");
            System.Diagnostics.Debug.WriteLine($"date: {date}");
            System.Diagnostics.Debug.WriteLine($"time: {time}");
            System.Diagnostics.Debug.WriteLine($"musteriAdSoyad: {musteriAdSoyad}");
            System.Diagnostics.Debug.WriteLine($"musteriEmail: {musteriEmail}");
            System.Diagnostics.Debug.WriteLine($"notlar: {notlar}");
            System.Diagnostics.Debug.WriteLine($"Session UserEmail: {user.Email}");
            
            var business = await _mongoDBService.GetBusniesByIdAsync(id);
            if (business == null)
            {
                TempData["Error"] = "İşletme bulunamadı.";
                            return RedirectToAction("Index");
        }

            // Seçilen tarih ve saatte dolu olup olmadığını kontrol et
            if (!DateTime.TryParse(date, out DateTime selectedDate))
            {
                TempData["Error"] = "Geçersiz tarih formatı.";
                return RedirectToAction("Create", new { id = id });
            }
            
            // UTC tarih oluştur - timezone sorununu tamamen çözmek için
            var utcDate = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, 0, 0, 0, DateTimeKind.Utc);
            
            // Geçmiş tarih kontrolü
            if (utcDate < DateTime.UtcNow.Date)
            {
                TempData["Error"] = "Geçmiş tarihler için rezervasyon yapılamaz.";
                return RedirectToAction("Create", new { id = id });
            }

            // Seçilen saatte dolu olup olmadığını kontrol et
            var doluSaatler = await _mongoDBService.GetDoluSaatlerListesiAsync(id, utcDate);
            if (doluSaatler.Contains(time))
            {
                TempData["Error"] = $"Seçilen saat ({time}) dolu. Lütfen başka bir saat seçin.";
                return RedirectToAction("Create", new { id = id, selectedDate = date });
            }

            // Rezervasyon oluştur
            var rezervasyon = new Rezervasyon
            {
                IsletmeId = id,
                IsletmeAd = business.Ad, // yeni alan
                MusteriEmail = user.Email, // Session'dan gelen kullanıcı email'ini kullan
                MusteriAdSoyad = user.Ad + " " + user.Soyad, // Session'dan gelen kullanıcı adını kullan
                Tarih = utcDate,
                Saat = time,
                HizmetAd = service,
                HizmetSuresi = business.Hizmetler?.FirstOrDefault(h => h.Ad == service)?.Sure ?? "30 dakika",
                Durum = "Bekliyor",
                Notlar = notlar,
                OlusturmaTarihi = DateTime.UtcNow // yeni alan
            };

            var result = await _mongoDBService.CreateRezervasyonAsync(rezervasyon);
            if (result != null)
            {
                TempData["Success"] = "Rezervasyonunuz başarıyla oluşturuldu!";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["Error"] = "Rezervasyon oluşturulurken bir hata oluştu.";
                return RedirectToAction("Create", new { id = id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableHours(string businessId, string date)
        {
            try
            {
                if (DateTime.TryParse(date, out DateTime selectedDate))
                {
                    // UTC tarih oluştur - timezone sorununu tamamen çözmek için
                    var utcDate = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, 0, 0, 0, DateTimeKind.Utc);
                    
                    // Debug bilgisi
                    System.Diagnostics.Debug.WriteLine($"GetAvailableHours - Gelen tarih: {date}, Parse edilen tarih: {selectedDate:yyyy-MM-dd}, UTC tarih: {utcDate:yyyy-MM-dd}");
                    
                    var doluSaatler = await _mongoDBService.GetDoluSaatlerListesiAsync(businessId, utcDate.Date);
                    
                    // İşletme bilgilerini de getir
                    var business = await _mongoDBService.GetBusniesByIdAsync(businessId);
                    var hizmetler = business?.Hizmetler ?? new List<BusniesService>();
                    
                    // Müsait saatleri hesapla - sadece tam saatler
                    var musaitSaatler = new List<string>();
                    for (int i = 8; i < 20; i++) // 8:00'dan 19:00'a kadar (20:00 dahil değil)
                    {
                        var saat = $"{i}:00";
                        
                        // Sadece dolu olmayan saatleri ekle
                        if (!doluSaatler.Contains(saat))
                        {
                            musaitSaatler.Add(saat);
                        }
                    }
                    
                    // Debug bilgisi
                    System.Diagnostics.Debug.WriteLine($"Tarih: {utcDate:yyyy-MM-dd}, Dolu saatler: {string.Join(", ", doluSaatler)}, Müsait saatler: {string.Join(", ", musaitSaatler)}");
                    
                    return Json(new { 
                        success = true, 
                        doluSaatler = doluSaatler,
                        musaitSaatler = musaitSaatler,
                        hizmetler = hizmetler.Select(h => new { ad = h.Ad, sure = h.Sure }).ToList()
                    });
                }
                
                return Json(new { success = false, message = "Geçersiz tarih formatı" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Profil resmi yükleme
        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> ProfilResmiYukle(IFormFile profilResmi)
        {
            // Authentication kontrolü
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
            {
                return Json(new { success = false, message = "Profil resmi yüklemek için giriş yapmalısınız." });
            }

            try
            {
                if (profilResmi == null || profilResmi.Length == 0)
                {
                    return Json(new { success = false, message = "Lütfen bir resim dosyası seçin." });
                }

                // Dosya boyutu kontrolü (5MB)
                if (profilResmi.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "Dosya boyutu 5MB'dan büyük olamaz." });
                }

                // Dosya türü kontrolü
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(profilResmi.ContentType.ToLower()))
                {
                    return Json(new { success = false, message = "Sadece JPEG, PNG ve GIF dosyaları kabul edilir." });
                }

                // Dosya adı oluştur
                var fileName = $"user_{user.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(profilResmi.FileName)}";
                var filePath = Path.Combine("wwwroot", "img", "users", fileName);

                // Dizin yoksa oluştur
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Dosyayı kaydet
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilResmi.CopyToAsync(stream);
                }

                // Veritabanında kullanıcı profil resmini güncelle
                var imageUrl = $"/img/users/{fileName}";
                await _mongoDBService.UpdateUserProfileImageAsync(user.Id, imageUrl);

                return Json(new { success = true, url = imageUrl, message = "Profil resmi başarıyla yüklendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }

        // Çıkış yapma
        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public IActionResult Logout()
        {
            // Session'ı temizle
            HttpContext.Session.Clear();
            
            // Response cache'i temizle
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["Surrogate-Control"] = "no-store";
            
            return RedirectToAction("Login", "Custommer");
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> Delete(string rezervasyonId)
        {
            // Authentication kontrolü
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
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

                if (rezervasyon.MusteriEmail != user.Email)
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
