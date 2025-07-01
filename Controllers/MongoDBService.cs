using MongoDB.Driver;
using MongoDB.Bson;
using RezervasyonSistemi.Models;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

namespace RezervasyonSistemi.Controllers
{
    public class MongoDBService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Busnies> _busnies;
        private readonly IMongoCollection<BusniesPasswordResetRequest> _busniesPasswordResetRequests;
        private readonly IMongoCollection<Rezervasyon> _rezervasyonlar;
        private readonly IMongoCollection<AccountDeletionRequest> _accountDeletionRequests;
        private readonly ILogger<MongoDBService> _logger;

        public MongoDBService(IConfiguration configuration, ILogger<MongoDBService> logger)
        {
            _logger = logger;
            
            try
            {
                // Önce çevresel değişkenden al, yoksa appsettings'ten al
                var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MongoDB");
                var connectionString = !string.IsNullOrEmpty(envConnectionString)
                    ? envConnectionString
                    : configuration.GetConnectionString("MongoDB");
                _logger.LogInformation("Initializing MongoDB connection...");
                
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase("Rezervasyondb");
                _users = database.GetCollection<User>("User");
                _busnies = database.GetCollection<Busnies>("Busnies");
                _busniesPasswordResetRequests = database.GetCollection<BusniesPasswordResetRequest>("BusniesPasswordResetRequest");
                _rezervasyonlar = database.GetCollection<Rezervasyon>("Rezervasyon");
                _accountDeletionRequests = database.GetCollection<AccountDeletionRequest>("AccountDeletionRequest");
                
                // Test the connection
                var pingResult = database.RunCommandAsync((Command<BsonDocument>)"{ping:1}").Result;
                _logger.LogInformation("MongoDB connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"MongoDB connection failed: {ex.Message}");
                throw new Exception($"MongoDB bağlantısı kurulamadı: {ex.Message}");
            }
        }

        public async Task<User> CreateUserAsync(User user)
        {
            try
            {
                _logger.LogInformation($"Creating user with email: {user.Email}");
                
                // Check if email already exists
                var existingUser = await _users.Find(u => u.Email == user.Email).FirstOrDefaultAsync();
                if (existingUser != null)
                {
                    _logger.LogWarning($"Email already exists: {user.Email}");
                    throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
                }

                // Hash the password
                user.Sifre = BCrypt.Net.BCrypt.HashPassword(user.Sifre);
                
                // Generate email verification code only if not provided
                if (string.IsNullOrEmpty(user.EmailDogrulamaKodu))
                {
                    user.EmailDogrulamaKodu = GenerateVerificationCode();
                }
                
                // EmailDogrulandi is already set by the caller, no need to override
                user.OlusturmaTarihi = DateTime.UtcNow;
                user.GuncellemeTarihi = DateTime.UtcNow;

                _logger.LogInformation($"Inserting user into MongoDB collection: {_users.CollectionNamespace}");
                await _users.InsertOneAsync(user);
                
                _logger.LogInformation($"User created successfully with ID: {user.Id}");
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating user {user.Email}: {ex.Message}");
                throw new Exception($"Kullanıcı oluşturulurken hata: {ex.Message}");
            }
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"E-posta ile kullanıcı aranırken hata: {ex.Message}");
            }
        }

        public async Task<User> GetUserByIdAsync(string id)
        {
            try
            {
                return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"ID ile kullanıcı aranırken hata: {ex.Message}");
            }
        }

        public async Task<bool> VerifyEmailAsync(string email, string verificationCode)
        {
            try
            {
                var filter = Builders<User>.Filter.And(
                    Builders<User>.Filter.Eq(u => u.Email, email),
                    Builders<User>.Filter.Eq(u => u.EmailDogrulamaKodu, verificationCode)
                );

                var update = Builders<User>.Update
                    .Set(u => u.EmailDogrulandi, true)
                    .Set(u => u.EmailDogrulamaTarihi, DateTime.UtcNow)
                    .Set(u => u.GuncellemeTarihi, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"E-posta doğrulanırken hata: {ex.Message}");
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                user.GuncellemeTarihi = DateTime.UtcNow;
                var result = await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Kullanıcı güncellenirken hata: {ex.Message}");
            }
        }

        public async Task<bool> UpdateLastLoginAsync(string userId)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
                var update = Builders<User>.Update
                    .Set(u => u.SonGirisTarihi, DateTime.UtcNow)
                    .Set(u => u.GuncellemeTarihi, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Son giriş tarihi güncellenirken hata: {ex.Message}");
            }
        }

        public async Task<bool> UpdateUserProfileImageAsync(string userId, string newImageUrl)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
                var update = Builders<User>.Update
                    .Set(u => u.ProfilResmiUrl, newImageUrl)
                    .Set(u => u.GuncellemeTarihi, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating user profile image for {userId}: {ex.Message}");
                throw new Exception($"Kullanıcı profil resmi güncellenirken hata: {ex.Message}");
            }
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            try
            {
                _logger.LogInformation($"Validating credentials for email: {email}");
                
                var user = await GetUserByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"User not found for email: {email}");
                    return false;
                }

                _logger.LogInformation($"User found for email: {email}, checking password...");
                var isValid = BCrypt.Net.BCrypt.Verify(password, user.Sifre);
                _logger.LogInformation($"Password verification result for {email}: {isValid}");
                
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating credentials for {email}: {ex.Message}");
                throw new Exception($"Kullanıcı kimlik doğrulaması yapılırken hata: {ex.Message}");
            }
        }

        public async Task<bool> UpdatePasswordAsync(string email, string newPassword)
        {
            try
            {
                _logger.LogInformation($"Updating password for email: {email}");
                
                var filter = Builders<User>.Filter.Eq(u => u.Email, email);
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                
                var update = Builders<User>.Update
                    .Set(u => u.Sifre, hashedPassword)
                    .Set(u => u.GuncellemeTarihi, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(filter, update);
                
                if (result.ModifiedCount > 0)
                {
                    _logger.LogInformation($"Password updated successfully for email: {email}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"No user found to update password for email: {email}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating password for {email}: {ex.Message}");
                throw new Exception($"Şifre güncellenirken hata: {ex.Message}");
            }
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        public async Task<Busnies> CreateBusniesAsync(Busnies busnies)
        {
            try
            {
                _logger.LogInformation($"Creating busnies with email: {busnies.Email}");
                var existing = await _busnies.Find(b => b.Email == busnies.Email).FirstOrDefaultAsync();
                if (existing != null)
                    throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
                busnies.Sifre = BCrypt.Net.BCrypt.HashPassword(busnies.Sifre);
                busnies.OlusturmaTarihi = DateTime.UtcNow;
                busnies.GuncellemeTarihi = DateTime.UtcNow;
                await _busnies.InsertOneAsync(busnies);
                return busnies;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating busnies {busnies.Email}: {ex.Message}");
                throw new Exception($"İşletme oluşturulurken hata: {ex.Message}");
            }
        }

        public async Task<Busnies> GetBusniesByEmailAsync(string email)
        {
            try { return await _busnies.Find(b => b.Email == email).FirstOrDefaultAsync(); }
            catch (Exception ex) { throw new Exception($"E-posta ile işletme aranırken hata: {ex.Message}"); }
        }

        public async Task<bool> ValidateBusniesCredentialsAsync(string email, string password)
        {
            try
            {
                var busnies = await GetBusniesByEmailAsync(email);
                if (busnies == null) return false;
                return BCrypt.Net.BCrypt.Verify(password, busnies.Sifre);
            }
            catch (Exception ex) { throw new Exception($"İşletme kimlik doğrulaması yapılırken hata: {ex.Message}"); }
        }

        public async Task<bool> UpdateBusniesPasswordAsync(string email, string newPassword)
        {
            try
            {
                var filter = Builders<Busnies>.Filter.Eq(b => b.Email, email);
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
                var update = Builders<Busnies>.Update.Set(b => b.Sifre, hashedPassword).Set(b => b.GuncellemeTarihi, DateTime.UtcNow);
                var result = await _busnies.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex) { throw new Exception($"İşletme şifresi güncellenirken hata: {ex.Message}"); }
        }

        public async Task<bool> VerifyBusniesEmailAsync(string email, string verificationCode)
        {
            try
            {
                var filter = Builders<Busnies>.Filter.And(
                    Builders<Busnies>.Filter.Eq(b => b.Email, email),
                    Builders<Busnies>.Filter.Eq(b => b.EmailDogrulamaKodu, verificationCode)
                );
                var update = Builders<Busnies>.Update
                    .Set(b => b.EmailDogrulandi, true)
                    .Set(b => b.EmailDogrulamaTarihi, DateTime.UtcNow)
                    .Set(b => b.GuncellemeTarihi, DateTime.UtcNow);
                var result = await _busnies.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex) { throw new Exception($"İşletme e-posta doğrulanırken hata: {ex.Message}"); }
        }

        public async Task<BusniesPasswordResetRequest> CreateBusniesPasswordResetRequestAsync(string email, string resetCode)
        {
            var request = new BusniesPasswordResetRequest
            {
                Email = email,
                ResetCode = resetCode,
                CreatedAt = DateTime.UtcNow
            };
            await _busniesPasswordResetRequests.InsertOneAsync(request);
            return request;
        }

        public async Task<BusniesPasswordResetRequest> GetBusniesPasswordResetRequestByIdAsync(string id)
        {
            return await _busniesPasswordResetRequests.Find(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task DeleteBusniesPasswordResetRequestAsync(string id)
        {
            await _busniesPasswordResetRequests.DeleteOneAsync(r => r.Id == id);
        }

        // İşletmeye ait rezervasyonları getir
        public async Task<List<Rezervasyon>> GetRezervasyonlarByIsletmeIdAsync(string isletmeId)
        {
            return await _rezervasyonlar.Find(r => r.IsletmeId == isletmeId).ToListAsync();
        }

        // İşletmeye yeni hizmet ekle
        public async Task<bool> AddBusniesServiceAsync(string busniesId, BusniesService service)
        {
            var filter = Builders<Busnies>.Filter.Eq(b => b.Id, busniesId);
            var update = Builders<Busnies>.Update.Push(b => b.Hizmetler, service);
            var result = await _busnies.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // İşletmeden hizmet sil
        public async Task<bool> RemoveBusniesServiceAsync(string busniesId, string serviceAd)
        {
            var filter = Builders<Busnies>.Filter.Eq(b => b.Id, busniesId);
            var update = Builders<Busnies>.Update.PullFilter(b => b.Hizmetler, s => s.Ad == serviceAd);
            var result = await _busnies.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // İşletme hizmetini güncelle
        public async Task<bool> UpdateBusniesServiceAsync(string busniesId, string oldServiceAd, BusniesService updatedService)
        {
            var filter = Builders<Busnies>.Filter.And(
                Builders<Busnies>.Filter.Eq(b => b.Id, busniesId),
                Builders<Busnies>.Filter.ElemMatch(b => b.Hizmetler, s => s.Ad == oldServiceAd)
            );
            var update = Builders<Busnies>.Update.Set("Hizmetler.$", updatedService);
            var result = await _busnies.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        // İşletme profil resmini güncelle
        public async Task<bool> UpdateBusniesProfileImageAsync(string busniesId, string newImageUrl)
        {
            var filter = Builders<Busnies>.Filter.Eq(b => b.Id, busniesId);
            var update = Builders<Busnies>.Update.Set(b => b.ProfilResmiUrl, newImageUrl);
            var result = await _busnies.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<List<Busnies>> GetAllBusniesAsync()
        {
            return await _busnies.Find(_ => true).ToListAsync();
        }

        public async Task<Busnies> GetBusniesByIdAsync(string id)
        {
            return await _busnies.Find(b => b.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Rezervasyon> CreateRezervasyonAsync(Rezervasyon rezervasyon)
        {
            _logger.LogInformation($"CreateRezervasyonAsync - Kaydedilen tarih: {rezervasyon.Tarih:yyyy-MM-dd}, Saat: {rezervasyon.Saat}");
            
            await _rezervasyonlar.InsertOneAsync(rezervasyon);
            
            _logger.LogInformation($"CreateRezervasyonAsync - Rezervasyon başarıyla kaydedildi. ID: {rezervasyon.Id}");
            return rezervasyon;
        }

        public async Task<List<Rezervasyon>> GetRezervasyonlarByMusteriEmailAsync(string musteriEmail)
        {
            try
            {
                _logger.LogInformation($"Searching reservations for email: {musteriEmail}");
                
                // Basit eşitlik kontrolü kullan
                var filter = Builders<Rezervasyon>.Filter.Eq(r => r.MusteriEmail, musteriEmail);
                var result = await _rezervasyonlar.Find(filter).ToListAsync();
                
                _logger.LogInformation($"Found {result.Count} reservations for email: {musteriEmail}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting reservations for email {musteriEmail}: {ex.Message}");
                throw new Exception($"Rezervasyonlar getirilirken hata: {ex.Message}");
            }
        }

        public async Task<Rezervasyon> GetRezervasyonByIdAsync(string rezervasyonId)
        {
            try
            {
                return await _rezervasyonlar.Find(r => r.Id == rezervasyonId).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting reservation by ID {rezervasyonId}: {ex.Message}");
                throw new Exception($"Rezervasyon alınırken hata: {ex.Message}");
            }
        }

        public async Task<bool> UpdateRezervasyonDurumAsync(string rezervasyonId, string yeniDurum)
        {
            var filter = Builders<Rezervasyon>.Filter.Eq(r => r.Id, rezervasyonId);
            var update = Builders<Rezervasyon>.Update.Set(r => r.Durum, yeniDurum);
            var result = await _rezervasyonlar.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }



        public async Task<bool> DeleteRezervasyonAsync(string rezervasyonId)
        {
            try
            {
                var filter = Builders<Rezervasyon>.Filter.Eq(r => r.Id, rezervasyonId);
                var result = await _rezervasyonlar.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting reservation {rezervasyonId}: {ex.Message}");
                throw new Exception($"Rezervasyon silinirken hata: {ex.Message}");
            }
        }

        public async Task<bool> RezervasyonSilAsync(string rezervasyonId)
        {
            try
            {
                var filter = Builders<Rezervasyon>.Filter.Eq(r => r.Id, rezervasyonId);
                var result = await _rezervasyonlar.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting reservation {rezervasyonId}: {ex.Message}");
                throw new Exception($"Rezervasyon silinirken hata: {ex.Message}");
            }
        }

        public async Task<List<Rezervasyon>> GetAllRezervasyonlarAsync()
        {
            return await _rezervasyonlar.Find(_ => true).ToListAsync();
        }

        // Belirli bir işletme için belirli tarih ve saatte dolu olan rezervasyonları getir
        public async Task<List<Rezervasyon>> GetDoluSaatlerAsync(string isletmeId, DateTime tarih)
        {
            try
            {
                _logger.LogInformation($"GetDoluSaatlerAsync - İşletme: {isletmeId}, Tarih: {tarih:yyyy-MM-dd}");
                
                // UTC tarih oluştur
                var utcTarih = new DateTime(tarih.Year, tarih.Month, tarih.Day, 0, 0, 0, DateTimeKind.Utc);
                
                _logger.LogInformation($"GetDoluSaatlerAsync - Aranan tarih: {tarih:yyyy-MM-dd}, UTC tarih: {utcTarih:yyyy-MM-dd}");
                
                var filter = Builders<Rezervasyon>.Filter.And(
                    Builders<Rezervasyon>.Filter.Eq(r => r.IsletmeId, isletmeId),
                    Builders<Rezervasyon>.Filter.Eq(r => r.Tarih, utcTarih),
                    Builders<Rezervasyon>.Filter.Ne(r => r.Durum, "iptal") // İptal edilen rezervasyonları sayma
                );
                
                var result = await _rezervasyonlar.Find(filter).ToListAsync();
                _logger.LogInformation($"GetDoluSaatlerAsync - Bulunan rezervasyon sayısı: {result.Count}");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting busy hours for business {isletmeId} on {tarih}: {ex.Message}");
                throw new Exception($"Dolu saatler alınırken hata: {ex.Message}");
            }
        }

        // Belirli bir işletme için belirli tarihte dolu olan saatleri hesapla (hizmet süresine göre)
        public async Task<List<string>> GetDoluSaatlerListesiAsync(string isletmeId, DateTime tarih)
        {
            try
            {
                var doluRezervasyonlar = await GetDoluSaatlerAsync(isletmeId, tarih);
                var doluSaatler = new List<string>();

                _logger.LogInformation($"İşletme {isletmeId} için {tarih:yyyy-MM-dd} tarihinde {doluRezervasyonlar.Count} rezervasyon bulundu");

                foreach (var rezervasyon in doluRezervasyonlar)
                {
                    _logger.LogInformation($"Rezervasyon: {rezervasyon.Saat}, Hizmet: {rezervasyon.HizmetAd}, Süre: {rezervasyon.HizmetSuresi}, Durum: {rezervasyon.Durum}");
                    
                    // Sadece rezervasyonun başlangıç saatini dolu olarak işaretle
                    if (TimeSpan.TryParse(rezervasyon.Saat, out TimeSpan baslangicSaati))
                    {
                        // Sadece tam saatleri kontrol et (8:00-19:00)
                        if (baslangicSaati.Hours >= 8 && baslangicSaati.Hours < 20 && baslangicSaati.Minutes == 0)
                        {
                            var slotString = $"{baslangicSaati.Hours:D2}:{baslangicSaati.Minutes:D2}";
                            
                            if (!doluSaatler.Contains(slotString))
                            {
                                doluSaatler.Add(slotString);
                            }
                        }
                    }
                }

                var result = doluSaatler.OrderBy(s => s).ToList();
                _logger.LogInformation($"Hesaplanan dolu saatler: {string.Join(", ", result)}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating busy hours for business {isletmeId} on {tarih}: {ex.Message}");
                throw new Exception($"Dolu saatler hesaplanırken hata: {ex.Message}");
            }
        }

        // Belirli bir işletme için belirli saatte rezervasyon alınabilir mi kontrol et
        public async Task<bool> IsSaatMusaitAsync(string isletmeId, DateTime tarih, string saat, int hizmetSuresiDakika)
        {
            try
            {
                var doluRezervasyonlar = await GetDoluSaatlerAsync(isletmeId, tarih);
                
                if (TimeSpan.TryParse(saat, out TimeSpan yeniRezervasyonBaslangic))
                {
                    // Sadece tam saatleri kontrol et
                    if (yeniRezervasyonBaslangic.Minutes != 0)
                    {
                        return false; // Sadece tam saatlerde rezervasyon alınabilir
                    }
                    
                    // Aynı saatte başka rezervasyon var mı kontrol et
                    foreach (var rezervasyon in doluRezervasyonlar)
                    {
                        if (TimeSpan.TryParse(rezervasyon.Saat, out TimeSpan mevcutBaslangic))
                        {
                            // Aynı saatte rezervasyon var mı kontrol et
                            if (yeniRezervasyonBaslangic == mevcutBaslangic)
                            {
                                return false; // Aynı saatte rezervasyon var, müsait değil
                            }
                        }
                    }
                }
                
                return true; // Müsait
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking availability for business {isletmeId} on {tarih} at {saat}: {ex.Message}");
                return false; // Hata durumunda güvenli tarafta kal
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                return await _users.Find(_ => true).ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Tüm kullanıcılar alınırken hata: {ex.Message}");
            }
        }

        // Account Deletion Methods
        public async Task<AccountDeletionRequest> CreateAccountDeletionRequestAsync(string email, string userType, string userId)
        {
            try
            {
                // Delete any existing requests for this user
                var existingFilter = Builders<AccountDeletionRequest>.Filter.Eq(r => r.Email, email);
                await _accountDeletionRequests.DeleteManyAsync(existingFilter);

                var verificationCode = GenerateDeletionVerificationCode();
                var request = new AccountDeletionRequest
                {
                    Email = email,
                    UserType = userType,
                    UserId = userId,
                    VerificationCode = verificationCode,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15 minutes expiry
                    IsVerified = false
                };

                await _accountDeletionRequests.InsertOneAsync(request);
                return request;
            }
            catch (Exception ex)
            {
                throw new Exception($"Hesap silme isteği oluşturulurken hata: {ex.Message}");
            }
        }

        public async Task<AccountDeletionRequest> GetAccountDeletionRequestAsync(string email)
        {
            try
            {
                var filter = Builders<AccountDeletionRequest>.Filter.Eq(r => r.Email, email);
                return await _accountDeletionRequests.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Hesap silme isteği aranırken hata: {ex.Message}");
            }
        }

        public async Task<bool> VerifyAccountDeletionRequestAsync(string email, string verificationCode)
        {
            try
            {
                var request = await GetAccountDeletionRequestAsync(email);
                if (request == null) return false;

                if (DateTime.UtcNow > request.ExpiresAt)
                {
                    await _accountDeletionRequests.DeleteOneAsync(r => r.Id == request.Id);
                    return false;
                }

                if (request.VerificationCode != verificationCode) return false;

                var update = Builders<AccountDeletionRequest>.Update
                    .Set(r => r.IsVerified, true)
                    .Set(r => r.VerifiedAt, DateTime.UtcNow);

                var result = await _accountDeletionRequests.UpdateOneAsync(r => r.Id == request.Id, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Hesap silme doğrulaması yapılırken hata: {ex.Message}");
            }
        }

        public async Task<bool> DeleteAccountAsync(string email, string userType)
        {
            try
            {
                if (userType.ToLower() == "user")
                {
                    // Delete user's reservations first
                    var reservationFilter = Builders<Rezervasyon>.Filter.Eq(r => r.MusteriEmail, email);
                    await _rezervasyonlar.DeleteManyAsync(reservationFilter);

                    // Delete user
                    var userFilter = Builders<User>.Filter.Eq(u => u.Email, email);
                    var result = await _users.DeleteOneAsync(userFilter);
                    return result.DeletedCount > 0;
                }
                else if (userType.ToLower() == "business")
                {
                    // Get business first to get its ID
                    var business = await GetBusniesByEmailAsync(email);
                    if (business == null) return false;

                    // Delete business's reservations first
                    var reservationFilter = Builders<Rezervasyon>.Filter.Eq(r => r.IsletmeId, business.Id);
                    await _rezervasyonlar.DeleteManyAsync(reservationFilter);

                    // Delete business
                    var businessFilter = Builders<Busnies>.Filter.Eq(b => b.Email, email);
                    var result = await _busnies.DeleteOneAsync(businessFilter);
                    return result.DeletedCount > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Hesap silinirken hata: {ex.Message}");
            }
        }

        public async Task<bool> DeleteAccountDeletionRequestAsync(string email)
        {
            try
            {
                var filter = Builders<AccountDeletionRequest>.Filter.Eq(r => r.Email, email);
                var result = await _accountDeletionRequests.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Hesap silme isteği silinirken hata: {ex.Message}");
            }
        }

        private string GenerateDeletionVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}