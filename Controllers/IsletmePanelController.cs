using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;

namespace RezervasyonSistemi.Controllers
{
    public class IsletmePanelController : Controller
    {
        private readonly MongoDBService _mongoDBService;
        private readonly IEmailService _emailService;
        
        public IsletmePanelController(MongoDBService mongoDBService, IEmailService emailService)
        {
            _mongoDBService = mongoDBService;
            _emailService = emailService;
        }

        // Authentication kontrolü için yardımcı metod
        private async Task<Busnies> GetAuthenticatedBusinessAsync()
        {
            var businessEmail = HttpContext.Session.GetString("BusniesEmail");
            
            if (string.IsNullOrEmpty(businessEmail))
            {
                return null;
            }

            var business = await _mongoDBService.GetBusniesByEmailAsync(businessEmail);
            if (business == null)
            {
                // Session'ı temizle
                HttpContext.Session.Clear();
                return null;
            }

            return business;
        }

        public async Task<IActionResult> Index()
        {
            // Authentication kontrolü
            var business = await GetAuthenticatedBusinessAsync();
            if (business == null)
            {
                TempData["Error"] = "İşletme paneline erişmek için giriş yapmalısınız.";
                return RedirectToAction("Login", "Busnies");
            }

            var rezervasyonlar = await _mongoDBService.GetRezervasyonlarByIsletmeIdAsync(business.Id);
            ViewBag.Busnies = business;
            ViewBag.Rezervasyonlar = rezervasyonlar;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> HizmetEkle(string ad, string sure)
        {
            // Authentication kontrolü
            var business = await GetAuthenticatedBusinessAsync();
            if (business == null)
            {
                return Json(new { success = false, message = "İşlem yapmak için giriş yapmalısınız." });
            }

            // Input validation
            if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(sure))
            {
                return Json(new { success = false, message = "Hizmet adı ve süresi boş olamaz." });
            }

            var service = new BusniesService { Ad = ad, Sure = sure };
            var result = await _mongoDBService.AddBusniesServiceAsync(business.Id, service);
            return Json(new { success = result, message = result ? "Hizmet başarıyla eklendi." : "Hizmet eklenirken hata oluştu." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> HizmetSil(string hizmetId)
        {
            // Authentication kontrolü
            var business = await GetAuthenticatedBusinessAsync();
            if (business == null)
            {
                return Json(new { success = false, message = "İşlem yapmak için giriş yapmalısınız." });
            }

            // Input validation
            if (string.IsNullOrEmpty(hizmetId))
            {
                return Json(new { success = false, message = "Hizmet ID boş olamaz." });
            }

            var result = await _mongoDBService.RemoveBusniesServiceAsync(business.Id, hizmetId);
            return Json(new { success = result, message = result ? "Hizmet başarıyla silindi." : "Hizmet silinirken hata oluştu." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
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
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> RezervasyonDurumGuncelle(string rezervasyonId, string yeniDurum)
        {
            // Authentication kontrolü
            var business = await GetAuthenticatedBusinessAsync();
            if (business == null)
            {
                return Json(new { success = false, message = "İşlem yapmak için giriş yapmalısınız." });
            }

            // Input validation
            if (string.IsNullOrEmpty(rezervasyonId) || string.IsNullOrEmpty(yeniDurum))
            {
                return Json(new { success = false, message = "Rezervasyon ID ve durum boş olamaz." });
            }

            // Geçerli durumlar
            var gecerliDurumlar = new[] { "onaylandı", "bekliyor", "iptal" };
            if (!gecerliDurumlar.Contains(yeniDurum.ToLower()))
            {
                return Json(new { success = false, message = "Geçersiz durum." });
            }

            // Rezervasyonun bu işletmeye ait olup olmadığını kontrol et
            var rezervasyon = await _mongoDBService.GetRezervasyonByIdAsync(rezervasyonId);
            if (rezervasyon == null)
            {
                return Json(new { success = false, message = "Rezervasyon bulunamadı." });
            }

            if (rezervasyon.IsletmeId != business.Id)
            {
                return Json(new { success = false, message = "Bu rezervasyonu güncelleme yetkiniz yok." });
            }

            var result = await _mongoDBService.UpdateRezervasyonDurumAsync(rezervasyonId, yeniDurum);
            
            if (result)
            {
                // Email gönder
                try
                {
                    var subject = "";
                    var body = "";
                    
                    switch (yeniDurum.ToLower())
                    {
                        case "onaylandı":
                            subject = $"Rezervasyonunuz Onaylandı - {business.Ad}";
                            body = $@"
                                <h2>Rezervasyonunuz Onaylandı!</h2>
                                <p>Sayın {rezervasyon.MusteriAdSoyad},</p>
                                <p><strong>{business.Ad}</strong> işletmesindeki rezervasyonunuz onaylanmıştır.</p>
                                <br>
                                <h3>Rezervasyon Detayları:</h3>
                                <ul>
                                    <li><strong>İşletme:</strong> {business.Ad}</li>
                                    <li><strong>Tarih:</strong> {rezervasyon.Tarih:dd.MM.yyyy}</li>
                                    <li><strong>Saat:</strong> {rezervasyon.Saat}</li>
                                    <li><strong>Hizmet:</strong> {rezervasyon.HizmetAd}</li>
                                    <li><strong>Durum:</strong> Onaylandı</li>
                                </ul>
                                <br>
                                <p>Rezervasyonunuza zamanında gelmeyi unutmayın.</p>
                                <p>İyi günler dileriz.</p>";
                            break;
                            
                        case "iptal":
                            subject = $"Rezervasyonunuz İptal Edildi - {business.Ad}";
                            body = $@"
                                <h2>Rezervasyonunuz İptal Edildi</h2>
                                <p>Sayın {rezervasyon.MusteriAdSoyad},</p>
                                <p><strong>{business.Ad}</strong> işletmesindeki rezervasyonunuz iptal edilmiştir.</p>
                                <br>
                                <h3>İptal Edilen Rezervasyon:</h3>
                                <ul>
                                    <li><strong>İşletme:</strong> {business.Ad}</li>
                                    <li><strong>Tarih:</strong> {rezervasyon.Tarih:dd.MM.yyyy}</li>
                                    <li><strong>Saat:</strong> {rezervasyon.Saat}</li>
                                    <li><strong>Hizmet:</strong> {rezervasyon.HizmetAd}</li>
                                    <li><strong>Durum:</strong> İptal Edildi</li>
                                </ul>
                                <br>
                                <p>Yeni bir rezervasyon almak için sistemimizi tekrar kullanabilirsiniz.</p>
                                <p>Anlayışınız için teşekkür ederiz.</p>";
                            break;
                            
                        case "bekliyor":
                            subject = $"Rezervasyon Durumu Güncellendi - {business.Ad}";
                            body = $@"
                                <h2>Rezervasyon Durumu Güncellendi</h2>
                                <p>Sayın {rezervasyon.MusteriAdSoyad},</p>
                                <p><strong>{business.Ad}</strong> işletmesindeki rezervasyonunuzun durumu güncellenmiştir.</p>
                                <br>
                                <h3>Rezervasyon Detayları:</h3>
                                <ul>
                                    <li><strong>İşletme:</strong> {business.Ad}</li>
                                    <li><strong>Tarih:</strong> {rezervasyon.Tarih:dd.MM.yyyy}</li>
                                    <li><strong>Saat:</strong> {rezervasyon.Saat}</li>
                                    <li><strong>Hizmet:</strong> {rezervasyon.HizmetAd}</li>
                                    <li><strong>Durum:</strong> Bekliyor</li>
                                </ul>
                                <br>
                                <p>Rezervasyonunuz inceleme aşamasındadır.</p>";
                            break;
                    }
                    
                    if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(body))
                    {
                        System.Diagnostics.Debug.WriteLine($"Email gönderimi başlatılıyor - Rezervasyon ID: {rezervasyonId}, Durum: {yeniDurum}");
                        System.Diagnostics.Debug.WriteLine($"Müşteri Email: {rezervasyon.MusteriEmail}");
                        System.Diagnostics.Debug.WriteLine($"Subject: {subject}");
                        
                        // Test için console'a yazdır
                        Console.WriteLine($"=== EMAIL GÖNDERİMİ ===");
                        Console.WriteLine($"To: {rezervasyon.MusteriEmail}");
                        Console.WriteLine($"Subject: {subject}");
                        Console.WriteLine($"Body: {body}");
                        Console.WriteLine($"========================");
                        
                        await _emailService.SendEmailAsync(rezervasyon.MusteriEmail, subject, body);
                        
                        System.Diagnostics.Debug.WriteLine($"Email gönderimi tamamlandı");
                        Console.WriteLine($"Email gönderimi tamamlandı");
                    }
                }
                catch (Exception ex)
                {
                    // Email gönderimi başarısız olsa bile rezervasyon durumu güncellenmiş olur
                    // Log hatayı ama kullanıcıya gösterme
                    System.Diagnostics.Debug.WriteLine($"Email gönderimi başarısız: {ex.Message}");
                }
            }
            
            return Json(new { success = result, message = result ? "Rezervasyon durumu güncellendi." : "Durum güncellenirken hata oluştu." });
        }



        // Rezervasyon silme
        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF koruması ekle
        public async Task<IActionResult> RezervasyonSil(string rezervasyonId)
        {
            // Authentication kontrolü
            var business = await GetAuthenticatedBusinessAsync();
            if (business == null)
            {
                return Json(new { success = false, message = "İşlem yapmak için giriş yapmalısınız." });
            }

            // Input validation
            if (string.IsNullOrEmpty(rezervasyonId))
            {
                return Json(new { success = false, message = "Rezervasyon ID boş olamaz." });
            }

            // Rezervasyonun bu işletmeye ait olup olmadığını kontrol et
            var rezervasyon = await _mongoDBService.GetRezervasyonByIdAsync(rezervasyonId);
            if (rezervasyon == null)
            {
                return Json(new { success = false, message = "Rezervasyon bulunamadı." });
            }

            if (rezervasyon.IsletmeId != business.Id)
            {
                return Json(new { success = false, message = "Bu rezervasyonu silme yetkiniz yok." });
            }

            var result = await _mongoDBService.RezervasyonSilAsync(rezervasyonId);
            return Json(new { success = result, message = result ? "Rezervasyon başarıyla silindi." : "Rezervasyon silinirken hata oluştu." });
        }

        // Test email gönderimi
        [HttpPost]
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                await _emailService.SendEmailAsync("test@example.com", "Test Email", "Bu bir test emailidir.");
                return Json(new { success = true, message = "Test email başarıyla gönderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Email gönderimi başarısız: {ex.Message}" });
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
            
            return RedirectToAction("Login", "Busnies");
        }
    }
} 