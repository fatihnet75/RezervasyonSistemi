using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RezervasyonSistemi.Models;

public class AccountDeletionRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("email")]
    public string Email { get; set; }

    [BsonElement("userType")]
    public string UserType { get; set; } // "user" or "business"

    [BsonElement("userId")]
    public string UserId { get; set; }

    [BsonElement("verificationCode")]
    public string VerificationCode { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("isVerified")]
    public bool IsVerified { get; set; } = false;

    [BsonElement("verifiedAt")]
    public DateTime? VerifiedAt { get; set; }
} 