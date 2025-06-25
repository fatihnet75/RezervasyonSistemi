using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RezervasyonSistemi.Models;

public class Busnies
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [Required(ErrorMessage = "İşletme Adı zorunludur.")]
    [BsonElement("ad")]
    public string Ad { get; set; }

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [BsonElement("email")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Şifre alanı zorunludur.")]
    [BsonElement("sifre")]
    public string Sifre { get; set; }

    [BsonElement("telefon")]
    public string Telefon { get; set; }

    [BsonElement("adres")]
    public string Adres { get; set; }

    [BsonElement("emailDogrulandi")]
    public bool EmailDogrulandi { get; set; } = false;

    [BsonElement("emailDogrulamaKodu")]
    public string EmailDogrulamaKodu { get; set; }

    [BsonElement("emailDogrulamaTarihi")]
    public DateTime? EmailDogrulamaTarihi { get; set; }

    [BsonElement("olusturmaTarihi")]
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

    [BsonElement("guncellemeTarihi")]
    public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;

    [BsonElement("sonGirisTarihi")]
    public DateTime? SonGirisTarihi { get; set; }

    // İşletmeye özel ek alanlar
    [BsonElement("hizmetler")]
    public List<BusniesService> Hizmetler { get; set; } = new();

    [BsonElement("profilResmiUrl")]
    public string ProfilResmiUrl { get; set; }

    [BsonElement("il")]
    [Required(ErrorMessage = "İl (şehir) zorunludur.")]
    public string Il { get; set; }

    [BsonElement("ilce")]
    [Required(ErrorMessage = "İlçe zorunludur.")]
    public string Ilce { get; set; }

    // Profil resmi için alan eklendi
}

public class BusniesService
{
    public string Ad { get; set; }
    public string Sure { get; set; }
} 