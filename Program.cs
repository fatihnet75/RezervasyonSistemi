using RezervasyonSistemi.Services;
using RezervasyonSistemi.Controllers;
using System.Net;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register MongoDB Service
builder.Services.AddSingleton<MongoDBService>();

// Register Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Session desteği ekle - Güvenlik artırıldı
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15); // 30 dakikadan 15 dakikaya düşürüldü
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS zorunlu
    options.Cookie.SameSite = SameSiteMode.Strict; // CSRF koruması
});

// Logging yapılandırması
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Güvenlik header'ları ekle
app.Use(async (context, next) =>
{
    // Güvenlik header'ları
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    
    // Cache kontrolü - hassas sayfalar için
    if (context.Request.Path.StartsWithSegments("/Rezervasyon") || 
        context.Request.Path.StartsWithSegments("/IsletmePanel") ||
        context.Request.Path.StartsWithSegments("/Custommer") ||
        context.Request.Path.StartsWithSegments("/Busnies"))
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
        context.Response.Headers["Surrogate-Control"] = "no-store";
    }
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
