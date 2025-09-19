// Helpers/LocalizationHelper.cs
using kayialp.Context;
using kayialp.Infrastructure;
using kayialp.Models;
using kayialp.Services;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ProductModels;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;

namespace kayialp.Helpers
{
    public class AdminHelper
    {
        public readonly kayialpDbContext _context;
        public readonly ITranslationService _translator;
        public readonly IWebHostEnvironment _env;

        public const string AdminBaseLangCode = "tr"; // Admin arayüzünde TR gösterelim

        public AdminHelper(kayialpDbContext context, ITranslationService translator, IWebHostEnvironment env)
        {
            _context = context;
            _translator = translator;
            _env = env;


        }



        // --- Helpers: KeyName ve Translation upsert ---

        // EN başlıktan camelCase key üret (boş ise yedek)
        public async Task<string> BuildCanonicalKeyForFairAsync(string trTitle, int fairId, CancellationToken ct)
        {
            string enTitle;
            try { enTitle = await _translator.TranslateAsync(trTitle ?? "", "TR", "EN"); }
            catch { enTitle = trTitle ?? ""; }

            var key = TextCaseHelper.ToCamelCaseKey(enTitle);
            if (string.IsNullOrWhiteSpace(key)) key = $"fair{fairId}";
            if (key.Length > 60) key = key[..60];
            return key;
        }

