# Rezervasyon Sistemi

Modern ve kullanıcı dostu bir rezervasyon yönetim sistemi. İşletmeler ve müşteriler için geliştirilmiş web tabanlı bir platform.

## 🚀 Özellikler

### İşletme Paneli
- İşletme kaydı ve giriş sistemi
- Hizmet yönetimi (hizmet ekleme, düzenleme, silme)
- Rezervasyon görüntüleme ve yönetimi
- Profil yönetimi ve fotoğraf yükleme
- Email doğrulama sistemi
- Şifre sıfırlama

### Müşteri Paneli
- Müşteri kaydı ve giriş sistemi
- Rezervasyon oluşturma
- Rezervasyon geçmişi görüntüleme
- Profil yönetimi
- Email doğrulama sistemi

### Genel Özellikler
- Responsive tasarım (Bootstrap)
- Güvenli oturum yönetimi
- Email bildirimleri
- MongoDB veritabanı entegrasyonu
- Şifre hashleme (BCrypt)
- Güvenlik header'ları

## 🛠️ Teknolojiler

- **Backend**: ASP.NET Core 8.0
- **Veritabanı**: MongoDB
- **Frontend**: HTML, CSS, JavaScript, Bootstrap
- **Email**: SMTP (Gmail)
- **Güvenlik**: BCrypt.Net-Next
- **Session**: Distributed Memory Cache

## 📋 Gereksinimler

- .NET 8.0 SDK
- MongoDB Atlas hesabı (veya yerel MongoDB)
- Gmail hesabı (SMTP için)

## ⚙️ Kurulum

### 1. Projeyi Klonlayın
```bash
git clone [repository-url]
cd RezervasyonSistemi
```

### 2. Bağımlılıkları Yükleyin
```bash
dotnet restore
```

### 3. Yapılandırma
`appsettings.json` dosyasını düzenleyin:

```json
{
  "ConnectionStrings": {
    "MongoDB": "your-mongodb-connection-string"
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromEmail": "your-email@gmail.com",
    "FromName": "Rezervasyon Sistemi",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

### 4. Uygulamayı Çalıştırın
```bash
dotnet run
```

Uygulama `https://localhost:5001` adresinde çalışacaktır.

## 📁 Proje Yapısı

```
RezervasyonSistemi/
├── Controllers/          # MVC Controller'ları
│   ├── HomeController.cs
│   ├── BusniesController.cs
│   ├── CustommerController.cs
│   ├── RezervasyonController.cs
│   └── IsletmePanelController.cs
├── Models/              # Veri modelleri
│   ├── Busnies.cs      # İşletme modeli
│   ├── User.cs         # Müşteri modeli
│   └── Rezervasyon.cs  # Rezervasyon modeli
├── Views/              # Razor view'ları
│   ├── Home/          # Ana sayfa view'ları
│   ├── Busnies/       # İşletme paneli view'ları
│   ├── Custommer/     # Müşteri paneli view'ları
│   └── Rezervasyon/   # Rezervasyon view'ları
├── Services/          # Servis sınıfları
│   └── EmailService.cs
├── wwwroot/          # Statik dosyalar
└── Program.cs        # Uygulama başlangıç noktası
```

## 🔐 Güvenlik Özellikleri

- **HTTPS Zorunluluğu**: Tüm bağlantılar HTTPS üzerinden
- **Session Güvenliği**: 15 dakika oturum zaman aşımı
- **CSRF Koruması**: SameSite cookie politikası
- **XSS Koruması**: Güvenlik header'ları
- **Şifre Hashleme**: BCrypt ile güvenli şifre saklama
- **Email Doğrulama**: Hesap aktivasyonu için email doğrulama

## 📧 Email Yapılandırması

Gmail SMTP kullanımı için:
1. Gmail hesabınızda 2FA'yı etkinleştirin
2. Uygulama şifresi oluşturun
3. `appsettings.json` dosyasında email bilgilerini güncelleyin

## 🗄️ Veritabanı Şeması

### Collections:
- **busnies**: İşletme bilgileri
- **users**: Müşteri bilgileri
- **rezervasyonlar**: Rezervasyon kayıtları

## 🚀 Deployment

### Production Ortamı
1. Environment variables kullanın
2. Güvenli connection string'ler
3. HTTPS sertifikası
4. Logging yapılandırması

### Docker (Opsiyonel)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out
ENTRYPOINT ["dotnet", "out/RezervasyonSistemi.dll"]
```

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/AmazingFeature`)
3. Commit yapın (`git commit -m 'Add some AmazingFeature'`)
4. Push yapın (`git push origin feature/AmazingFeature`)
5. Pull Request oluşturun

## 📝 Lisans

Bu proje MIT lisansı altında lisanslanmıştır.

## 📞 İletişim

Proje Sahibi - [@fatihgurbuz7536](mailto:fatihgurbuz7536@gmail.com)


---

⭐ Bu projeyi beğendiyseniz yıldız vermeyi unutmayın! 