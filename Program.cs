using kayialp.Context;
using kayialp.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ContentService>();
builder.Services.AddHttpContextAccessor(); // HttpContext için gerekli

builder.Services.AddDbContext<kayialpDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Veritabanı bağlantısını kurup, dilleri veritabanından çekme
using (var scope = app.Services.CreateScope())
{
  var services = scope.ServiceProvider;
  var dbContext = services.GetRequiredService<kayialpDbContext>();

  // Langs tablosundaki LangCode değerlerini çekiyoruz
  var supportedCulturesFromDb = dbContext.Langs
      .Select(l => new CultureInfo(l.LangCode))
      .ToList();

  var supportedLangsFromDb = dbContext.Langs
      .Select(l => l.LangCode)
      .ToList();

  app.UseRequestLocalization(new RequestLocalizationOptions
  {
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCulturesFromDb,
    SupportedUICultures = supportedCulturesFromDb,
    RequestCultureProviders =
        {
            new RouteDataRequestCultureProvider(),
            new CookieRequestCultureProvider(),
            new AcceptLanguageHeaderRequestCultureProvider()
        }
  });

  // Artık bu kısmı veritabanından gelen dillerle güncelliyoruz
  var supportedLang = supportedLangsFromDb.ToArray();
  app.Use(async (context, next) =>
  {
    if (context.Request.Path == "/")
    {
      string? culture = null;

      // 1. Çerezden kültürü oku
      var cultureCookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
      if (!string.IsNullOrEmpty(cultureCookie))
      {
        var parsedCulture = CookieRequestCultureProvider.ParseCookieValue(cultureCookie);
        culture = parsedCulture?.Cultures.FirstOrDefault().Value;
      }

      // 2. Tarayıcıdan (Accept-Language) al (eğer çerez yoksa)
      if (string.IsNullOrEmpty(culture))
      {
        var userLanguages = context.Request.Headers["Accept-Language"].ToString();
        if (!string.IsNullOrEmpty(userLanguages))
        {
          var preferred = userLanguages.Split(',')
                .Select(lang => lang.Split(';').First().Trim().Substring(0, 2))
                .FirstOrDefault(lang => supportedLang.Contains(lang));
          if (!string.IsNullOrEmpty(preferred))
          {
            culture = preferred;
          }
        }
      }

      // 3. Hâlâ kültür yoksa varsayılan: "en"
      if (string.IsNullOrEmpty(culture))
      {
        culture = "en";
      }

      context.Response.Redirect($"/{culture}");
      return;
    }

    await next();
  });
}

// Yönlendirme ve geri kalan kod
app.UseStaticFiles();
app.UseRouting();

// Admin paneli için kültürsüz (prefix'siz) rota
app.MapControllerRoute(
    name: "admin-nonlocalized",
    pattern: "Admin/{action=Index}/{id?}",
    defaults: new { controller = "Admin" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{culture=en}/{controller=Home}/{action=Index}/{id?}",
    defaults: new { culture = "en" }
);

app.Run();
