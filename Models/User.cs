using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RezervasyonSistemi.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [Required(ErrorMessage = "Ad alanı zorunludur.")]
    [BsonElement("ad")]
    public string Ad { get; set; }

    [Required(ErrorMessage = "Soyad alanı zorunludur.")]
    [BsonElement("soyad")]
    public string Soyad { get; set; }

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [BsonElement("email")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Şifre alanı zorunludur.")]
    [BsonElement("sifre")]
    public string Sifre { get; set; }

    [BsonElement("telefon")]
    public string Telefon { get; set; }

    [BsonElement("dogumTarihi")]
    public DateTime? DogumTarihi { get; set; }

    [BsonElement("cinsiyet")]
    public string Cinsiyet { get; set; }

    [BsonElement("emailDogrulandi")]
    public bool EmailDogrulandi { get; set; } = false;

    [BsonElement("emailDogrulamaKodu")]
    public string EmailDogrulamaKodu { get; set; }

    [BsonElement("emailDogrulamaTarihi")]
    public DateTime? EmailDogrulamaTarihi { get; set; }

    [BsonElement("haberBulteni")]
    public bool HaberBulteni { get; set; } = false;

    [BsonElement("olusturmaTarihi")]
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

    [BsonElement("guncellemeTarihi")]
    public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;

    [BsonElement("sonGirisTarihi")]
    public DateTime? SonGirisTarihi { get; set; }

    [BsonElement("profilResmiUrl")]
    public string ProfilResmiUrl { get; set; }
} 