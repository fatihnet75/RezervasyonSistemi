using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;
using RezervasyonSistemi.Controllers;
using System.Text.Json;

namespace RezervasyonSistemi.Controllers
{
    public class CustommerController : Controller
    {
        private readonly MongoDBService _mongoDBService;
        private readonly IEmailService _emailService;
        private readonly ILogger<CustommerController> _logger;
        private static readonly Dictionary<string, TempUserData> _tempUsers = new();
        private static readonly Dictionary<string, PasswordResetData> _passwordResets = new();

        public CustommerController(MongoDBService mongoDBService, IEmailService emailService, ILogger<CustommerController> logger)
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
                _logger.LogInformation($"Login attempt for email: {email}");
                
                // First check if user exists
                var user = await _mongoDBService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"Login attempt with non-existent email: {email}");
                    TempData["Error"] = "Bu e-posta adresi bulunamadı. Lütfen e-posta adresinizi kontrol ediniz.";
                    return View();
                }
                
                // Then validate credentials
                var isValid = await _mongoDBService.ValidateUserCredentialsAsync(email, password);
                if (isValid)
                {
                    await _mongoDBService.UpdateLastLoginAsync(user.Id);
                    
                    // Check if email is verified
                    if (!user.EmailDogrulandi)
                    {
                        TempData["Warning"] = "E-posta adresinizi doğrulamanız gerekiyor. Lütfen e-postanızı kontrol edin.";
                        return View();
                    }
                    
                    _logger.LogInformation($"Successful login for user: {email}");
                    // Successful login - redirect to Rezervasyon/Index
                    return RedirectToAction("Index", "Rezervasyon");
                }
                else
                {
                    _logger.LogWarning($"Failed login attempt for email: {email} - wrong password");
                    TempData["Error"] = "Şifre hatalı. Lütfen şifrenizi kontrol ediniz.";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error for email {email}: {ex.Message}");
                TempData["Error"] = "Giriş yapılırken bir hata oluştu.";
                return View();
            }
        }

        public IActionResult CustommerSave()
        {
            return View();
        }

        // Step 1: Send verification code
        [HttpPost]
        public async Task<IActionResult> SendVerificationCode(string ad, string soyad, string email, string telefon, 
            DateTime? dogumTarihi, string cinsiyet, string password, bool haberBulteni)
        {
            try
            {
                _logger.LogInformation($"Sending verification code for email: {email}");

                // Validate required fields
                if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(soyad) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "Tüm zorunlu alanları doldurunuz." });
                }

                // Validate email format
                if (!IsValidEmail(email))
                {
                    return Json(new { success = false, message = "Geçersiz e-posta adresi formatı. Lütfen doğru e-posta adresini giriniz." });
                }

                // Check if email already exists in database
                var existingUser = await _mongoDBService.GetUserByEmailAsync(email);
                if (existingUser != null)
                {
                    return Json(new { success = false, message = "Bu e-posta adresi zaten kullanılıyor. Farklı bir e-posta adresi deneyiniz." });
                }

                // Generate verification code
                var verificationCode = GenerateVerificationCode();
                
                // Store user data temporarily
                var tempUserId = Guid.NewGuid().ToString();
                var tempUserData = new TempUserData
                {
                    Ad = ad,
                    Soyad = soyad,
                    Email = email,
                    Telefon = telefon,
                    DogumTarihi = dogumTarihi,
                    Cinsiyet = cinsiyet,
                    Sifre = password,
                    HaberBulteni = haberBulteni,
                    VerificationCode = verificationCode,
                    CreatedAt = DateTime.UtcNow
                };

                _tempUsers[tempUserId] = tempUserData;

                // Send verification email
                try
                {
                    var emailBody = $@"
                        <h2>E-posta Doğrulama</h2>
                        <p>Merhaba {ad} {soyad},</p>
                        <p>RezerveHub hesabınızı doğrulamak için aşağıdaki kodu kullanın:</p>
                        <h3 style='color: #667eea; font-size: 24px;'>{verificationCode}</h3>
                        <p>Bu kod 10 dakika geçerlidir.</p>
                        <p>Teşekkürler,<br>RezerveHub Ekibi</p>";

                    await _emailService.SendEmailAsync(email, "RezerveHub - E-posta Doğrulama", emailBody);
                    _logger.LogInformation($"Verification email sent to: {email}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError($"Failed to send verification email to {email}: {emailEx.Message}");
                    
                    // Remove temp user data if email fails
                    _tempUsers.Remove(tempUserId);
                    
                    return Json(new { 
                        success = false, 
                        message = "E-posta adresinize doğrulama kodu gönderilemedi. Lütfen e-posta adresinizin doğru olduğundan emin olunuz ve tekrar deneyiniz." 
                    });
                }

                return Json(new { 
                    success = true, 
                    tempUserId = tempUserId,
                    email = email,
                    message = "Doğrulama kodu e-posta adresinize gönderildi."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendVerificationCode error for {email}: {ex.Message}");
                return Json(new { success = false, message = "Bir hata oluştu. Lütfen tekrar deneyiniz." });
            }
        }

        // Step 2: Verify code and save user
        [HttpPost]
        public async Task<IActionResult> VerifyAndSave(string tempUserId, string tempUserEmail, string verificationCode)
        {
            try
            {
                _logger.LogInformation($"Verifying code for tempUserId: {tempUserId}");

                if (!_tempUsers.ContainsKey(tempUserId))
                {
                    return Json(new { success = false, message = "Geçersiz işlem. Lütfen tekrar deneyiniz." });
                }

                var tempUserData = _tempUsers[tempUserId];

                // Check if email matches
                if (tempUserData.Email != tempUserEmail)
                {
                    return Json(new { success = false, message = "E-posta adresi eşleşmiyor." });
                }

                // Check if verification code is correct
                if (tempUserData.VerificationCode != verificationCode)
                {
                    return Json(new { success = false, message = "Geçersiz doğrulama kodu." });
                }

                // Check if code is expired (10 minutes)
                if (DateTime.UtcNow.Subtract(tempUserData.CreatedAt).TotalMinutes > 10)
                {
                    _tempUsers.Remove(tempUserId);
                    return Json(new { success = false, message = "Doğrulama kodu süresi dolmuş. Lütfen tekrar deneyiniz." });
                }

                // Create user in database with verification code
                var user = new User
                {
                    Ad = tempUserData.Ad,
                    Soyad = tempUserData.Soyad,
                    Email = tempUserData.Email,
                    Telefon = tempUserData.Telefon,
                    DogumTarihi = tempUserData.DogumTarihi,
                    Cinsiyet = tempUserData.Cinsiyet,
                    Sifre = tempUserData.Sifre,
                    HaberBulteni = tempUserData.HaberBulteni,
                    EmailDogrulamaKodu = tempUserData.VerificationCode, // Use the verification code
                    EmailDogrulandi = false, // Initially false, will be verified below
                    EmailDogrulamaTarihi = null
                };

                var createdUser = await _mongoDBService.CreateUserAsync(user);
                
                // Now verify the email using the verification code
                var isVerified = await _mongoDBService.VerifyEmailAsync(tempUserData.Email, tempUserData.VerificationCode);
                
                if (!isVerified)
                {
                    _logger.LogError($"Failed to verify email for user: {tempUserData.Email}");
                    return Json(new { success = false, message = "E-posta doğrulama işlemi başarısız oldu." });
                }
                
                // Remove temporary data
                _tempUsers.Remove(tempUserId);

                _logger.LogInformation($"User created and verified successfully: {createdUser.Id}");
                return Json(new { success = true, message = "Hesabınız başarıyla oluşturuldu ve e-posta adresiniz doğrulandı!" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"VerifyAndSave error: {ex.Message}");
                return Json(new { success = false, message = "Hesap oluşturulurken bir hata oluştu." });
            }
        }

        // Resend verification code
        [HttpPost]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendCodeRequest request)
        {
            try
            {
                _logger.LogInformation($"Resending verification code for email: {request.Email}");

                // Find temp user by email
                var tempUser = _tempUsers.Values.FirstOrDefault(u => u.Email == request.Email);
                if (tempUser == null)
                {
                    return Json(new { success = false, message = "Geçersiz işlem. Lütfen tekrar deneyiniz." });
                }

                // Generate new verification code
                var newVerificationCode = GenerateVerificationCode();
                tempUser.VerificationCode = newVerificationCode;
                tempUser.CreatedAt = DateTime.UtcNow;

                // Send new verification email
                var emailBody = $@"
                    <h2>E-posta Doğrulama</h2>
                    <p>Merhaba {tempUser.Ad} {tempUser.Soyad},</p>
                    <p>Yeni doğrulama kodunuz:</p>
                    <h3 style='color: #667eea; font-size: 24px;'>{newVerificationCode}</h3>
                    <p>Bu kod 10 dakika geçerlidir.</p>
                    <p>Teşekkürler,<br>RezerveHub Ekibi</p>";

                await _emailService.SendEmailAsync(request.Email, "RezerveHub - Yeni Doğrulama Kodu", emailBody);
                
                _logger.LogInformation($"New verification code sent to: {request.Email}");
                return Json(new { success = true, message = "Yeni doğrulama kodu gönderildi." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"ResendVerificationCode error: {ex.Message}");
                return Json(new { success = false, message = "Doğrulama kodu gönderilemedi." });
            }
        }

        public async Task<IActionResult> VerifyEmail(string email, string code)
        {
            try
            {
                _logger.LogInformation($"Email verification attempt for: {email} with code: {code}");
                
                // First check if user exists
                var user = await _mongoDBService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"Email verification attempt with non-existent email: {email}");
                    TempData["Error"] = "Bu e-posta adresi bulunamadı. Lütfen e-posta adresinizi kontrol ediniz.";
                    return RedirectToAction("Login");
                }
                
                var isVerified = await _mongoDBService.VerifyEmailAsync(email, code);
                if (isVerified)
                {
                    _logger.LogInformation($"Email verified successfully for: {email}");
                    TempData["Success"] = "E-posta adresiniz başarıyla doğrulandı! Artık giriş yapabilirsiniz.";
                }
                else
                {
                    _logger.LogWarning($"Email verification failed for: {email} with code: {code}");
                    TempData["Error"] = "Geçersiz doğrulama kodu. Lütfen doğru kodu girdiğinizden emin olunuz.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Email verification error for {email}: {ex.Message}");
                TempData["Error"] = "E-posta doğrulama sırasında bir hata oluştu.";
            }

            return RedirectToAction("Login");
        }

        public IActionResult Forgotpasword()
        {
            return View();
        }

        // Step 1: Send password reset code
        [HttpPost]
        public async Task<IActionResult> SendPasswordResetCode(string email)
        {
            try
            {
                _logger.LogInformation($"Sending password reset code for email: {email}");

                // Validate email format
                if (!IsValidEmail(email))
                {
                    return Json(new { success = false, message = "Geçersiz e-posta adresi formatı." });
                }

                // Check if user exists
                var user = await _mongoDBService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    return Json(new { success = false, message = "Bu e-posta adresi bulunamadı. Lütfen e-posta adresinizi kontrol ediniz." });
                }

                // Generate reset code
                var resetCode = GenerateVerificationCode();
                
                // Store reset data temporarily
                var resetId = Guid.NewGuid().ToString();
                var resetData = new PasswordResetData
                {
                    Email = email,
                    ResetCode = resetCode,
                    CreatedAt = DateTime.UtcNow
                };

                _passwordResets[resetId] = resetData;

                // Send reset email
                try
                {
                    var emailBody = $@"
                        <h2>Şifre Sıfırlama</h2>
                        <p>Merhaba {user.Ad} {user.Soyad},</p>
                        <p>Şifrenizi sıfırlamak için aşağıdaki kodu kullanın:</p>
                        <h3 style='color: #667eea; font-size: 24px;'>{resetCode}</h3>
                        <p>Bu kod 10 dakika geçerlidir.</p>
                        <p>Eğer bu işlemi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                        <p>Teşekkürler,<br>RezerveHub Ekibi</p>";

                    await _emailService.SendEmailAsync(email, "RezerveHub - Şifre Sıfırlama", emailBody);
                    _logger.LogInformation($"Password reset email sent to: {email}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError($"Failed to send password reset email to {email}: {emailEx.Message}");
                    _passwordResets.Remove(resetId);
                    
                    return Json(new { 
                        success = false, 
                        message = "Şifre sıfırlama kodu gönderilemedi. Lütfen e-posta adresinizin doğru olduğundan emin olunuz." 
                    });
                }

                return Json(new { 
                    success = true, 
                    resetId = resetId,
                    email = email,
                    message = "Şifre sıfırlama kodu e-posta adresinize gönderildi."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendPasswordResetCode error for {email}: {ex.Message}");
                return Json(new { success = false, message = "Bir hata oluştu. Lütfen tekrar deneyiniz." });
            }
        }

        // Step 2: Verify reset code
        [HttpPost]
        public async Task<IActionResult> VerifyPasswordResetCode(string resetId, string resetCode)
        {
            try
            {
                _logger.LogInformation($"Verifying password reset code for resetId: {resetId}");

                if (!_passwordResets.ContainsKey(resetId))
                {
                    return Json(new { success = false, message = "Geçersiz işlem. Lütfen tekrar deneyiniz." });
                }

                var resetData = _passwordResets[resetId];

                // Check if code is correct
                if (resetData.ResetCode != resetCode)
                {
                    return Json(new { success = false, message = "Geçersiz doğrulama kodu." });
                }

                // Check if code is expired (10 minutes)
                if (DateTime.UtcNow.Subtract(resetData.CreatedAt).TotalMinutes > 10)
                {
                    _passwordResets.Remove(resetId);
                    return Json(new { success = false, message = "Doğrulama kodu süresi dolmuş. Lütfen tekrar deneyiniz." });
                }

                return Json(new { success = true, message = "Doğrulama kodu doğru." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"VerifyPasswordResetCode error: {ex.Message}");
                return Json(new { success = false, message = "Bir hata oluştu. Lütfen tekrar deneyiniz." });
            }
        }

        // Step 3: Reset password
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string resetId, string newPassword)
        {
            try
            {
                _logger.LogInformation($"Resetting password for resetId: {resetId}");

                if (!_passwordResets.ContainsKey(resetId))
                {
                    return Json(new { success = false, message = "Geçersiz işlem. Lütfen tekrar deneyiniz." });
                }

                var resetData = _passwordResets[resetId];

                // Check if code is expired (10 minutes)
                if (DateTime.UtcNow.Subtract(resetData.CreatedAt).TotalMinutes > 10)
                {
                    _passwordResets.Remove(resetId);
                    return Json(new { success = false, message = "İşlem süresi dolmuş. Lütfen tekrar deneyiniz." });
                }

                // Update password in database
                var success = await _mongoDBService.UpdatePasswordAsync(resetData.Email, newPassword);
                
                if (success)
                {
                    // Remove reset data
                    _passwordResets.Remove(resetId);
                    
                    _logger.LogInformation($"Password reset successfully for: {resetData.Email}");
                    return Json(new { success = true, message = "Şifreniz başarıyla güncellendi!" });
                }
                else
                {
                    return Json(new { success = false, message = "Şifre güncellenirken bir hata oluştu." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ResetPassword error: {ex.Message}");
                return Json(new { success = false, message = "Şifre sıfırlanırken bir hata oluştu." });
            }
        }

        // Resend password reset code
        [HttpPost]
        public async Task<IActionResult> ResendPasswordResetCode([FromBody] ResendCodeRequest request)
        {
            try
            {
                _logger.LogInformation($"Resending password reset code for email: {request.Email}");

                // Find reset data by email
                var resetData = _passwordResets.Values.FirstOrDefault(r => r.Email == request.Email);
                if (resetData == null)
                {
                    return Json(new { success = false, message = "Geçersiz işlem. Lütfen tekrar deneyiniz." });
                }

                // Get user info
                var user = await _mongoDBService.GetUserByEmailAsync(request.Email);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });
                }

                // Generate new reset code
                var newResetCode = GenerateVerificationCode();
                resetData.ResetCode = newResetCode;
                resetData.CreatedAt = DateTime.UtcNow;

                // Send new reset email
                var emailBody = $@"
                    <h2>Şifre Sıfırlama</h2>
                    <p>Merhaba {user.Ad} {user.Soyad},</p>
                    <p>Yeni şifre sıfırlama kodunuz:</p>
                    <h3 style='color: #667eea; font-size: 24px;'>{newResetCode}</h3>
                    <p>Bu kod 10 dakika geçerlidir.</p>
                    <p>Eğer bu işlemi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                    <p>Teşekkürler,<br>RezerveHub Ekibi</p>";

                await _emailService.SendEmailAsync(request.Email, "RezerveHub - Yeni Şifre Sıfırlama Kodu", emailBody);
                
                _logger.LogInformation($"New password reset code sent to: {request.Email}");
                return Json(new { success = true, message = "Yeni şifre sıfırlama kodu gönderildi." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"ResendPasswordResetCode error: {ex.Message}");
                return Json(new { success = false, message = "Şifre sıfırlama kodu gönderilemedi." });
            }
        }

        // Test endpoint to verify MongoDB connection
        public async Task<IActionResult> TestMongoDB()
        {
            try
            {
                _logger.LogInformation("Testing MongoDB connection...");
                
                // Test creating a simple user
                var testUser = new User
                {
                    Ad = "Test",
                    Soyad = "User",
                    Email = $"test{DateTime.Now.Ticks}@test.com",
                    Sifre = "test123",
                    Telefon = "5551234567",
                    DogumTarihi = DateTime.Now.AddYears(-25),
                    Cinsiyet = "male",
                    HaberBulteni = false
                };

                var createdUser = await _mongoDBService.CreateUserAsync(testUser);
                
                // Test retrieving the user
                var retrievedUser = await _mongoDBService.GetUserByEmailAsync(testUser.Email);
                
                if (retrievedUser != null && retrievedUser.Id == createdUser.Id)
                {
                    return Json(new { 
                        success = true, 
                        message = "MongoDB connection and operations working correctly",
                        userId = createdUser.Id,
                        email = createdUser.Email
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = "MongoDB operations failed - user retrieval failed" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"MongoDB test failed: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = $"MongoDB test failed: {ex.Message}" 
                });
            }
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    // Helper classes
    public class TempUserData
    {
        public string Ad { get; set; }
        public string Soyad { get; set; }
        public string Email { get; set; }
        public string Telefon { get; set; }
        public DateTime? DogumTarihi { get; set; }
        public string Cinsiyet { get; set; }
        public string Sifre { get; set; }
        public bool HaberBulteni { get; set; }
        public string VerificationCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PasswordResetData
    {
        public string Email { get; set; }
        public string ResetCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ResendCodeRequest
    {
        public string Email { get; set; }
    }
}