        // Dil bazında slug benzersiz olacak şekilde çeviri kaydı güncelle/ekle
        public async Task UpsertFairTranslationAsync(
            int fairId, int langId, string keyName, string title, string? slug, CancellationToken ct)
        {
            var normSlug = ToLocalizedSlugSafe(string.IsNullOrWhiteSpace(slug) ? title : slug);

            bool taken = await _context.FairTranslations
                .AnyAsync(x => x.LangCodeId == langId && x.Slug == normSlug && x.FairId != fairId, ct);
            if (taken) throw new ValidationException($"Slug zaten kullanılıyor: {normSlug}");

            var tr = await _context.FairTranslations
                .FirstOrDefaultAsync(x => x.FairId == fairId && x.LangCodeId == langId, ct);

            if (tr == null)
            {
                _context.FairTranslations.Add(new FairTranslations
                {
                    FairId = fairId,
                    LangCodeId = langId,
                    KeyName = keyName,
                    Title = title,
                    Slug = normSlug
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(tr.KeyName))
                    tr.KeyName = keyName;

                tr.Title = title;
                tr.Slug = normSlug;
            }
        }

        #region helpers
        // ===== Helpers =====

        // "en-GB" -> "en", "pt-br" -> "pt", "ru" -> "ru"
        public static string NormalizeLangKey(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            var c = code.Trim().ToLowerInvariant();
            var dash = c.IndexOf('-');
            return dash > 0 ? c[..dash] : c;
        }
        public static string BaseLang(string code) => NormalizeLangKey(code);

        // EF-Core tarafında LIKE ile TR Id getir (StartsWith(StringComparison) çevrilemez)
        public async Task<int> GetLangIdAsync(string code)
        {
            var prefix = NormalizeLangKey(code);
            Console.WriteLine($"dil kodu: {code} | prefix: {prefix}");
            return await _context.Langs
                .Where(l => EF.Functions.Like(l.LangCode, prefix + "%"))
                .OrderBy(l => l.Id) // kanonik Id
                .Select(l => l.Id)
                .FirstOrDefaultAsync();
        }

        // DeepL için hedef kod normalize
        public static string NormalizeToDeepLCode(string dbCode)
        {
            if (string.IsNullOrWhiteSpace(dbCode)) return "EN";

            var code = dbCode.Trim().ToLowerInvariant();   // "en", "en-us", "tr-tr"...

            if (code.StartsWith("EN")) return "en";
            if (code.StartsWith("TR")) return "tr";
            if (code.StartsWith("RU")) return "ru";
            if (code.StartsWith("AR")) return "ar";

            // Güvenli fallback (çok nadir): iki harfi büyüt
            return code[..Math.Min(2, code.Length)].ToUpperInvariant();
        }

        // ====== public HELPERS ======

        public async Task UpsertCategoryTranslationAsync(int categoryId, int langId, string keyName, string valueText, string slug)
        {
            if (langId <= 0) return;

            var existing = await _context.CategoriesTranslations
                .FirstOrDefaultAsync(t => t.CategoriesId == categoryId && t.LangCodeId == langId);

            if (existing is null)
            {
                _context.CategoriesTranslations.Add(new CategoriesTranslations
                {
                    CategoriesId = categoryId,
                    LangCodeId = langId,
                    KeyName = keyName,
                    ValueText = valueText,
                    Slug = await EnsureUniqueLocalizedSlugAsync(langId, slug)
                });
            }
            else
            {
                existing.KeyName = keyName;          // tüm dillerde aynı
                existing.ValueText = valueText;      // dilin adı
                existing.Slug = await EnsureUniqueLocalizedSlugAsync(langId, slug, existing.Id);
            }
            Console.WriteLine($"langsRaw langId : {langId}");

            await _context.SaveChangesAsync();
        }

        // Dil içinde slug benzersizliği
        public async Task<string> EnsureUniqueLocalizedSlugAsync(int langId, string baseSlug, int? ignoreId = null)
        {
            var slug = baseSlug;
            int suffix = 2;

            while (true)
            {
                var q = _context.CategoriesTranslations
                    .Where(ct => ct.LangCodeId == langId && ct.Slug == slug);

                if (ignoreId.HasValue)
                    q = q.Where(ct => ct.Id != ignoreId.Value);

                if (!await q.AnyAsync()) return slug;

                slug = $"{baseSlug}-{suffix++}";
            }
        }

        // ======= Helpers =======

        public async Task<IEnumerable<SelectListItem>> GetCategorySelectListAsync()
        {
            var trId = await GetLangIdAsync("tr");
            var items = await _context.Categories
                .OrderBy(c => c.Order)
                .Join(_context.CategoriesTranslations,
                      c => c.Id, t => t.CategoriesId,
                      (c, t) => new { c, t })
                .Where(x => x.t.LangCodeId == trId)
                .Select(x => new SelectListItem
                {
                    Value = x.c.Id.ToString(),
                    Text = x.t.ValueText
                })
                .ToListAsync();

            return items;
        }



        // Kanonik dil listesi: duplicate dilleri en küçük Id ile tekilleştir
        public async Task<List<(int Id, string Code)>> GetCanonicalLangsAsync()
        {
            var raw = await _context.Langs
                .AsNoTracking()
                .Where(l => !string.IsNullOrWhiteSpace(l.LangCode))
                .Select(l => new { l.Id, l.LangCode })
                .ToListAsync();

            return raw
                .GroupBy(x => NormalizeLangKey(x.LangCode))
                .Select(g => (Id: g.Min(x => x.Id),
                              Code: g.OrderBy(x => x.Id).First().LangCode))
                .OrderBy(x => x.Code)
                .ToList();
        }

        // Ürün slugu: dil içinde benzersiz hale getir
        public async Task<string> EnsureUniqueProductSlugAsync(int langId, string baseSlug, int? ignoreId = null)
        {
            var slug = baseSlug;
            int suffix = 2;
            while (true)
            {
                var q = _context.ProductsTranslations
                    .Where(pt => pt.LangCodeId == langId && pt.Slug == slug);
                if (ignoreId.HasValue) q = q.Where(pt => pt.Id != ignoreId.Value);

                if (!await q.AnyAsync()) return slug;
                slug = $"{baseSlug}-{suffix++}";
            }
        }

        // CSV Helpers
        public static List<string> SplitCsv(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();
            return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }

        public static string NormalizeImageUrlCsv(string? csv)
        {
            var list = SplitCsv(csv);
            var clean = new List<string>();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var url in list)
            {
                if (set.Add(url)) clean.Add(url);
                if (clean.Count == 5) break; // max 5
            }
            return string.Join(",", clean);
        }

