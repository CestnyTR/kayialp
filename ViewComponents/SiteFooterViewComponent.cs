using System.Globalization;
using kayialp.Context;
using kayialp.ViewModels.Layout;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.ViewComponents
{
    // Footer'ı CompanyInfo tablosundan doldurur
    public class SiteFooterViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;
        public SiteFooterViewComponent(kayialpDbContext context) { _context = context; }

        private int? GetLangId(string code) =>
            _context.Langs.FirstOrDefault(l => l.LangCode == code)?.Id;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var langId = GetLangId(culture) ?? GetLangId("en");

            // Tekil kayıt varsayımı
            var c = await _context.CompanyInfos.AsNoTracking().FirstOrDefaultAsync();
            if (c == null)
            {
                // boş state
                return View("/Views/Shared/Components/SiteFooter/Default.cshtml", new FooterViewModel());
            }

            // ilgili dilde çeviri (About/Mission/Vision)
            var tr = await _context.CompanyInfoTranslations.AsNoTracking()
                .FirstOrDefaultAsync(t => t.CompanyInfoId == c.Id && t.LangCodeId == langId);

            // adresi tek satıra getir
            string ComposeAddress()
            {
                var parts = new[]
                {
                    c.AddressLine, // örn. cadde/sokak + kapı
                    c.District,
                    c.City,
                    c.PostalCode
                }.Where(s => !string.IsNullOrWhiteSpace(s));
                return string.Join(", ", parts);
            }
            // AboutHtml max 200 karakter (HTML etiketlerini temizlemeden sade kısaltma)
            string about = tr?.AboutHtml ?? "";
            if (!string.IsNullOrEmpty(about) && about.Length > 200)
            {
                about = about.Substring(0, 200) + "...";
            }
            var vm = new FooterViewModel
            {
                LogoUrl = string.IsNullOrWhiteSpace(c.LogoUrl) ? c.LogoUrl : c.LogoUrl,
                Email = string.IsNullOrWhiteSpace(c.Email) ? c.Email2 : c.Email,
                Phone = string.IsNullOrWhiteSpace(c.Phone) ? c.Phone2 : c.Phone,
                Address = ComposeAddress(),
                AboutHtml = about,

                // socials
                FacebookUrl = c.FacebookUrl,
                InstagramUrl = c.InstagramUrl,
                LinkedInUrl = c.LinkedInUrl,
                TwitterUrl = c.TwitterUrl,
                YoutubeUrl = c.YoutubeUrl,
                Whatsapp = c.Whatsapp
            };

            return View("/Views/Shared/Components/SiteFooter/Default.cshtml", vm);
        }
    }
}
