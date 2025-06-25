using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RezervasyonSistemi.Controllers;

public class BusniesController : Controller
{
    private readonly MongoDBService _mongoDBService;
    private readonly IEmailService _emailService;
    private readonly ILogger<BusniesController> _logger;
    private static readonly Dictionary<string, TempBusniesData> _tempBusnies = new();

    public BusniesController(MongoDBService mongoDBService, IEmailService emailService, ILogger<BusniesController> logger)
    {
        _mongoDBService = mongoDBService;
        _emailService = emailService;
        _logger = logger;
    }

    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            _logger.LogInformation($"Busnies login attempt for email: {email}");
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null)
            {
                TempData["Error"] = "Bu e-posta adresi bulunamadı.";
                return View();
            }
            var isValid = await _mongoDBService.ValidateBusniesCredentialsAsync(email, password);
            if (isValid)
            {
                if (!busnies.EmailDogrulandi)
                {
                    TempData["Warning"] = "E-posta adresinizi doğrulamanız gerekiyor. Lütfen e-postanızı kontrol edin.";
                    return View();
                }
                // TempData["BusniesEmail"] = email;
                HttpContext.Session.SetString("BusniesEmail", email);
                return RedirectToAction("Index", "IsletmePanel");
            }
            else
            {
                TempData["Error"] = "Şifre hatalı.";
                return View();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies login error: {ex.Message}");
            TempData["Error"] = "Giriş yapılırken bir hata oluştu.";
            return View();
        }
    }

    public IActionResult BusniesSave()
    {
        return View();
    }

    // Kayıt: Doğrulama kodu gönder
    public class BusniesRegisterRequest
    {
        public string ad { get; set; }
        public string email { get; set; }
        public string telefon { get; set; }
        public string adres { get; set; }
        public string il { get; set; }
        public string ilce { get; set; }
        public string password { get; set; }
        public List<BusniesService> hizmetler { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> SendVerificationCode([FromBody] BusniesRegisterRequest request)
    {
        try
        {
            var ad = request.ad;
            var email = request.email;
            var telefon = request.telefon;
            var adres = request.adres;
            var il = request.il;
            var ilce = request.ilce;
            var password = request.password;
            var hizmetler = request.hizmetler ?? new List<BusniesService>();
            if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return Json(new { success = false, message = "Tüm zorunlu alanları doldurunuz." });
            if (!IsValidEmail(email))
                return Json(new { success = false, message = "Geçersiz e-posta adresi formatı." });
            var existing = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (existing != null)
                return Json(new { success = false, message = "Bu e-posta adresi zaten kullanılıyor." });
            var verificationCode = GenerateVerificationCode();
            var tempId = Guid.NewGuid().ToString();
            var tempData = new TempBusniesData
            {
                Ad = ad,
                Email = email,
                Telefon = telefon,
                Adres = adres,
                Il = il,
                Ilce = ilce,
                Sifre = password,
                VerificationCode = verificationCode,
                CreatedAt = DateTime.UtcNow,
                Hizmetler = hizmetler
            };
            _tempBusnies[tempId] = tempData;
            var emailBody = $@"<h2>E-posta Doğrulama</h2><p>Merhaba {ad},</p><p>İşletme hesabınızı doğrulamak için kodunuz:</p><h3>{verificationCode}</h3><p>Bu kod 10 dakika geçerlidir.</p>";
            await _emailService.SendEmailAsync(email, "RezerveHub - İşletme E-posta Doğrulama", emailBody);
            return Json(new { success = true, tempId, email, message = "Doğrulama kodu gönderildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies SendVerificationCode error: {ex.Message}");
            return Json(new { success = false, message = "Bir hata oluştu." });
        }
    }

    // Kayıt: Kod doğrula ve kaydet
    public class BusniesVerifyRequest
    {
        public string tempUserId { get; set; }
        public string tempUserEmail { get; set; }
        public string verificationCode { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> VerifyAndSave([FromBody] BusniesVerifyRequest request)
    {
        try
        {
            var tempId = request.tempUserId;
            var tempEmail = request.tempUserEmail;
            var verificationCode = request.verificationCode;
            if (!_tempBusnies.ContainsKey(tempId))
                return Json(new { success = false, message = "Geçersiz işlem." });
            var tempData = _tempBusnies[tempId];
            if (tempData.Email != tempEmail)
                return Json(new { success = false, message = "E-posta adresi eşleşmiyor." });
            if (tempData.VerificationCode != verificationCode)
                return Json(new { success = false, message = "Geçersiz doğrulama kodu." });
            if (DateTime.UtcNow.Subtract(tempData.CreatedAt).TotalMinutes > 10)
            {
                _tempBusnies.Remove(tempId);
                return Json(new { success = false, message = "Kodun süresi doldu." });
            }
            var busnies = new Busnies
            {
                Ad = tempData.Ad,
                Email = tempData.Email,
                Telefon = tempData.Telefon,
                Adres = tempData.Adres,
                Il = tempData.Il,
                Ilce = tempData.Ilce,
                Sifre = tempData.Sifre,
                EmailDogrulamaKodu = tempData.VerificationCode,
                EmailDogrulandi = false,
                Hizmetler = tempData.Hizmetler ?? new List<BusniesService>()
            };
            var created = await _mongoDBService.CreateBusniesAsync(busnies);
            var isVerified = await _mongoDBService.VerifyBusniesEmailAsync(tempData.Email, tempData.VerificationCode);
            _tempBusnies.Remove(tempId);
            if (!isVerified)
                return Json(new { success = false, message = "E-posta doğrulama başarısız." });
            return Json(new { success = true, message = "İşletme hesabınız oluşturuldu ve e-posta doğrulandı!" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies VerifyAndSave error: {ex.Message}");
            return Json(new { success = false, message = "Kayıt sırasında hata oluştu." });
        }
    }

    // Şifremi unuttum: Kod gönder
    public class PasswordResetRequest
    {
        public string email { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> SendPasswordResetCode([FromBody] PasswordResetRequest request)
    {
        try
        {
            var email = request.email;
            if (!IsValidEmail(email))
                return Json(new { success = false, message = "Geçersiz e-posta adresi formatı." });
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null)
                return Json(new { success = false, message = "Bu e-posta adresi bulunamadı." });
            var resetCode = GenerateVerificationCode();
            var resetRequest = await _mongoDBService.CreateBusniesPasswordResetRequestAsync(email, resetCode);
            var resetId = resetRequest.Id;
            var emailBody = $@"<h2>Şifre Sıfırlama</h2><p>Merhaba {busnies.Ad},</p><p>Şifrenizi sıfırlamak için kodunuz:</p><h3>{resetCode}</h3><p>Bu kod 10 dakika geçerlidir.</p>";
            await _emailService.SendEmailAsync(email, "RezerveHub - İşletme Şifre Sıfırlama", emailBody);
            return Json(new { success = true, resetId, email, message = "Şifre sıfırlama kodu gönderildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies SendPasswordResetCode error: {ex.Message}");
            return Json(new { success = false, message = "Bir hata oluştu." });
        }
    }

    // Şifremi unuttum: Kod doğrula
    public class PasswordResetCodeVerifyRequest
    {
        public string resetId { get; set; }
        public string resetCode { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> VerifyPasswordResetCode([FromBody] PasswordResetCodeVerifyRequest request)
    {
        try
        {
            string resetId = request.resetId;
            string resetCode = request.resetCode;
            var resetRequest = await _mongoDBService.GetBusniesPasswordResetRequestByIdAsync(resetId);
            if (resetRequest == null)
                return Json(new { success = false, message = "Geçersiz işlem." });
            if (resetRequest.ResetCode != resetCode)
                return Json(new { success = false, message = "Geçersiz kod." });
            if (DateTime.UtcNow.Subtract(resetRequest.CreatedAt).TotalMinutes > 10)
            {
                await _mongoDBService.DeleteBusniesPasswordResetRequestAsync(resetId);
                return Json(new { success = false, message = "Kodun süresi doldu." });
            }
            return Json(new { success = true, message = "Kod doğru." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies VerifyPasswordResetCode error: {ex.Message}");
            return Json(new { success = false, message = $"Bir hata oluştu: {ex.Message}" });
        }
    }

    // Şifre sıfırla
    public class ResetPasswordRequest
    {
        public string resetId { get; set; }
        public string newPassword { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var resetId = request.resetId;
            var newPassword = request.newPassword;
            var resetRequest = await _mongoDBService.GetBusniesPasswordResetRequestByIdAsync(resetId);
            if (resetRequest == null)
                return Json(new { success = false, message = "Geçersiz işlem." });
            if (DateTime.UtcNow.Subtract(resetRequest.CreatedAt).TotalMinutes > 10)
            {
                await _mongoDBService.DeleteBusniesPasswordResetRequestAsync(resetId);
                return Json(new { success = false, message = "İşlem süresi doldu." });
            }
            var success = await _mongoDBService.UpdateBusniesPasswordAsync(resetRequest.Email, newPassword);
            if (success)
            {
                await _mongoDBService.DeleteBusniesPasswordResetRequestAsync(resetId);
                return Json(new { success = true, message = "Şifreniz güncellendi!" });
            }
            else
            {
                return Json(new { success = false, message = "Şifre güncellenemedi." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies ResetPassword error: {ex.Message}");
            return Json(new { success = false, message = $"Şifre sıfırlanırken hata oluştu: {ex.Message}" });
        }
    }

    // E-posta doğrulama (link ile)
    public async Task<IActionResult> VerifyEmail(string email, string code)
    {
        try
        {
            var busnies = await _mongoDBService.GetBusniesByEmailAsync(email);
            if (busnies == null)
            {
                TempData["Error"] = "Bu e-posta adresi bulunamadı.";
                return RedirectToAction("Login");
            }
            var isVerified = await _mongoDBService.VerifyBusniesEmailAsync(email, code);
            if (isVerified)
                TempData["Success"] = "E-posta adresiniz doğrulandı!";
            else
                TempData["Error"] = "Geçersiz doğrulama kodu.";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Busnies VerifyEmail error: {ex.Message}");
            TempData["Error"] = "E-posta doğrulama sırasında hata oluştu.";
        }
        return RedirectToAction("Login");
    }

    public IActionResult ForgtPasword()
    {
        return View();
    }

    private string GenerateVerificationCode()
    {
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }
    private bool IsValidEmail(string email)
    {
        try { var addr = new System.Net.Mail.MailAddress(email); return addr.Address == email; }
        catch { return false; }
    }

    // Geçici veri sınıfları
    public class TempBusniesData
    {
        public string Ad { get; set; }
        public string Email { get; set; }
        public string Telefon { get; set; }
        public string Adres { get; set; }
        public string Il { get; set; }
        public string Ilce { get; set; }
        public string Sifre { get; set; }
        public string VerificationCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<BusniesService> Hizmetler { get; set; }
    }
}