        public static string NormalizeImageAltsCsv(string? csv, int imageCount)
        {
            var list = SplitCsv(csv).Select(s => s.Replace(",", " ").Trim()).ToList();
            if (list.Count > imageCount) list = list.Take(imageCount).ToList();
            while (list.Count < imageCount) list.Add(string.Empty);
            return string.Join(",", list);
        }
        // Alt text otomatik üretimi (CSV)
        public static string BuildAutoImageAltsCsv(string productNameLocalized, int imageCount, string baseLang)
        {
            imageCount = Math.Max(0, Math.Min(imageCount, 5));
            var list = new List<string>(imageCount);
            if (imageCount == 0) return string.Empty;

            // 1. görsel: sadece ürün adı
            list.Add(productNameLocalized);

            // 2..N görseller için dil bazlı şablon
            for (int i = 2; i <= imageCount; i++)
            {
                string alt;
                switch (baseLang)
                {
                    case "tr": alt = $"{productNameLocalized} – {i}. görsel"; break;
                    case "ru": alt = $"{productNameLocalized} – изображение {i}"; break;
                    case "ar": alt = $"{productNameLocalized} – صورة {i}"; break;
                    default: alt = $"{productNameLocalized} – image {i}"; break; // en
                }
                // virgülü bozmasın
                alt = alt.Replace(",", " ");
                list.Add(alt);
            }

            return string.Join(",", list);
        }


        public static readonly HashSet<string> _allowedImageExts =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif", ".svg" };

        // 10 MB
        public const long MAX_IMAGE_BYTES = 10 * 1024 * 1024;


        // Dosya adı gövdesini sadeleştirir (ASCII + tire)
        public static string SlugifyFileBase(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var s = name.Trim();

            // apostrof vb. kaldır
            s = s.Replace("’", "").Replace("'", "");

            // Türkçe/Latince basit normalize
            s = s.Normalize(NormalizationForm.FormD);
            var ascii = new char[s.Length];
            int idx = 0;
            foreach (var ch in s)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                var c = ch;
                if (c > 127) c = '-';
                ascii[idx++] = c;
            }
            var raw = new string(ascii, 0, idx);

            // harf/rakam dışında tire, çoklu tireleri tekle
            raw = Regex.Replace(raw, @"[^A-Za-z0-9]+", "-");
            raw = Regex.Replace(raw, @"-+", "-").Trim('-');

            if (raw.Length > 60) raw = raw[..60];
            return raw.ToLowerInvariant();
        }

