using kayialp.Context;
using Microsoft.AspNetCore.Localization;
using System;
using System.Globalization;
using static System.Collections.Specialized.BitVector32;
namespace kayialp.Services
{
    public class ContentService
    {

        private readonly kayialpDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ContentService(kayialpDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetText(string compoundKey)
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var langId = _context.Langs.FirstOrDefault(l => l.LangCode == culture)?.Id;

            if (string.IsNullOrWhiteSpace(compoundKey) || !compoundKey.Contains('.') || langId == null)
                return $"[[{compoundKey}]]";

            var parts = compoundKey.Split('.');
            var tableKey = parts[0].ToLower(); // Örnek: "pages", "categories", "products"

            // Her zaman ikinci parçayı anahtar olarak kullanacağız.
            var key = parts.Last();
            string result = null;

            // Hangi tablodan veri çekileceğini belirle
            switch (tableKey)
            {
                case "pages":
                    // Key formatı: "pages.page_name.key"
                    if (parts.Length < 3) return $"[[{compoundKey}]]";

                    var pageName = parts[1];
                    var page = _context.Pages.FirstOrDefault(p => p.PageName.ToLower() == pageName);

                    if (page != null)
                    {
                        var translation = _context.PageTranslations
                            .FirstOrDefault(t => t.PageId == page.Id && t.LangCodeId == langId && t.KeyName == key);
                        result = translation?.ValueText;
                    }
                    break;

                case "categories":
                    // Key formatı: "categories.key_name"
                    var categoryTranslation = _context.CategoriesTranslations
                        .FirstOrDefault(t => t.KeyName == key && t.LangCodeId == langId);
                    result = categoryTranslation?.ValueText;
                    break;
                case "fairs":
                    // Key formatı: "categories.key_name"
                    var fairsTranslation = _context.FairTranslations
                        .FirstOrDefault(t => t.KeyName == key && t.LangCodeId == langId);
                    result = fairsTranslation?.Title;
                    break;
                case "products":
                    // Key formatı: "products.key_name"
                    // ProductTranslations modelinize göre sorgunuzu buraya ekleyebilirsiniz.
                    // Örneğin: var productTranslation = _context.ProductTranslations...
                    // result = productTranslation?.Value;
                    break;

                // Diğer tabloları buraya ekleyebiliriz...

                default:
                    return $"[[Unsupported Table: {tableKey}]]";
            }

            // Eğer çeviri bulunamazsa, varsayılan dili (en) dene
            if (string.IsNullOrEmpty(result) && langId != _context.Langs.FirstOrDefault(l => l.LangCode == "en")?.Id)
            {
                var defaultLangId = _context.Langs.FirstOrDefault(l => l.LangCode == "en")?.Id;
                if (defaultLangId != null)
                {
                    // Varsayılan dil için sorguyu tekrar çalıştır.
                    // Basitlik için switch içinde tekrar yazıyoruz,
                    // ancak daha temiz bir çözüm için refactor edilebilir.
                    switch (tableKey)
                    {
                        case "pages":
                            var pageName = parts[1];
                            var page = _context.Pages.FirstOrDefault(p => p.PageName.ToLower() == pageName);
                            if (page != null)
                            {
                                var translation = _context.PageTranslations
                                    .FirstOrDefault(t => t.PageId == page.Id && t.LangCodeId == defaultLangId && t.KeyName == key);
                                result = translation?.ValueText;
                            }
                            break;

                        case "categories":
                            var categoryTranslation = _context.CategoriesTranslations
                                .FirstOrDefault(t => t.KeyName == key && t.LangCodeId == defaultLangId);
                            result = categoryTranslation?.ValueText;
                            break;

                        case "products":
                            // ... products tablosu için varsayılan dil kodu
                            break;
                    }
                }
            }

            return result ?? $"[[{compoundKey}]]";
        }
    }
}