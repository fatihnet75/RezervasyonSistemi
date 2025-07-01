using Microsoft.AspNetCore.Mvc;
using RezervasyonSistemi.Models;
using RezervasyonSistemi.Services;

namespace RezervasyonSistemi.Controllers
{
    public class AccountDeletionController : Controller
    {
        private readonly MongoDBService _mongoDBService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountDeletionController> _logger;

        public AccountDeletionController(MongoDBService mongoDBService, IEmailService emailService, ILogger<AccountDeletionController> logger)
        {
            _mongoDBService = mongoDBService;
            _emailService = emailService;
            _logger = logger;
        }

        // Step 1: Request account deletion
        [HttpPost]
        public async Task<IActionResult> RequestDeletion([FromBody] DeletionRequestModel model)
        {
            try
            {
                _logger.LogInformation($"Account deletion request for email: {model.Email}, UserType: {model.UserType}");

                // Validate user type
                if (model.UserType.ToLower() != "user" && model.UserType.ToLower() != "business")
                {
                    return Json(new { success = false, message = "Geçersiz kullanıcı türü." });
                }

                // Check if user/business exists
                string userId = null;
                if (model.UserType.ToLower() == "user")
                {
                    var user = await _mongoDBService.GetUserByEmailAsync(model.Email);
                    if (user == null)
                    {
                        return Json(new { success = false, message = "Bu e-posta adresi ile kayıtlı kullanıcı bulunamadı." });
                    }
                    userId = user.Id;
                }
                else
                {
                    var business = await _mongoDBService.GetBusniesByEmailAsync(model.Email);
                    if (business == null)
                    {
                        return Json(new { success = false, message = "Bu e-posta adresi ile kayıtlı işletme bulunamadı." });
                    }
                    userId = business.Id;
                }

                // Create deletion request
                var deletionRequest = await _mongoDBService.CreateAccountDeletionRequestAsync(model.Email, model.UserType, userId);

                // Send verification email
                var emailBody = GenerateDeletionEmailBody(deletionRequest.VerificationCode, model.UserType);
                var subject = model.UserType.ToLower() == "user" ? "Hesap Silme Doğrulama Kodu" : "İşletme Hesabı Silme Doğrulama Kodu";

                await _emailService.SendEmailAsync(model.Email, subject, emailBody);

                _logger.LogInformation($"Deletion verification email sent to: {model.Email}");

                return Json(new { success = true, message = "Doğrulama kodu e-posta adresinize gönderildi. Lütfen e-postanızı kontrol edin." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error requesting account deletion: {ex.Message}");
                return Json(new { success = false, message = "Hesap silme isteği gönderilirken bir hata oluştu." });
            }
        }

        // Step 2: Verify deletion code
        [HttpPost]
        public async Task<IActionResult> VerifyDeletion([FromBody] DeletionVerificationModel model)
        {
            try
            {
                _logger.LogInformation($"Verifying deletion code for email: {model.Email}");

                var isVerified = await _mongoDBService.VerifyAccountDeletionRequestAsync(model.Email, model.VerificationCode);

                if (isVerified)
                {
                    _logger.LogInformation($"Deletion code verified for: {model.Email}");
                    return Json(new { success = true, message = "Doğrulama başarılı. Hesabınız silinecek." });
                }
                else
                {
                    return Json(new { success = false, message = "Geçersiz doğrulama kodu veya süre dolmuş." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying deletion code: {ex.Message}");
                return Json(new { success = false, message = "Doğrulama sırasında bir hata oluştu." });
            }
        }

        // Step 3: Confirm and delete account
        [HttpPost]
        public async Task<IActionResult> ConfirmDeletion([FromBody] DeletionConfirmationModel model)
        {
            try
            {
                _logger.LogInformation($"Confirming account deletion for email: {model.Email}");

                // Get the verified deletion request
                var deletionRequest = await _mongoDBService.GetAccountDeletionRequestAsync(model.Email);
                if (deletionRequest == null || !deletionRequest.IsVerified)
                {
                    return Json(new { success = false, message = "Doğrulama yapılmamış. Lütfen önce doğrulama kodunu girin." });
                }

                // Delete the account
                var isDeleted = await _mongoDBService.DeleteAccountAsync(model.Email, deletionRequest.UserType);

                if (isDeleted)
                {
                    // Clean up the deletion request
                    await _mongoDBService.DeleteAccountDeletionRequestAsync(model.Email);

                    // Clear session
                    HttpContext.Session.Clear();

                    _logger.LogInformation($"Account successfully deleted for: {model.Email}");

                    return Json(new { success = true, message = "Hesabınız başarıyla silindi.", redirectUrl = "/" });
                }
                else
                {
                    return Json(new { success = false, message = "Hesap silinirken bir hata oluştu." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error confirming account deletion: {ex.Message}");
                return Json(new { success = false, message = "Hesap silme işlemi sırasında bir hata oluştu." });
            }
        }

        private string GenerateDeletionEmailBody(string verificationCode, string userType)
        {
            var userTypeText = userType.ToLower() == "user" ? "kullanıcı" : "işletme";
            
            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <h2 style='color: #e74c3c; margin: 0;'>⚠️ Hesap Silme Doğrulama</h2>
                        </div>
                        
                        <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; border-radius: 8px; padding: 15px; margin-bottom: 25px;'>
                            <p style='margin: 0; color: #856404; font-weight: bold;'>
                                ⚠️ Bu işlem geri alınamaz! {userTypeText} hesabınız ve tüm verileriniz kalıcı olarak silinecektir.
                            </p>
                        </div>
                        
                        <p style='color: #333; line-height: 1.6;'>
                            {userTypeText} hesabınızı silmek için aşağıdaki doğrulama kodunu kullanın:
                        </p>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <div style='background-color: #e74c3c; color: white; padding: 20px; border-radius: 10px; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                                {verificationCode}
                            </div>
                        </div>
                        
                        <p style='color: #666; font-size: 14px;'>
                            <strong>Önemli Notlar:</strong>
                        </p>
                        <ul style='color: #666; font-size: 14px; line-height: 1.6;'>
                            <li>Bu kod 15 dakika geçerlidir</li>
                            <li>Hesabınız silindikten sonra tüm rezervasyonlarınız da silinecektir</li>
                            <li>Bu işlem geri alınamaz</li>
                            <li>Eğer bu işlemi siz yapmadıysanız, lütfen bu e-postayı dikkate almayın</li>
                        </ul>
                        
                        <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                            <p style='color: #999; font-size: 12px;'>
                                Bu e-posta RezerveHub hesap silme işlemi için gönderilmiştir.
                            </p>
                        </div>
                    </div>
                </div>";
        }
    }

    public class DeletionRequestModel
    {
        public string Email { get; set; }
        public string UserType { get; set; }
    }

    public class DeletionVerificationModel
    {
        public string Email { get; set; }
        public string VerificationCode { get; set; }
    }

    public class DeletionConfirmationModel
    {
        public string Email { get; set; }
    }
} 