using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Localization;

namespace kayialp.Controllers
{
    public class LanguageController : Controller
    {
        [HttpPost]
        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
              CookieRequestCultureProvider.DefaultCookieName,
              CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
              new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            // returnUrl içindeki mevcut culture'ı değiştir
            if (!string.IsNullOrEmpty(returnUrl))
            {
                var segments = returnUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length > 0 && supportedCultureCodes.Contains(segments[0]))
                {
                    // URL’nin başındaki mevcut kültürü yenisiyle değiştir
                    segments[0] = culture;
                    var newUrl = "/" + string.Join("/", segments);
                    return LocalRedirect(newUrl);
                }
                else
                {
                    // Kültür parametresi yoksa, başa ekle
                    return LocalRedirect($"/{culture}{returnUrl}");
                }
            }

            return RedirectToAction("Index", "Home", new { culture = culture });
        }
        private static readonly List<string> supportedCultureCodes = new List<string> { "en", "tr", "ru", "ar" };

    }
}
