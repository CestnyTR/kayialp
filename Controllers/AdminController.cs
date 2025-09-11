using kayialp.Context;
using kayialp.Infrastructure;
using kayialp.Models;
using kayialp.Services;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly kayialpDbContext _context;
        private readonly ITranslationService _translator;

        private const string AdminBaseLangCode = "tr"; // Admin arayüzünde TR gösterelim

        public AdminController(kayialpDbContext context, ITranslationService translator)
        {
            _context = context;
            _translator = translator;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var trId = await GetLangIdAsync(AdminBaseLangCode);

            var categories = await _context.Categories
                .OrderBy(c => c.Order)
                .Join(_context.CategoriesTranslations,
                      c => c.Id, t => t.CategoriesId,
                      (c, t) => new { c, t })
                .Where(x => x.t.LangCodeId == trId)
                .Select(x => new CategoryViewModel
                {
                    Id = x.c.Id,
                    Name = x.t.ValueText
                })
                .ToListAsync();

            return View("Index", categories);
        }
        #region  Category sections
        [HttpGet("create-category")]
        public IActionResult CreateCategory() => View(new CreateCategoryViewModel());

        [HttpPost("create-category")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CreateCategoryViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // 1) Ana kayıt: try/catch DIŞINDA tanımla -> catch içinde de görünsün
            var category = new Categories { Order = model.Order };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync(); // Id lazım

            try
            {
                // 2) Dilleri DB'den al ve tekilleştir (en küçük Id kanonik)
                var langsRaw = await _context.Langs
                    .AsNoTracking()
                    .Where(l => !string.IsNullOrWhiteSpace(l.LangCode))
                    .Select(l => new { l.Id, l.LangCode })
                    .ToListAsync();
                Console.WriteLine($"langsRaw : {langsRaw}");
                var langs = langsRaw
                    .GroupBy(x => NormalizeLangKey(x.LangCode))
                    .Select(g => new { Id = g.Min(x => x.Id), LangCode = g.OrderBy(x => x.Id).First().LangCode })
                    .ToList();

                var tr = langs.FirstOrDefault(l => NormalizeLangKey(l.LangCode) == "tr");
                if (tr is null) throw new InvalidOperationException("TR language not found in DB.");
                Console.WriteLine($"langsRaw  tr : {tr}");

                // 3) KeyName için EN çeviri (EN tabloya yazılsın/yazılmasın fark etmez)
                var enName = await _translator.TranslateAsync(model.NameTr, "TR", "EN");
                var keyName = TextCaseHelper.ToCamelCaseKey(enName);

                // EN slug (çok nadir fallback için)
                var enSlug = TextCaseHelper.ToLocalizedSlug(enName);
                if (string.IsNullOrWhiteSpace(enSlug))
                    enSlug = TextCaseHelper.ToLocalizedSlug(model.NameTr);

                // 4) Tüm diller için çeviri + Unicode slug + upsert
                foreach (var lang in langs)
                {
                    string valueText;
                    if (lang.Id == tr.Id)
                    {
                        valueText = model.NameTr; // TR orijinal
                    }
                    else
                    {
                        var target = NormalizeToDeepLCode(lang.LangCode);
                        try { valueText = await _translator.TranslateAsync(model.NameTr, "TR", target); }
                        catch { valueText = model.NameTr; } // DeepL yoksa fallback
                    }

                    var localizedSlug = TextCaseHelper.ToLocalizedSlug(valueText);
                    if (string.IsNullOrWhiteSpace(localizedSlug))
                        localizedSlug = enSlug;

                    await UpsertCategoryTranslationAsync(category.Id, lang.Id, keyName, valueText, localizedSlug);
                }

                TempData["CategoryMsg"] = "Kategori oluşturuldu.";
                return RedirectToAction(nameof(Index)); // <-- success path
            }
            catch
            {
                // Hata halinde en az TR kaydı dursun
                var trId = await GetLangIdAsync("tr");
                var fallbackSlug = TextCaseHelper.ToLocalizedSlug(model.NameTr);
                await UpsertCategoryTranslationAsync(category.Id, trId, $"category{category.Id}", model.NameTr, fallbackSlug);

                TempData["CategoryMsg"] = "Kategori oluşturuldu ancak çeviri sırasında hata oluştu.";
                return RedirectToAction(nameof(Index)); // <-- failure path de return ediyor
            }
        }
        // GET: /admin/update-category/{id}
        [HttpGet("update-category/{id:int}")]
        public async Task<IActionResult> UpdateCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category is null) return NotFound();

            // Kanonik dil listesi (duplicate’leri en küçük Id ile tekilleştir)
            var langsRaw = await _context.Langs
                .AsNoTracking()
                .Where(l => !string.IsNullOrWhiteSpace(l.LangCode))
                .Select(l => new { l.Id, l.LangCode })
                .ToListAsync();

            var langs = langsRaw
                .GroupBy(x => NormalizeLangKey(x.LangCode))
                .Select(g => new { Id = g.Min(x => x.Id), LangCode = g.OrderBy(x => x.Id).First().LangCode })
                .OrderBy(x => x.LangCode) // sekme sırası için
                .ToList();

            // Mevcut çevirileri çek
            var existing = await _context.CategoriesTranslations
                .AsNoTracking()
                .Where(t => t.CategoriesId == id)
                .ToListAsync();

            // EN metnini bul (KeyName ön-izleme için)
            var enRow = existing.FirstOrDefault(x => NormalizeLangKey(x.LangCode?.LangCode ?? "") == "en");
            string previewEnText;
            if (enRow != null) previewEnText = enRow.ValueText;
            else
            {
                // EN yoksa TR’den bir önizleme üretelim
                var trRow = existing.FirstOrDefault(x => NormalizeLangKey(x.LangCode?.LangCode ?? "") == "tr");
                previewEnText = trRow?.ValueText ?? existing.FirstOrDefault()?.ValueText ?? "";
            }

            var vm = new UpdateCategoryViewModel
            {
                Id = id,
                Order = category.Order,
                KeyName = TextCaseHelper.ToCamelCaseKey(previewEnText),
                RegenerateKeyNameFromEnglish = true,
                RegenerateEmptySlugs = true
            };

            // Sekmeler: her dil için mevcut değerleri yerleştir
            foreach (var lang in langs)
            {
                var row = existing.FirstOrDefault(t => t.LangCodeId == lang.Id);
                vm.Translations.Add(new CategoryTranslationEdit
                {
                    LangId = lang.Id,
                    LangCode = lang.LangCode,
                    LangDisplay = NormalizeLangKey(lang.LangCode).ToUpperInvariant(),
                    ValueText = row?.ValueText ?? "",
                    Slug = row?.Slug ?? ""
                });
            }

            return View("UpdateCategory", vm);
        }

        // POST: /admin/update-category/{id}
        [HttpPost("update-category/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCategory(int id, UpdateCategoryViewModel model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View("UpdateCategory", model);

            var category = await _context.Categories.FindAsync(id);
            if (category is null) return NotFound();

            // 1) Sıra güncelle
            category.Order = model.Order;

            // 2) EN metni: gelen sekmelerden EN olanı bul (yoksa TR’yi çevir)
            var enEdit = model.Translations.FirstOrDefault(x => BaseLang(x.LangCode) == "en");
            string enTextForKey = enEdit?.ValueText;
            if (string.IsNullOrWhiteSpace(enTextForKey))
            {
                var trEdit = model.Translations.FirstOrDefault(x => BaseLang(x.LangCode) == "tr");
                var trText = trEdit?.ValueText ?? "";
                // EN yoksa TR’yi EN’e çevirip kullan
                enTextForKey = await _translator.TranslateAsync(trText, "TR", "EN");
            }

            // 3) KeyName: checkbox işaretliyse EN’den yeniden üret, değilse modeldeki değeri kullan
            var newKeyName = model.RegenerateKeyNameFromEnglish
                ? TextCaseHelper.ToCamelCaseKey(enTextForKey)
                : (model.KeyName ?? "");

            // 4) Tüm diller için upsert (ValueText & Slug)
            //    - Slug alanını boş bıraktıysan ve RegenerateEmptySlugs=true ise otomatik üretiriz (Unicode)
            //    - Dil içinde slug benzersizliği korunur
            var dbRows = await _context.CategoriesTranslations
                .Where(t => t.CategoriesId == id)
                .ToListAsync();

            foreach (var edit in model.Translations)
            {
                if (string.IsNullOrWhiteSpace(edit.ValueText))
                    continue; // tamamen boş bırakılmışsa pas geç (istersen zorunlu da kılabilirsin)

                var row = dbRows.FirstOrDefault(r => r.LangCodeId == edit.LangId);
                var desiredSlug = edit.Slug;

                if (string.IsNullOrWhiteSpace(desiredSlug) && model.RegenerateEmptySlugs)
                    desiredSlug = TextCaseHelper.ToLocalizedSlug(edit.ValueText);

                if (string.IsNullOrWhiteSpace(desiredSlug))
                    desiredSlug = TextCaseHelper.ToLocalizedSlug(edit.ValueText); // son çare yine üret

                if (row is null)
                {
                    _context.CategoriesTranslations.Add(new CategoriesTranslations
                    {
                        CategoriesId = id,
                        LangCodeId = edit.LangId,
                        KeyName = newKeyName,
                        ValueText = edit.ValueText,
                        Slug = await EnsureUniqueLocalizedSlugAsync(edit.LangId, desiredSlug)
                    });
                }
                else
                {
                    row.KeyName = newKeyName; // tüm dillerde aynı
                    row.ValueText = edit.ValueText;

                    // benzersiz hale getir (aynı dilde slug çakışırsa -2, -3 eklenir)
                    row.Slug = await EnsureUniqueLocalizedSlugAsync(edit.LangId, desiredSlug, row.Id);
                }
            }

            await _context.SaveChangesAsync();

            TempData["CategoryMsg"] = "Kategori güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete-category/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category is null)
            {
                TempData["CategoryError"] = "Kategori bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Bu kategoriye bağlı ürün var mı?
            var hasProducts = await _context.Products
                .AnyAsync(p => p.CategoryId == id); // SubCategoryId yoksa yalnızca CategoryId kontrolü yeter

            if (hasProducts)
            {
                TempData["CategoryError"] = "Bu kategoriye bağlı ürünler var. Önce ürünleri taşıyın veya silin.";
                return RedirectToAction(nameof(Index));
            }

            // Çevirileri temizle (cascade yoksa güvenli yol)
            var translations = await _context.CategoriesTranslations
                .Where(t => t.CategoriesId == id)
                .ToListAsync();

            _context.CategoriesTranslations.RemoveRange(translations);
            _context.Categories.Remove(category);

            try
            {
                await _context.SaveChangesAsync();
                TempData["CategoryMsg"] = "Kategori silindi.";
            }
            catch (DbUpdateException)
            {
                TempData["CategoryError"] = "Kategori silinemedi. Bağlı kayıtlar olabilir.";
            }

            return RedirectToAction(nameof(Index));
        }


        // ===== Helpers =====

        // "en-GB" -> "en", "pt-br" -> "pt", "ru" -> "ru"
        private static string NormalizeLangKey(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            var c = code.Trim().ToLowerInvariant();
            var dash = c.IndexOf('-');
            return dash > 0 ? c[..dash] : c;
        }
        private static string BaseLang(string code) => NormalizeLangKey(code);

        // EF-Core tarafında LIKE ile TR Id getir (StartsWith(StringComparison) çevrilemez)
        private async Task<int> GetLangIdAsync(string code)
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
        private static string NormalizeToDeepLCode(string dbCode)
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

        // ====== PRIVATE HELPERS ======

        private async Task UpsertCategoryTranslationAsync(int categoryId, int langId, string keyName, string valueText, string slug)
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
        private async Task<string> EnsureUniqueLocalizedSlugAsync(int langId, string baseSlug, int? ignoreId = null)
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

        #endregion
    }
}
