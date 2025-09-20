using kayialp.Context;
using kayialp.Models;
using System.Globalization;
using System.Reflection;

namespace kayialp.Services
{
    public class ContentService
    {
        private readonly kayialpDbContext _context;

        public ContentService(kayialpDbContext context)
        {
            _context = context;
        }

        // Aktif UI culture'a göre Langs.Id
        private int? GetCurrentLangId()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return _context.Langs.FirstOrDefault(l => l.LangCode == culture)?.Id;
        }

        // İstenen two-letter culture'a göre Langs.Id
        private int? GetLangIdFor(string twoLetter)
        {
            return _context.Langs.FirstOrDefault(l => l.LangCode == twoLetter)?.Id;
        }

        /// <summary>
        /// Genel metin/alan çözümleyici.
        /// Desteklenen kalıplar:
        ///  - pages.{pageName}.{keyName}
        ///  - pages.{pageName}.{keyName}.slug
        ///  - categories.{keyName}
        ///  - fairs.{keyName}
        ///  - homeslide.{slideId}.{field}
        ///  - advantages.{advantageId}.{field}
        ///  - companyinfo.{field}
        /// </summary>
        public string GetText(string compoundKey)
        {
            var langId = GetCurrentLangId();
            if (string.IsNullOrWhiteSpace(compoundKey) || langId == null)
                return $"[[{compoundKey}]]";

            var parts = compoundKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return $"[[{compoundKey}]]";

            var tableKey = parts[0].ToLowerInvariant();
            string? result = null;

            switch (tableKey)
            {
                case "pages":
                    // "pages.pageName.keyName" veya "pages.pageName.keyName.slug"
                    if (parts.Length < 3) return $"[[{compoundKey}]]";
                    var pageName = parts[1];
                    var keyName = parts[2];
                    var field = parts.Length >= 4 ? parts[3] : null; // örn: "slug"
                    result = GetPageText(pageName, keyName, langId.Value, field);
                    break;

                case "categories":
                    // "categories.keyName"
                    result = GetCategoryText(parts[1], langId.Value);
                    break;

                case "fairs":
                    // "fairs.keyName" -> Title
                    result = GetFairTitle(parts[1], langId.Value);
                    break;

                case "homeslide":
                    // "homeslide.{slideId}.{field}"  field: slogan|title|content|cta1text|cta1url|cta2text|cta2url
                    if (parts.Length < 3) return $"[[{compoundKey}]]";
                    if (!int.TryParse(parts[1], out var slideId)) return $"[[{compoundKey}]]";
                    var hsField = parts[2].ToLowerInvariant();
                    result = GetHomeSlideField(slideId, hsField, langId.Value);
                    break;

                case "advantages":
                    // "advantages.{advantageId}.{field}"  field: title | desc
                    if (parts.Length < 3) return $"[[{compoundKey}]]";
                    if (!int.TryParse(parts[1], out var advId)) return $"[[{compoundKey}]]";
                    var advField = parts[2].ToLowerInvariant();
                    result = GetAdvantageField(advId, advField, langId.Value);
                    break;

                case "companyinfo":
                    // "companyinfo.about" | "companyinfo.mission" | "companyinfo.vision" | "companyinfo.address" | "companyinfo.mapurl"
                    if (parts.Length < 2) return $"[[{compoundKey}]]";
                    result = GetCompanyInfoField(parts[1].ToLowerInvariant(), langId.Value);
                    break;

                default:
                    return $"[[Unsupported Table: {tableKey}]]";
            }

            if (!string.IsNullOrEmpty(result))
                return result;

            // Fallback: en (yoksa ilk dil)
            var defaultLangId = GetLangIdFor("en") ?? _context.Langs.Select(x => (int?)x.Id).FirstOrDefault();
            if (defaultLangId == null || defaultLangId == langId)
                return $"[[{compoundKey}]]";

            // Aynı anahtarı fallback dilde çöz
            switch (tableKey)
            {
                case "pages":
                    var fbField = parts.Length >= 4 ? parts[3] : null;
                    return GetPageText(parts[1], parts[2], defaultLangId.Value, fbField) ?? $"[[{compoundKey}]]";

                case "categories":
                    return GetCategoryText(parts[1], defaultLangId.Value) ?? $"[[{compoundKey}]]";

                case "fairs":
                    return GetFairTitle(parts[1], defaultLangId.Value) ?? $"[[{compoundKey}]]";

                case "homeslide":
                    if (!int.TryParse(parts[1], out var slideId2)) return $"[[{compoundKey}]]";
                    return GetHomeSlideField(slideId2, parts[2], defaultLangId.Value) ?? $"[[{compoundKey}]]";

                case "advantages":
                    if (!int.TryParse(parts[1], out var advId2)) return $"[[{compoundKey}]]";
                    return GetAdvantageField(advId2, parts[2], defaultLangId.Value) ?? $"[[{compoundKey}]]";

                case "companyinfo":
                    return GetCompanyInfoField(parts[1].ToLowerInvariant(), defaultLangId.Value) ?? $"[[{compoundKey}]]";

                default:
                    return $"[[{compoundKey}]]";
            }
        }

