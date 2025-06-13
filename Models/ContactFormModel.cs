using System.ComponentModel.DataAnnotations;

namespace RezervasyonSistemi.Models;

public class ContactFormModel
{
    [Required(ErrorMessage = "Ad Soyad alanı zorunludur.")]
    public string Name { get; set; }

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; }

    public string Phone { get; set; }

    [Required(ErrorMessage = "Konu seçimi zorunludur.")]
    public string Subject { get; set; }

    [Required(ErrorMessage = "Mesaj alanı zorunludur.")]
    public string Message { get; set; }
} 