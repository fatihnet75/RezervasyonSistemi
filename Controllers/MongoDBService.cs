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
        private readonly ILogger<MongoDBService> _logger;

        public MongoDBService(IConfiguration configuration, ILogger<MongoDBService> logger)
        {
            _logger = logger;
            
            try
            {
                var connectionString = configuration.GetConnectionString("MongoDB");
                _logger.LogInformation("Initializing MongoDB connection...");
                
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase("Rezervasyondb");
                _users = database.GetCollection<User>("User");
                _busnies = database.GetCollection<Busnies>("Busnies");
                _busniesPasswordResetRequests = database.GetCollection<BusniesPasswordResetRequest>("BusniesPasswordResetRequest");
                
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

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            try
            {
                var user = await GetUserByEmailAsync(email);
                if (user == null) return false;

                return BCrypt.Net.BCrypt.Verify(password, user.Sifre);
            }
            catch (Exception ex)
            {
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
    }
}