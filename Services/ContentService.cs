using kayialp.Context;
using System.Globalization;

namespace kayialp.Services
{
    public class ContentService
    {
        private readonly kayialpDbContext _context;

        public ContentService(kayialpDbContext context)
        {
            _context = context;
        }

        // Aktif dilin Langs.Id’sini getir
        private int? GetCurrentLangId()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return _context.Langs.FirstOrDefault(l => l.LangCode == culture)?.Id;
        }

        // Verilen culture için Langs.Id
        private int? GetLangIdFor(string twoLetter)
        {
            return _context.Langs.FirstOrDefault(l => l.LangCode == twoLetter)?.Id;
        }

        // Genel metin çözümleyici: pages.*, categories.*, fairs.*, homeslide.* ...
        public string GetText(string compoundKey)
        {
            var langId = GetCurrentLangId();
            if (string.IsNullOrWhiteSpace(compoundKey) || langId == null)
                return $"[[{compoundKey}]]";

            var parts = compoundKey.Split('.');
            if (parts.Length < 2)
                return $"[[{compoundKey}]]";

            var tableKey = parts[0].ToLowerInvariant();
            string result = null;

            switch (tableKey)
            {
                case "pages":
                    // "pages.pageName.keyName"
                    if (parts.Length < 3) return $"[[{compoundKey}]]";
                    result = GetPageText(parts[1], parts[2], langId.Value);
                    break;

                case "categories":
                    // "categories.keyName"
                    result = GetCategoryText(parts[1], langId.Value);
                    break;

                case "fairs":
                    // "fairs.keyName"  -> Title alanını döndürüyoruz
                    result = GetFairTitle(parts[1], langId.Value);
                    break;

                case "homeslide":
                    // "homeslide.{slideId}.{field}"  field: slogan|title|content|cta1text|cta1url|cta2text|cta2url
                    if (parts.Length < 3) return $"[[{compoundKey}]]";
                    if (!int.TryParse(parts[1], out var slideId)) return $"[[{compoundKey}]]";
                    var field = parts[2].ToLowerInvariant();
                    result = GetHomeSlideField(slideId, field, langId.Value);
                    break;
                case "advantages":
                    // "advantages.{advantageId}.{field}"  field: title | desc | linktext (opsiyonel)
                    if (parts.Length < 3) return $"[[{compoundKey}]]";
                    if (!int.TryParse(parts[1], out var advId)) return $"[[{compoundKey}]]";
                    var advantagesFields = parts[2].ToLowerInvariant();
                    result = GetAdvantageField(advId, advantagesFields, langId.Value);
                    break;

                case "products":
                    // İleride ihtiyaç olursa doldurulabilir
                    return $"[[products resolver not implemented]]";

                default:
                    return $"[[Unsupported Table: {tableKey}]]";
            }

            if (!string.IsNullOrEmpty(result))
                return result;

            // Fallback: en (yoksa ilk dil)
            var defaultLangId = GetLangIdFor("en") ?? _context.Langs.Select(x => (int?)x.Id).FirstOrDefault();
            if (defaultLangId == null || defaultLangId == langId)
                return $"[[{compoundKey}]]";

            // Aynı anahtarı default dilde tekrar çöz
            switch (tableKey)
            {
                case "pages":
                    return GetPageText(parts[1], parts[2], defaultLangId.Value) ?? $"[[{compoundKey}]]";
                case "categories":
                    return GetCategoryText(parts[1], defaultLangId.Value) ?? $"[[{compoundKey}]]";
                case "fairs":
                    return GetFairTitle(parts[1], defaultLangId.Value) ?? $"[[{compoundKey}]]";
                case "homeslide":
                    return GetHomeSlideField(int.Parse(parts[1]), parts[2], defaultLangId.Value) ?? $"[[{compoundKey}]]";
                default:
                    return $"[[{compoundKey}]]";
            }
        }

        private string GetPageText(string pageName, string keyName, int langId)
        {
            var page = _context.Pages.FirstOrDefault(p => p.PageName.ToLower() == pageName.ToLower());
            if (page == null) return null;

            var t = _context.PageTranslations
                .FirstOrDefault(x => x.PageId == page.Id && x.LangCodeId == langId && x.KeyName == keyName);
            return t?.ValueText;
        }

        private string GetCategoryText(string keyName, int langId)
        {
            var t = _context.CategoriesTranslations
                .FirstOrDefault(x => x.KeyName == keyName && x.LangCodeId == langId);
            return t?.ValueText;
        }

        private string GetFairTitle(string keyName, int langId)
        {
            var t = _context.FairTranslations
                .FirstOrDefault(x => x.KeyName == keyName && x.LangCodeId == langId);
            return t?.Title;
        }

        private string GetHomeSlideField(int slideId, string field, int langId)
        {
            // Translation tablosunda ilgili kaydı bul
            var t = _context.HomeSlideTranslations
                .FirstOrDefault(x => x.HomeSlideId == slideId && x.LangCodeId == langId);

            if (t == null) return null;

            return field switch
            {
                "slogan" => t.Slogan,
                "title" => t.Title,
                "content" => t.Content,
                "cta1text" => t.Cta1Text,
                "cta1url" => t.Cta1Url,
                "cta2text" => t.Cta2Text,
                "cta2url" => t.Cta2Url,
                _ => null
            };
        }

        private string GetAdvantageField(int advantageId, string field, int langId)
        {
            // Varsayım: AdvantageTranslations tablosu: AdvantageId, LangCodeId, Title, Description, LinkText (ops)
            var t = _context.AdvantageTranslations
                .FirstOrDefault(x => x.AdvantageId == advantageId && x.LangCodeId == langId);

            if (t == null) return null;

            return field switch
            {
                "title" => t.Title,
                "desc" => t.Content,
                _ => null
            };
        }
    }
}
