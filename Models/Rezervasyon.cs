using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace RezervasyonSistemi.Models
{
    public class Rezervasyon
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("isletmeId")]
        public string IsletmeId { get; set; } // İşletme Id

        [BsonElement("musteriAdSoyad")]
        public string MusteriAdSoyad { get; set; }

        [BsonElement("musteriEmail")]
        public string MusteriEmail { get; set; }

        [BsonElement("tarih")]
        public DateTime Tarih { get; set; }

        [BsonElement("saat")]
        public string Saat { get; set; }

        [BsonElement("hizmetAd")]
        public string HizmetAd { get; set; }

        [BsonElement("hizmetSuresi")]
        public string HizmetSuresi { get; set; }

        [BsonElement("durum")]
        public string Durum { get; set; } = "Bekliyor";

        [BsonElement("notlar")]
        public string Notlar { get; set; } = "";

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 