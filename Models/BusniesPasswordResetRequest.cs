using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace RezervasyonSistemi.Models
{
    public class BusniesPasswordResetRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("resetCode")]
        public string ResetCode { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
} 