        /// <summary>
        /// Sayfa içeriği/slug çözümü.
        /// field == "slug" ise PageTranslations.Slug döner; aksi halde ValueText.
        /// </summary>
        private string? GetPageText(string pageName, string keyName, int langId, string? field = null)
        {
            if (string.IsNullOrWhiteSpace(pageName) || string.IsNullOrWhiteSpace(keyName))
                return null;

            var normalized = pageName.ToLowerInvariant();

            var page = _context.Pages.FirstOrDefault(p => p.PageName.ToLower() == normalized);
            if (page == null) return null;

            var t = _context.PageTranslations
                .FirstOrDefault(x =>
                    x.PageId == page.Id &&
                    x.LangCodeId == langId &&
                    x.KeyName == keyName
                );

            if (t == null) return null;

            if (!string.IsNullOrEmpty(field) && field.Equals("slug", StringComparison.OrdinalIgnoreCase))
                return t.Slug;

            return t.ValueText;
        }

        private string? GetCategoryText(string keyName, int langId)
        {
            var t = _context.CategoriesTranslations
                .FirstOrDefault(x => x.KeyName == keyName && x.LangCodeId == langId);
            return t?.ValueText;
        }

        private string? GetFairTitle(string keyName, int langId)
        {
            var t = _context.FairTranslations
                .FirstOrDefault(x => x.KeyName == keyName && x.LangCodeId == langId);
            return t?.Title;
        }

        private string? GetHomeSlideField(int slideId, string field, int langId)
        {
            var t = _context.HomeSlideTranslations
                .FirstOrDefault(x => x.HomeSlideId == slideId && x.LangCodeId == langId);

            if (t == null) return null;

            return field switch
            {
                "slogan" => t.Slogan,
                "title" => t.Title,
                "content" => t.Content,
                _ => null
            };
        }

        private string? GetAdvantageField(int advantageId, string field, int langId)
        {
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

        private string? GetCompanyInfoField(string field, int langId)
        {
            var info = _context.CompanyInfos.FirstOrDefault();
            if (info == null) return null;

            // Önce translation üzerinde dene
            var t = _context.CompanyInfoTranslations
                .FirstOrDefault(x => x.CompanyInfoId == info.Id && x.LangCodeId == langId);

            var transProp = typeof(CompanyInfoTranslation).GetProperty(
                CultureInfo.CurrentCulture.TextInfo.ToTitleCase(field),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
            );
            if (transProp != null && t != null)
            {
                return transProp.GetValue(t)?.ToString();
            }

            if (field.Equals("address", StringComparison.OrdinalIgnoreCase))
            {
                // Formatlı adres
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(info.AddressLine)) parts.Add(info.AddressLine);
                if (!string.IsNullOrWhiteSpace(info.District)) parts.Add(info.District);
                if (!string.IsNullOrWhiteSpace(info.City)) parts.Add(info.City);
                if (!string.IsNullOrWhiteSpace(info.Country)) parts.Add(info.Country);
                if (!string.IsNullOrWhiteSpace(info.PostalCode)) parts.Add(info.PostalCode);

                return string.Join(", ", parts);
            }

            // CompanyInfo üzerinde dene
            var infoProp = typeof(CompanyInfo).GetProperty(
                CultureInfo.CurrentCulture.TextInfo.ToTitleCase(field),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
            );
            if (infoProp != null)
            {
                return infoProp.GetValue(info)?.ToString();
            }

            if (field.Equals("mapurl", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(info.MapEmbedUrl))
                {
                    return info.MapEmbedUrl; // Admin’in girdiği hazır embed
                }
                else
                {
                    // Adresten otomatik Google Maps embed linki üret
                    var address = GetCompanyInfoField("address", langId);
                    if (string.IsNullOrWhiteSpace(address)) return null;
                    return $"https://www.google.com/maps?q={Uri.EscapeDataString(address)}&output=embed";
                }
            }

            return null;
        }
    }
}