        public static string GetFirstImage(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return "";
            var arr = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return arr.Length > 0 ? arr[0] : "";
        }
        public static int GetImageCount(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return 0;
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        }
        // --- NEW: central crop + exact size + webp save ---
        public async Task<string> SaveWebpVariantAsync(IFormFile file, string folder, string? relFolder, string fileNameNoExt, int width, int height)
        {
            Directory.CreateDirectory(folder);
            var targetPath = Path.Combine(folder, $"{fileNameNoExt}.webp");

            using var stream = file.OpenReadStream();
            using var image = await Image.LoadAsync(stream);

            // central crop/cover to exact size
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = new Size(width, height)
            }));

            var encoder = new WebpEncoder
            {
                Quality = 80, // optimize
                FileFormat = WebpFileFormatType.Lossy
            };

            await image.SaveAsWebpAsync(targetPath, encoder);

            // return web-relative path
            // var rel = $"/uploads/{relFolder}/{fileNameNoExt}.webp";
            // return rel;
            var rel = $"/uploads/{relFolder}/{Path.GetFileName(folder)}/{fileNameNoExt}.webp";
            return rel.Replace("\\", "/");
        }

        public void TryDeleteFileByWebPath(string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath)) return;
            var abs = Path.Combine(_env.WebRootPath, webPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (System.IO.File.Exists(abs))
            {
                System.IO.File.Delete(abs);
            }
        }

        // === Product Yardımcılar ===

        // Görsel sayısı kadar ALT liste üretir: [name, "name - image 2", ...]
        public static List<string> BuildAutoImageAltsList(int count, string productName)
        {
            var list = new List<string>(Math.Max(1, count));
            for (int i = 0; i < Math.Max(1, count); i++)
                list.Add(i == 0 ? productName : $"{productName} - image {i + 1}");
            return list;
        }


        public static string MakeSafeFileBaseName(string input)
        {
            var s = input.Trim();
            s = Regex.Replace(s, @"\s+", "-");
            s = Regex.Replace(s, @"[^a-zA-Z0-9\-_]", "");
            return string.IsNullOrWhiteSpace(s) ? "image" : s.ToLowerInvariant();
        }

        public async Task UpsertBlockHtmlAsync(int blockId, int langId, string html, CancellationToken ct)
        {
            var t = await _context.ProductContentBlockTranslations
                .FirstOrDefaultAsync(x => x.BlockId == blockId && x.LangCodeId == langId, ct);

            if (t == null)
                _context.ProductContentBlockTranslations.Add(new ProductContentBlockTranslation { BlockId = blockId, LangCodeId = langId, Html = html ?? "" });
            else
                t.Html = html ?? "";

            await _context.SaveChangesAsync(ct);
        }

        public async Task UpsertProductTranslationAsync(int productId, int langId, UpdateProductLangVM vm, int imageCount, bool isSource, CancellationToken ct)
        {
            var t = await _context.ProductsTranslations.FirstOrDefaultAsync(x => x.ProductId == productId && x.LangCodeId == langId, ct);
            if (t == null)
            {
                t = new ProductsTranslations { ProductId = productId, LangCodeId = langId };
                _context.ProductsTranslations.Add(t);
            }

            // KeyName EN bazlı kalsın (varsa dokunma). Kaynakta güncelliyorsa yeniden üretebilirsin.
            if (string.IsNullOrWhiteSpace(t.KeyName) && !string.IsNullOrWhiteSpace(vm.Name))
            {
                var enName = isSource ? TryTranslate(vm.Name, "tr", "en") : vm.Name;
                t.KeyName = TextCaseHelper.ToCamelCaseKey(enName);
            }

            t.ValueText = vm.Name ?? "";


            // Slug: boşsa üret; doluysa benzersizleştir (isim değişmiş olabilir)
            var baseSlug = string.IsNullOrWhiteSpace(vm.Slug)
                ? ToLocalizedSlugSafe(_context.Langs.First(x => x.Id == langId).LangCode, vm.Name)
                : vm.Slug;
            t.Slug = await EnsureUniqueProductSlugAsync(langId, baseSlug, productId);

            // ImageAlts: boşsa üret; eleman sayısını imageCount’a göre dengeler
            t.ImageAlts = NormalizeAltsCsv(vm.ImageAltsCsv, vm.Name, imageCount, _context.Langs.First(x => x.Id == langId).LangCode);

            await _context.SaveChangesAsync(ct);
        }

        public string NormalizeAltsCsv(string? csv, string productName, int count, string langCode)
        {
            var list = SplitCsv(csv);
            if (list.Count == 0)
                list = BuildAutoImageAltsList(count, productName);
            // listeyi sayıya eşitle
            if (list.Count > count) list = list.Take(count).ToList();
            if (list.Count < count)
                while (list.Count < count) list.Add($"{productName} - image {list.Count + 1}");
            return string.Join(", ", list);
        }


        public string BuildOrTranslateAlts(string? targetCsv, string? trCsv, string trName, int imageCount, bool auto, string src, string tgt)
        {
            if (!string.IsNullOrWhiteSpace(targetCsv)) return NormalizeAltsCsv(targetCsv, trName, imageCount, tgt);

            var trList = SplitCsv(trCsv);
            if (trList.Count == 0)
                trList = BuildAutoImageAltsList(imageCount, trName);

            if (!auto) return string.Join(", ", trList);

            var upperTgt = (tgt ?? "en").ToUpperInvariant();
            var outList = new List<string>(trList.Count);
            foreach (var item in trList)
                outList.Add(TryTranslate(item, src, upperTgt));
            return string.Join(", ", outList);
        }

        // Dil-özel slug (TextCaseHelper varsa onu kullan)
        public string ToLocalizedSlugSafe(string lang, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) text = "urun";
            try { return TextCaseHelper.ToLocalizedSlug(text); }
            catch
            {
                var slug = Regex.Replace(text.ToLowerInvariant(), @"\s+", "-");
                slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
                slug = Regex.Replace(slug, @"-+", "-").Trim('-');
                return string.IsNullOrWhiteSpace(slug) ? "urun" : slug;
            }
        }

        // Zincirlemeyi önleyerek benzersiz slug üret(foo, foo-2, foo-3…)
        // Kendi kaydını hariç tutar
        public async Task<string> EnsureUniqueProductSlugAsync(
            int langId, string slugCandidate, int? excludeProductId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(slugCandidate)) slugCandidate = "item";

            // sondaki -N ekini temizle, zinciri kır
            var baseSlug = Regex.Replace(slugCandidate.Trim(), @"-(\d+)$", "", RegexOptions.CultureInvariant);
            var candidate = baseSlug;
            var suffix = 2;

            while (await IsProductSlugTakenAsync(langId, candidate, excludeProductId, ct))
            {
                candidate = $"{baseSlug}-{suffix}";
                suffix++;
            }
            return candidate;
        }

        // Sadeleştirilmiş çeviri (upper target)
        public string TryTranslate(string text, string from, string toLower)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var tgt = (toLower ?? "en").ToUpperInvariant();   // "ar" -> "AR"
            var src = (from ?? "tr").ToUpperInvariant();
            try { return _translator.TranslateAsync(text, src, tgt).GetAwaiter().GetResult(); }
            catch { return text; }
        }

        public static List<string> BalanceAltCount(List<string> list, int count, string name)
        {
            var res = new List<string>(list);
            if (res.Count > count) res = res.Take(count).ToList();
            while (res.Count < count) res.Add($"{name} - image {res.Count + 1}");
            return res;
        }

        public static string NormalizeOrBuildAlts(string? csv, string name, int imageCount)
        {
            var list = SplitCsv(csv);
            if (list.Count == 0) list = BuildAutoImageAltsList(imageCount, name);
            return string.Join(", ", BalanceAltCount(list, imageCount, name));
        }

        // TextCaseHelper sende var; tek parametreli sürüm kullanılıyor :contentReference[oaicite:5]{index=5}
        public string ToLocalizedSlugSafe(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) text = "urun";
            try { return TextCaseHelper.ToLocalizedSlug(text); }
            catch
            {
                var slug = Regex.Replace(text.ToLowerInvariant(), @"\s+", "-");
                slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
                slug = Regex.Replace(slug, @"-+", "-").Trim('-');
                return string.IsNullOrWhiteSpace(slug) ? "urun" : slug;
            }
        }

        // mevcut Create'teki imzayı korumak istersen bunu çağır:

        // Ürün çeviri upsert (kullanıcı slug’ı varsa dokunma; çakışırsa HATA)
        public async Task UpsertProductTranslationAsync(
            int productId,
            int langId,
            string name,
            string? slugInput,
            string? imageAltsCsv,
            CancellationToken ct = default)
        {
            var tr = await _context.ProductsTranslations
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.LangCodeId == langId, ct);

            string finalSlug;
            if (!string.IsNullOrWhiteSpace(slugInput))
            {
                // kullanıcı girdisi → normalize et, çakışırsa reddet
                var normalized = ToLocalizedSlugSafe(slugInput!);
                var taken = await IsProductSlugTakenAsync(langId, normalized, excludeProductId: productId, ct);
                if (taken)
                    throw new ValidationException($"Slug '{normalized}' bu dilde başka bir üründe kullanılıyor.");
                finalSlug = normalized;
            }
            else
            {
                // boşsa addan üret + benzersiz yap (kendi kaydını dışla)
                var baseSlug = ToLocalizedSlugSafe(string.IsNullOrWhiteSpace(name) ? $"product-{productId}" : name);
                finalSlug = await EnsureUniqueProductSlugAsync(langId, baseSlug, productId, ct);
            }

            if (tr == null)
            {
                _context.ProductsTranslations.Add(new ProductsTranslations
                {
                    ProductId = productId,
                    LangCodeId = langId,
                    KeyName = TextCaseHelper.ToCamelCaseKey(name),
                    ValueText = name,
                    Slug = finalSlug,
                    ImageAlts = imageAltsCsv ?? ""
                });
            }
            else
            {
                tr.KeyName = TextCaseHelper.ToCamelCaseKey(name);
                tr.ValueText = name;
                tr.Slug = finalSlug;           // zincirlenmez, tek sefer karar
                tr.ImageAlts = imageAltsCsv ?? "";
            }
        }

        // Aynı dilde slug mevcut mu? (mevcut ürünü hariç tut)
        public async Task<bool> IsProductSlugTakenAsync(
            int langId, string slug, int? excludeProductId = null, CancellationToken ct = default)
        {
            var q = _context.ProductsTranslations.AsNoTracking()
                .Where(t => t.LangCodeId == langId && t.Slug == slug);
            if (excludeProductId.HasValue)
                q = q.Where(t => t.ProductId != excludeProductId.Value);
            return await q.AnyAsync(ct);
        }
        public static List<int> ParseIds(string? csv)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (var s in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(s.Trim(), out var n) && n > 0) list.Add(n);
            return list;
        }

        public static List<int> NormalizeOrderPayload(string[]? order, string? idsCsv)
        {
            if (order != null && order.Length > 0)
            {
                var res = new List<int>();
                foreach (var s in order)
                    if (int.TryParse(s, out var n) && n > 0) res.Add(n);
                return res;
            }
            return ParseIds(idsCsv);
        }

        #region  blog helper
        // === BLOG SLUG HELPERS ===
        public async Task<bool> IsPostSlugTakenAsync(int langId, string slug, int? excludePostId = null, CancellationToken ct = default)
        {
            var q = _context.BlogPostsTranslations.AsNoTracking()
                .Where(t => t.LangCodeId == langId && t.Slug == slug);
            if (excludePostId.HasValue) q = q.Where(t => t.PostId != excludePostId.Value);
            return await q.AnyAsync(ct);
        }

        public async Task<string> EnsureUniquePostSlugAsync(int langId, string slugCandidate, int? excludePostId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(slugCandidate)) slugCandidate = "post";
            var baseSlug = System.Text.RegularExpressions.Regex.Replace(slugCandidate.Trim(), @"-(\d+)$", "");
            var candidate = baseSlug; var suffix = 2;
            while (await IsPostSlugTakenAsync(langId, candidate, excludePostId, ct))
            {
                candidate = $"{baseSlug}-{suffix++}";
            }
            return candidate;
        }

        public async Task UpsertPostTranslationAsync(
            int postId, int langId, string KeyValue, string title, string? slugInput,
            string? altCover, string? altInner, CancellationToken ct = default)
        {
            var tr = await _context.BlogPostsTranslations
                .FirstOrDefaultAsync(x => x.PostId == postId && x.LangCodeId == langId, ct);

            string finalSlug;
            if (!string.IsNullOrWhiteSpace(slugInput))
            {
                var normalized = ToLocalizedSlugSafe(slugInput!);
                if (await IsPostSlugTakenAsync(langId, normalized, excludePostId: postId, ct))
                    throw new ValidationException($"Slug '{normalized}' bu dilde başka bir gönderide kullanılıyor.");
                finalSlug = normalized;
            }
            else
            {
                var baseSlug = ToLocalizedSlugSafe(string.IsNullOrWhiteSpace(title) ? $"post-{postId}" : title);
                finalSlug = await EnsureUniquePostSlugAsync(langId, baseSlug, postId, ct);
            }

            if (tr == null)
            {
                _context.BlogPostsTranslations.Add(new BlogPostsTranslations
                {
                    PostId = postId,
                    LangCodeId = langId,
                    KeyName = KeyValue,
                    ValueTitle = title,
                    Slug = finalSlug,
                    ImageAltCover = altCover ?? title,
                    ImageAltInner = altInner ?? title
                });
            }
            else
            {
                tr.KeyName = TextCaseHelper.ToCamelCaseKey(title);
                tr.ValueTitle = title;
                tr.Slug = finalSlug;
                tr.ImageAltCover = altCover ?? title;
                tr.ImageAltInner = altInner ?? title;
            }
        }

        public async Task UpsertPostBlockHtmlAsync(int blockId, int langId, string html, CancellationToken ct = default)
        {
            var tr = await _context.BlogPostContentBlockTranslations
                .FirstOrDefaultAsync(x => x.BlockId == blockId && x.LangCodeId == langId, ct);
            if (tr == null)
            {
                _context.BlogPostContentBlockTranslations.Add(new BlogPostContentBlockTranslation
                {
                    BlockId = blockId,
                    LangCodeId = langId,
                    Html = html ?? ""
                });
            }
            else tr.Html = html ?? "";
        }
        public async Task<string> BuildCanonicalKeyNameAsync(string trTitle, int idForFallback, CancellationToken ct)
        {
            string enTitle;
            try { enTitle = await _translator.TranslateAsync(trTitle, "TR", "EN"); }
            catch { enTitle = trTitle; }

            var key = TextCaseHelper.ToCamelCaseKey(enTitle); // mevcut helper’ını kullan
            if (string.IsNullOrWhiteSpace(key)) key = $"post{idForFallback}";
            if (key.Length > 60) key = key.Substring(0, 60);
            return key;
        }

        #endregion

        /* === Helpers === */

        // EN’den kısa, camelCase key üret; boşsa fallback
        public async Task<string> BuildCanonicalKeyNameForCategoryAsync(string trName, int idForFallback, CancellationToken ct)
        {
            string enName;
            try { enName = await _translator.TranslateAsync(trName ?? "", "TR", "EN"); }
            catch { enName = trName ?? ""; }

            var key = TextCaseHelper.ToCamelCaseKey(enName);
            if (string.IsNullOrWhiteSpace(key)) key = $"blogCategory{idForFallback}";
            if (key.Length > 60) key = key.Substring(0, 60);
            return key;
        }

        // Slug dil bazında benzersiz; çakışırsa ValidationException fırlatır
        public async Task UpsertBlogCategoryTranslationAsync(
            int categoryId, int langId,
            string keyName, string name, string? slugOrTitle,
            CancellationToken ct)
        {
            // slug normalize (boşsa name’den)
            var slug = ToLocalizedSlugSafe(string.IsNullOrWhiteSpace(slugOrTitle) ? name : slugOrTitle!);

            // benzersizlik kontrolü (dil bazında)
            var taken = await _context.BlogCategoriesTranslations
                .AnyAsync(x => x.LangCodeId == langId && x.Slug == slug && x.BlogCategoryId != categoryId, ct);
            if (taken)
                throw new ValidationException($"Slug zaten kullanılıyor: '{slug}' (langId={langId})");

            var tr = await _context.BlogCategoriesTranslations
                .FirstOrDefaultAsync(x => x.BlogCategoryId == categoryId && x.LangCodeId == langId, ct);

            if (tr == null)
            {
                _context.BlogCategoriesTranslations.Add(new BlogCategoriesTranslations
                {
                    BlogCategoryId = categoryId,
                    LangCodeId = langId,
                    KeyName = keyName,
                    ValueText = name,
                    Slug = slug
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(tr.KeyName))
                    tr.KeyName = keyName; // ilk boşsa doldur, sonrasında sabit bırak

                tr.ValueText = name;
                tr.Slug = slug;
            }
        }


        #endregion
        // ===== FAQ HELPERS =====
        public async Task UpsertFaqTranslationAsync(
            int faqId, int langId, string question, string answer, CancellationToken ct)
        {
            var tr = await _context.FaqTranslations
                .FirstOrDefaultAsync(x => x.FaqId == faqId && x.LangCodeId == langId, ct);

            if (tr == null)
            {
                _context.FaqTranslations.Add(new FaqTranslations
                {
                    FaqId = faqId,
                    LangCodeId = langId,
                    Question = question ?? "",
                    Answer = answer ?? "",
                    Slug = "" // kullanılmıyor
                });
            }
            else
            {
                tr.Question = question ?? "";
                tr.Answer = answer ?? "";
                // Slug kullanmıyoruz, dokunma
            }
        }
    }

}
