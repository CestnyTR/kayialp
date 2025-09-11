using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace kayialp.Infrastructure
{
    public static class TextCaseHelper
    {
        // (Aynı kalsın) EN metinden ortak KeyName üretimi
        // EN metinden ortak KeyName üretimi (apostroflar temizlenir)
        public static string ToCamelCaseKey(string english)
        {
            if (string.IsNullOrWhiteSpace(english)) return string.Empty;

            // 1) Diyakritik temizle (café -> cafe)
            english = RemoveDiacritics(english.Trim());

            // 2) Apostrof/tek tırnak çeşitlerini tamamen KALDIR
            // ’ (U+2019), ‘ (U+2018), ' (U+0027), ` (U+0060), ´ (U+00B4)
            english = Regex.Replace(english, @"[’‘'`´]", "");

            // 3) Alfa-numerik dışını boşluk yap, boşlukları sıkıştır
            var cleaned = Regex.Replace(english, @"[^A-Za-z0-9]+", " ").Trim();
            if (cleaned.Length == 0) return string.Empty;

            // 4) camelCase
            var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(parts[0].ToLowerInvariant());
            for (int i = 1; i < parts.Length; i++)
            {
                var p = parts[i].ToLowerInvariant();
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }

            var key = sb.ToString();

            // 5) Başta rakam başlarsa güvenli prefix
            if (char.IsDigit(key[0])) key = "n" + key;

            return key;
        }
        private static string RemoveDiacritics(string input)
        {
            var norm = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(capacity: norm.Length);
            foreach (var ch in norm)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
        // YERELLEŞTİRİLMİŞ SLUG (Unicode): Arapça/kiril/türkçe harfleri KORUR, sadece ayırıcıları '-' yapar.
        public static string ToLocalizedSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // 1) Arapça hareke & tatweel temizliği (okunurluk)
            text = RemoveArabicDiacritics(text);

            // 2) Boşlukları tek boşluk yap
            text = Regex.Replace(text, @"\s+", " ");

            // 3) Harf/rakam dışındaki TÜM karakter gruplarını '-' ile değiştir (Unicode farkındalığı)
            // \p{L} = tüm harfler (Arapça, Kiril, Latin, ...), \p{Nd} = tüm ondalık rakamlar (Arap-Indic dahil)
            var slug = Regex.Replace(text, @"[^\p{L}\p{Nd}]+", "-", RegexOptions.CultureInvariant);

            // 4) Trim + küçült + çoklu '-' sadeleştir
            slug = slug.Trim('-');
            slug = slug.ToLowerInvariant();
            slug = Regex.Replace(slug, "-{2,}", "-");

            return slug;
        }

        private static string RemoveArabicDiacritics(string input)
        {
            // Arapça harekeler (U+0610–U+061A, U+064B–U+065F, U+0670, U+06D6–U+06ED) + tatweel (U+0640)
            // Diğer diller etkilenmez.
            return Regex.Replace(input, @"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]", "");
        }
    }
}
