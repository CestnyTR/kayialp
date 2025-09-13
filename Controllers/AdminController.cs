using kayialp.Context;
using kayialp.Infrastructure;
using kayialp.Models;
using kayialp.Services;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ProductModels;

namespace kayialp.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly kayialpDbContext _context;
        private readonly ITranslationService _translator;
        private readonly IWebHostEnvironment _env;

        private const string AdminBaseLangCode = "tr"; // Admin arayüzünde TR gösterelim

        public AdminController(kayialpDbContext context, ITranslationService translator, IWebHostEnvironment env)
        {
            _context = context;
            _translator = translator;
            _env = env;

        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Admin TR sabit
            var trId = await _context.Langs
                .AsNoTracking()
                .Where(l => EF.Functions.Like(l.LangCode, "tr%"))
                .OrderBy(l => l.Id)             // kanonik
                .Select(l => l.Id)
                .FirstOrDefaultAsync();

            // Kategoriler (TR)
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Order)
                .Join(
                    _context.CategoriesTranslations.AsNoTracking().Where(t => t.LangCodeId == trId),
                    c => c.Id,
                    t => t.CategoriesId,
                    (c, t) => new CategoryViewModel
                    {
                        Id = c.Id,
                        Name = t.ValueText
                    })
                .ToListAsync();

            // Ürünler (TR adı, TR kategori adı, görseller, slug)
            var products = await (
                from p in _context.Products.AsNoTracking()
                join pt in _context.ProductsTranslations.AsNoTracking()
                        .Where(x => x.LangCodeId == trId)
                    on p.Id equals pt.ProductId
                join ct in _context.CategoriesTranslations.AsNoTracking()
                        .Where(x => x.LangCodeId == trId)
                    on p.CategoryId equals ct.CategoriesId into catJoin
                from ctt in catJoin.DefaultIfEmpty()
                orderby p.Order, p.Id descending
                select new ProductListItemViewModel
                {
                    Id = p.Id,
                    Name = pt.ValueText,
                    CategoryName = ctt != null ? ctt.ValueText : "-",
                    Stock = p.Stock,
                    Order = p.Order,
                    FirstImageUrl = GetFirstImage(p.ImageUrl),
                    ImageCount = GetImageCount(p.ImageUrl),
                    Slug = pt.Slug
                }
            ).ToListAsync();

            var vm = new AdminIndexViewModel
            {
                Categories = categories,
                Products = products
            };

            return View(vm);
        }

        #region  Product sections

        // ===================== CREATE PRODUCT (FULL) =====================
        [HttpGet("create-product")]
        public async Task<IActionResult> CreateProduct()
        {
            var trId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync();

            var categories = await (from c in _context.Categories
                                    join t in _context.CategoriesTranslations on c.Id equals t.CategoriesId
                                    where t.LangCodeId == trId
                                    orderby c.Order, t.ValueText
                                    select new { c.Id, Name = t.ValueText })
                                   .AsNoTracking()
                                   .ToListAsync();

            ViewBag.Categories = new SelectList(categories, "Id", "Name");

            var vm = new CreateProductViewModel
            {
                Attributes = new List<ProductAttributeRow> { new ProductAttributeRow() }
            };
            return View("CreateProduct", vm);
        }
        [HttpPost("create-product")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(CreateProductViewModel model)
        {
            // 0) Dosya/doğrulama
            if (model.Images == null || model.Images.Count < 1 || model.Images.Count > 5)
                ModelState.AddModelError(nameof(model.Images), "Lütfen 1 ile 5 arasında görsel yükleyin.");

            if (!ModelState.IsValid)
            {
                var trIdR = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync();
                var categoriesR = await (from c in _context.Categories
                                         join t in _context.CategoriesTranslations on c.Id equals t.CategoriesId
                                         where t.LangCodeId == trIdR
                                         orderby c.Order, t.ValueText
                                         select new { c.Id, Name = t.ValueText }).ToListAsync();
                ViewBag.Categories = new SelectList(categoriesR, "Id", "Name");
                return View("CreateProduct", model);
            }

            // 1) Ürün ana kaydı
            var product = new Products
            {
                CategoryId = model.CategoryId,
                Stock = model.Stock,
                Order = model.Order
            };
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // 2) Görsellerin sıra/kapak belirlenmesi
            var files = model.Images;
            var ordered = new List<IFormFile>();
            if (!string.IsNullOrWhiteSpace(model.ImageOrder))
            {
                var idxs = model.ImageOrder.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => int.TryParse(s, out var n) ? n : -1)
                                           .Where(n => n >= 0 && n < files.Count)
                                           .ToList();

                foreach (var i in idxs) ordered.Add(files[i]);
                foreach (var (f, i) in files.Select((f, i) => (f, i)))
                    if (!idxs.Contains(i)) ordered.Add(f);
            }
            else
            {
                ordered = files.ToList();
            }

            // coverIndex: varsa onu başa çek
            if (model.CoverIndex.HasValue)
            {
                var ci = model.CoverIndex.Value;
                if (ci >= 0 && ci < files.Count)
                {
                    var coverFile = files[ci];
                    ordered = ordered.Where(f => f != coverFile).Prepend(coverFile).ToList();
                }
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "products", product.Id.ToString());
            Directory.CreateDirectory(folder);

            // Kapak
            product.ImageUrl = await SaveWebpVariantAsync(ordered[0], folder, "cover", 1200, 1200);
            // Güvenlik yaması: metod yanlış klasör döndürürse düzelt
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                product.ImageUrl = product.ImageUrl.Replace("/uploads/categories/", "/uploads/products/");

            // Galeri (kalanlar)
            for (int i = 1; i < ordered.Count && i <= 4; i++)
            {
                var p = await SaveWebpVariantAsync(ordered[i], folder, $"gallery-{i}", 1200, 1200);
                // (gerekirse p üzerinde aynı replace uygulanabilir)
            }
            await _context.SaveChangesAsync();

            // 3) Diller (DB’den kanonikle – CreateCategory ile aynı mantık)
            var langsRaw = await _context.Langs
                .AsNoTracking()
                .Where(l => !string.IsNullOrWhiteSpace(l.LangCode))
                .Select(l => new { l.Id, l.LangCode })
                .ToListAsync();

            var langs = langsRaw
                .GroupBy(x => NormalizeLangKey(x.LangCode))
                .Select(g => new { Id = g.Min(x => x.Id), LangCode = g.OrderBy(x => x.Id).First().LangCode })
                .ToList();

            var tr = langs.FirstOrDefault(l => NormalizeLangKey(l.LangCode) == "tr");
            if (tr is null) throw new InvalidOperationException("TR language not found in DB.");

            // 4) Ürün başlık/slug/alt → TR’den
            var nameTr = (model.NameTr ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nameTr))
                throw new InvalidOperationException("Ürün adı (TR) zorunludur.");

            // KeyName için EN çeviri (CreateCategory ile tutarlılık)
            var enName = await _translator.TranslateAsync(nameTr, "TR", "EN");
            var keyName = TextCaseHelper.ToCamelCaseKey(enName);

            // Slug baz (TR)
            var slugBaseTr = TextCaseHelper.ToLocalizedSlug(nameTr);
            if (string.IsNullOrWhiteSpace(slugBaseTr))
                slugBaseTr = "urun";

            // Görsel ALT metinleri için CSV (görsel sayısı kadar)
            var altListTr = BuildAutoImageAltsList(ordered.Count, nameTr);
            var altsCsvTr = string.Join(", ", altListTr);

            // 5) İçerik blokları (tek ProductContent + 3 blok)
            var content = new ProductContent { ProductId = product.Id, Order = 0, IsActive = true };
            _context.ProductContents.Add(content);
            await _context.SaveChangesAsync();

            var blockShort = new ProductContentBlock { ProductContentId = content.Id, BlockType = "short_desc", Order = 0, IsActive = true };
            var blockDesc = new ProductContentBlock { ProductContentId = content.Id, BlockType = "description", Order = 1, IsActive = true };
            var blockAbout = new ProductContentBlock { ProductContentId = content.Id, BlockType = "about", Order = 2, IsActive = true };
            _context.ProductContentBlocks.AddRange(blockShort, blockDesc, blockAbout);
            await _context.SaveChangesAsync();

            // === TR kayıtları ===
            _context.ProductsTranslations.Add(new ProductsTranslations
            {
                ProductId = product.Id,
                LangCodeId = tr.Id,
                KeyName = keyName,
                ValueText = nameTr,
                Slug = await EnsureUniqueProductSlugAsync(tr.Id, slugBaseTr),
                ImageAlts = altsCsvTr
            });

            _context.ProductContentBlockTranslations.AddRange(
                new ProductContentBlockTranslation { BlockId = blockShort.Id, LangCodeId = tr.Id, Html = model.ShortDescriptionTr ?? "" },
                new ProductContentBlockTranslation { BlockId = blockDesc.Id, LangCodeId = tr.Id, Html = model.DescriptionTr ?? "" },
                new ProductContentBlockTranslation { BlockId = blockAbout.Id, LangCodeId = tr.Id, Html = model.AboutTr ?? "" }
            );

            // 6) Hedef dillere otomatik çeviri
            foreach (var lang in langs.Where(l => l.Id != tr.Id))
            {
                var target = NormalizeToDeepLCode(lang.LangCode);

                // Ad, slug
                string nameT;
                try { nameT = await _translator.TranslateAsync(nameTr, "TR", target); }
                catch { nameT = nameTr; }

                // Dil-özel slug (adı baz al)
                var slugBaseT = TextCaseHelper.ToLocalizedSlug(nameT);
                if (string.IsNullOrWhiteSpace(slugBaseT))
                    slugBaseT = slugBaseTr;
                var slugT = await EnsureUniqueProductSlugAsync(lang.Id, slugBaseT);

                // Alt metinler (CSV eleman bazında çeviri)
                var altListT = new List<string>(altListTr.Count);
                foreach (var alt in altListTr)
                {
                    try { altListT.Add(await _translator.TranslateAsync(alt, "TR", target)); }
                    catch { altListT.Add(alt); }
                }
                var altsCsvT = string.Join(", ", altListT);

                // İçerik blokları
                string shortT, descT, aboutT;
                try { shortT = await _translator.TranslateAsync(model.ShortDescriptionTr ?? "", "TR", target); } catch { shortT = model.ShortDescriptionTr ?? ""; }
                try { descT = await _translator.TranslateAsync(model.DescriptionTr ?? "", "TR", target); } catch { descT = model.DescriptionTr ?? ""; }
                try { aboutT = await _translator.TranslateAsync(model.AboutTr ?? "", "TR", target); } catch { aboutT = model.AboutTr ?? ""; }

                _context.ProductsTranslations.Add(new ProductsTranslations
                {
                    ProductId = product.Id,
                    LangCodeId = lang.Id,
                    KeyName = keyName,          // KeyName sabit kalır (EN bazlı)
                    ValueText = nameT,
                    Slug = slugT,
                    ImageAlts = altsCsvT
                });

                _context.ProductContentBlockTranslations.AddRange(
                    new ProductContentBlockTranslation { BlockId = blockShort.Id, LangCodeId = lang.Id, Html = shortT },
                    new ProductContentBlockTranslation { BlockId = blockDesc.Id, LangCodeId = lang.Id, Html = descT },
                    new ProductContentBlockTranslation { BlockId = blockAbout.Id, LangCodeId = lang.Id, Html = aboutT }
                );
            }

            // 7) Özellikler (TR’den → tüm dillere çeviri)
            if (model.Attributes != null && model.Attributes.Any(a =>
                !string.IsNullOrWhiteSpace(a.NameTr) || !string.IsNullOrWhiteSpace(a.ValueTr)))
            {
                var group = new ProductAttributeGroup { ProductId = product.Id, Order = 0, IsActive = true };
                _context.ProductAttributeGroups.Add(group);
                await _context.SaveChangesAsync();

                foreach (var (row, idx) in model.Attributes.Select((r, i) => (r, i)))
                {
                    if (string.IsNullOrWhiteSpace(row.NameTr) && string.IsNullOrWhiteSpace(row.ValueTr)) continue;

                    var key = TextCaseHelper.ToCamelCaseKey(row.NameTr ?? $"attr{idx + 1}");
                    var attr = new ProductAttribute
                    {
                        GroupId = group.Id,
                        KeyName = key,
                        Order = row.Order
                    };
                    _context.ProductAttributes.Add(attr);
                    await _context.SaveChangesAsync();

                    // TR
                    _context.ProductAttributeTranslations.Add(new ProductAttributeTranslation
                    {
                        AttributeId = attr.Id,
                        LangCodeId = tr.Id,
                        Name = row.NameTr ?? "",
                        Value = row.ValueTr ?? ""
                    });

                    // Hedef diller
                    foreach (var lang in langs.Where(l => l.Id != tr.Id))
                    {
                        var target = NormalizeToDeepLCode(lang.LangCode);
                        string nameT, valueT;
                        try { nameT = await _translator.TranslateAsync(row.NameTr ?? "", "TR", target); } catch { nameT = row.NameTr ?? ""; }
                        try { valueT = await _translator.TranslateAsync(row.ValueTr ?? "", "TR", target); } catch { valueT = row.ValueTr ?? ""; }

                        _context.ProductAttributeTranslations.Add(new ProductAttributeTranslation
                        {
                            AttributeId = attr.Id,
                            LangCodeId = lang.Id,
                            Name = nameT,
                            Value = valueT
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["ProductMsg"] = "Ürün başarıyla eklendi ve tüm dillere çevrildi.";
            return RedirectToAction(nameof(Index));
        }

        // === Yardımcılar ===

        // Görsel sayısı kadar ALT liste üretir: [name, "name - image 2", ...]
        private static List<string> BuildAutoImageAltsList(int count, string productName)
        {
            var list = new List<string>(Math.Max(1, count));
            for (int i = 0; i < Math.Max(1, count); i++)
                list.Add(i == 0 ? productName : $"{productName} - image {i + 1}");
            return list;
        }


        #endregion


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
                // === Görsel işleme ===
                string folder = Path.Combine(_env.WebRootPath, "uploads", "categories", category.Id.ToString());
                Directory.CreateDirectory(folder);

                if (model.CardImage != null && model.CardImage.Length > 0)
                {
                    category.ImageCard312x240 = await SaveWebpVariantAsync(
                        model.CardImage, folder, "card-312x240", 312, 240);
                }

                if (model.ShowcaseImage != null && model.ShowcaseImage.Length > 0)
                {
                    category.ImageShowcase423x636 = await SaveWebpVariantAsync(
                        model.ShowcaseImage, folder, "showcase-423x636", 423, 636);
                }
                await _context.SaveChangesAsync();

                // === Dilleri DB'den al ve tekilleştir (en küçük Id kanonik) ===
                var langsRaw = await _context.Langs
                    .AsNoTracking()
                    .Where(l => !string.IsNullOrWhiteSpace(l.LangCode))
                    .Select(l => new { l.Id, l.LangCode })
                    .ToListAsync();

                var langs = langsRaw
                    .GroupBy(x => NormalizeLangKey(x.LangCode))
                    .Select(g => new { Id = g.Min(x => x.Id), LangCode = g.OrderBy(x => x.Id).First().LangCode })
                    .ToList();

                var tr = langs.FirstOrDefault(l => NormalizeLangKey(l.LangCode) == "tr");
                if (tr is null) throw new InvalidOperationException("TR language not found in DB.");

                // === KeyName için EN çeviri ===
                var enName = await _translator.TranslateAsync(model.NameTr, "TR", "EN");
                var keyName = TextCaseHelper.ToCamelCaseKey(enName);

                var enSlug = TextCaseHelper.ToLocalizedSlug(enName);
                if (string.IsNullOrWhiteSpace(enSlug))
                    enSlug = TextCaseHelper.ToLocalizedSlug(model.NameTr);

                // === Tüm diller için çeviri + Unicode slug ===
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
                        catch { valueText = model.NameTr; } // hata fallback
                    }

                    var localizedSlug = TextCaseHelper.ToLocalizedSlug(valueText);
                    if (string.IsNullOrWhiteSpace(localizedSlug))
                        localizedSlug = enSlug;

                    await UpsertCategoryTranslationAsync(category.Id, lang.Id, keyName, valueText, localizedSlug);
                }

                TempData["CategoryMsg"] = "Kategori oluşturuldu.";
                return RedirectToAction(nameof(Index)); // success
            }
            catch
            {
                // Hata halinde en az TR kaydı dursun
                var trId = await GetLangIdAsync("tr");
                var fallbackSlug = TextCaseHelper.ToLocalizedSlug(model.NameTr);
                await UpsertCategoryTranslationAsync(category.Id, trId, $"category{category.Id}", model.NameTr, fallbackSlug);

                TempData["CategoryMsg"] = "Kategori oluşturuldu ancak çeviri sırasında hata oluştu.";
                return RedirectToAction(nameof(Index)); // failure
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
                .Include(t => t.LangCode)
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
                RegenerateEmptySlugs = true,

                // --- NEW: Görsel yolları ---
                ExistingCardImage = category.ImageCard312x240,
                ExistingShowcaseImage = category.ImageShowcase423x636
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

            category.Order = model.Order;

            // === Görseller ===
            var folder = Path.Combine(_env.WebRootPath, "uploads", "categories", category.Id.ToString());
            Directory.CreateDirectory(folder);

            // Kart görseli
            if (model.NewCardImage != null && model.NewCardImage.Length > 0)
            {
                // Yeni dosya varsa -> eskisini sil, yenisini ekle
                TryDeleteFileByWebPath(category.ImageCard312x240);
                category.ImageCard312x240 = await SaveWebpVariantAsync(
                    model.NewCardImage, folder, "card-312x240", 312, 240);
            }
            else if (string.IsNullOrEmpty(category.ImageCard312x240))
            {
                // Yeni yok, eski de yok → hata
                ModelState.AddModelError("NewCardImage", "Kart görseli zorunludur.");
            }

            // Vitrin görseli
            if (model.NewShowcaseImage != null && model.NewShowcaseImage.Length > 0)
            {
                TryDeleteFileByWebPath(category.ImageShowcase423x636);
                category.ImageShowcase423x636 = await SaveWebpVariantAsync(
                    model.NewShowcaseImage, folder, "showcase-423x636", 423, 636);
            }
            else if (string.IsNullOrEmpty(category.ImageShowcase423x636))
            {
                ModelState.AddModelError("NewShowcaseImage", "Vitrin görseli zorunludur.");
            }

            // Eğer validasyon hataları varsa tekrar view göster
            if (!ModelState.IsValid)
                return View("UpdateCategory", model);

            // === Çeviriler ===
            var dbRows = await _context.CategoriesTranslations
                .Where(t => t.CategoriesId == id)
                .ToListAsync();

            if (model.GenerateFromTurkish)
            {
                var trEdit = model.Translations.FirstOrDefault(x => BaseLang(x.LangCode) == "tr");
                var trText = trEdit?.ValueText ?? "";

                foreach (var edit in model.Translations)
                {
                    if (BaseLang(edit.LangCode) == "tr")
                    {
                        // TR olduğu gibi kalsın
                        edit.ValueText = trText;
                        edit.Slug = TextCaseHelper.ToLocalizedSlug(trText);
                    }
                    else
                    {
                        try
                        {
                            var target = NormalizeToDeepLCode(edit.LangCode);
                            var translated = await _translator.TranslateAsync(trText, "TR", target);
                            edit.ValueText = translated;
                            edit.Slug = TextCaseHelper.ToLocalizedSlug(translated);
                        }
                        catch
                        {
                            // Çeviri hatası → fallback TR
                            edit.ValueText = trText;
                            edit.Slug = TextCaseHelper.ToLocalizedSlug(trText);
                        }
                    }
                }
            }


            // KeyName – EN veya TR’den üret
            var enEdit = model.Translations.FirstOrDefault(x => BaseLang(x.LangCode) == "en");
            string keySourceText = enEdit?.ValueText;
            if (string.IsNullOrWhiteSpace(keySourceText))
                keySourceText = model.Translations.FirstOrDefault(x => BaseLang(x.LangCode) == "tr")?.ValueText ?? "";

            var newKeyName = TextCaseHelper.ToCamelCaseKey(keySourceText);

            foreach (var edit in model.Translations)
            {
                if (string.IsNullOrWhiteSpace(edit.ValueText)) continue;

                var row = dbRows.FirstOrDefault(r => r.LangCodeId == edit.LangId);
                var slug = string.IsNullOrWhiteSpace(edit.Slug)
                    ? TextCaseHelper.ToLocalizedSlug(edit.ValueText)
                    : edit.Slug;

                if (row is null)
                {
                    _context.CategoriesTranslations.Add(new CategoriesTranslations
                    {
                        CategoriesId = id,
                        LangCodeId = edit.LangId,
                        KeyName = newKeyName,
                        ValueText = edit.ValueText,
                        Slug = await EnsureUniqueLocalizedSlugAsync(edit.LangId, slug)
                    });
                }
                else
                {
                    row.KeyName = newKeyName;
                    row.ValueText = edit.ValueText;
                    row.Slug = await EnsureUniqueLocalizedSlugAsync(edit.LangId, slug, row.Id);
                }
            }

            await _context.SaveChangesAsync();
            TempData["CategoryMsg"] = "Kategori güncellendi.";
            return RedirectToAction(nameof(Index));
        }



        // POST: /admin/delete-category/{id}
        [HttpPost("delete-category/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category is null) return NotFound();

            // 1) Ürün var mı kontrolü
            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                TempData["CategoryMsg"] = "Bu kategoride ürün bulunduğu için silinemez.";
                return RedirectToAction(nameof(Index));
            }

            // 2) Çeviri kayıtlarını sil
            var translations = _context.CategoriesTranslations.Where(t => t.CategoriesId == id);
            _context.CategoriesTranslations.RemoveRange(translations);

            // 3) Resim dosyalarını sil
            if (!string.IsNullOrEmpty(category.ImageCard312x240))
                TryDeleteFileByWebPath(category.ImageCard312x240);

            if (!string.IsNullOrEmpty(category.ImageShowcase423x636))
                TryDeleteFileByWebPath(category.ImageShowcase423x636);

            // 4) Kategori kaydını sil
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["CategoryMsg"] = "Kategori başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region helpers
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

        // ======= Helpers =======

        private async Task<IEnumerable<SelectListItem>> GetCategorySelectListAsync()
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
        private async Task<List<(int Id, string Code)>> GetCanonicalLangsAsync()
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
        private async Task<string> EnsureUniqueProductSlugAsync(int langId, string baseSlug, int? ignoreId = null)
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
        private static List<string> SplitCsv(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();
            return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }

        private static string NormalizeImageUrlCsv(string? csv)
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

        private static string NormalizeImageAltsCsv(string? csv, int imageCount)
        {
            var list = SplitCsv(csv).Select(s => s.Replace(",", " ").Trim()).ToList();
            if (list.Count > imageCount) list = list.Take(imageCount).ToList();
            while (list.Count < imageCount) list.Add(string.Empty);
            return string.Join(",", list);
        }
        // Alt text otomatik üretimi (CSV)
        private static string BuildAutoImageAltsCsv(string productNameLocalized, int imageCount, string baseLang)
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


        private static readonly HashSet<string> _allowedImageExts =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif" };

        // 10 MB
        private const long MAX_IMAGE_BYTES = 10 * 1024 * 1024;


        // Dosya adı gövdesini sadeleştirir (ASCII + tire)
        private static string SlugifyFileBase(string? name)
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

        private static string GetFirstImage(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return "";
            var arr = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return arr.Length > 0 ? arr[0] : "";
        }
        private static int GetImageCount(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return 0;
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        }
        // --- NEW: central crop + exact size + webp save ---
        private async Task<string> SaveWebpVariantAsync(IFormFile file, string folder, string fileNameNoExt, int width, int height)
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
            var rel = $"/uploads/categories/{Path.GetFileName(folder)}/{fileNameNoExt}.webp";
            return rel.Replace("\\", "/");
        }

        private void TryDeleteFileByWebPath(string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath)) return;
            var abs = Path.Combine(_env.WebRootPath, webPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (System.IO.File.Exists(abs))
            {
                System.IO.File.Delete(abs);
            }
        }
        #endregion

    }
}

