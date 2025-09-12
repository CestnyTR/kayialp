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
            var vm = new CreateProductFullViewModel
            {
                Categories = await GetCategorySelectListAsync(),

                // İki sabit CKEditor alanı (boş başlayabilir)
                DescriptionTrHtml = string.Empty, // Açıklama
                AboutTrHtml = string.Empty, // Ürün Hakkında

                // Ürüne bağlı (detaydan bağımsız) özellik listesi – başlangıçta 1 satır
                Features = new List<ProductFeatureInput>
        {
            new ProductFeatureInput { TextTr = string.Empty, Order = 0 }
        }
            };

            return View("CreateProduct", vm);
        }
        [HttpPost("create-product")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(CreateProductFullViewModel model)
        {
            // 1) Validasyon: görseller
            if (model.Images is null || model.Images.Count == 0)
                ModelState.AddModelError(nameof(model.Images), "En az bir görsel yükleyin.");

            if (model.Images?.Count > 5)
                ModelState.AddModelError(nameof(model.Images), "En fazla 5 görsel yükleyebilirsiniz.");

            // 2) Validasyon: en az bir detay
            if (string.IsNullOrWhiteSpace(model.DescriptionTrHtml) &&
                string.IsNullOrWhiteSpace(model.AboutTrHtml))
            {
                ModelState.AddModelError(string.Empty, "En az bir detay (Açıklama veya Ürün Hakkında) doldurulmalı.");
            }

            if (!ModelState.IsValid)
            {
                model.Categories = await GetCategorySelectListAsync();
                return View("CreateProduct", model);
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 3) Görselleri kaydet
                var imageUrls = await SaveImagesAsync(model.Images);
                if (imageUrls.Count == 0)
                {
                    ModelState.AddModelError(nameof(model.Images), "Geçerli bir görsel yüklenemedi (uzantı/boyut).");
                    model.Categories = await GetCategorySelectListAsync();
                    return View("CreateProduct", model);
                }

                var imageCsv = string.Join(",", imageUrls);
                var imageCount = imageUrls.Count;

                // 4) Ürün çekirdeği
                var product = new Products
                {
                    CategoryId = model.CategoryId,
                    Stock = model.Stock,
                    Order = model.Order,
                    ImageUrl = imageCsv
                };
                _context.Products.Add(product);
                await _context.SaveChangesAsync(); // Id lazım

                // 5) Diller
                var langs = await GetCanonicalLangsAsync();
                var tr = langs.FirstOrDefault(l => NormalizeLangKey(l.Code) == "tr");
                if (tr.Equals(default((int Id, string Code))))
                    throw new InvalidOperationException("TR language not found.");

                // 6) KeyName & EN kanonik slug
                string enName;
                try
                {
                    enName = await _translator.TranslateAsync(model.NameTr, "TR", "EN");
                    if (string.IsNullOrWhiteSpace(enName)) enName = model.NameTr;
                }
                catch { enName = model.NameTr; }

                var keyName = TextCaseHelper.ToCamelCaseKey(enName);

                var canonicalSlug = TextCaseHelper.ToLocalizedSlug(enName);
                if (string.IsNullOrWhiteSpace(canonicalSlug))
                    canonicalSlug = TextCaseHelper.ToLocalizedSlug(model.NameTr);
                if (string.IsNullOrWhiteSpace(canonicalSlug))
                    canonicalSlug = "product-" + Guid.NewGuid().ToString("N")[..8];

                var productSlugProp = product.GetType().GetProperty("Slug");
                if (productSlugProp != null && productSlugProp.CanWrite)
                {
                    productSlugProp.SetValue(product, canonicalSlug);
                    await _context.SaveChangesAsync();
                }

                // 7) Ürün çevirileri (ad, slug, alt text otomatik)
                foreach (var lang in langs)
                {
                    string valueText;
                    if (lang.Id == tr.Id) valueText = model.NameTr;
                    else
                    {
                        var target = NormalizeToDeepLCode(lang.Code);
                        try { valueText = await _translator.TranslateAsync(model.NameTr, "TR", target); }
                        catch { valueText = model.NameTr; }
                    }

                    var localizedSlug = TextCaseHelper.ToLocalizedSlug(valueText);
                    if (string.IsNullOrWhiteSpace(localizedSlug)) localizedSlug = canonicalSlug;
                    localizedSlug = await EnsureUniqueProductSlugAsync(lang.Id, localizedSlug);

                    var imageAltsCsv = BuildAutoImageAltsCsv(valueText, imageCount, NormalizeLangKey(lang.Code));

                    _context.ProductsTranslations.Add(new ProductsTranslations
                    {
                        ProductId = product.Id,
                        LangCodeId = lang.Id,
                        KeyName = keyName,
                        ValueText = valueText,
                        Slug = localizedSlug,
                        ImageAlts = imageAltsCsv
                    });
                }
                await _context.SaveChangesAsync();

                // 8) Detaylar (Order: 0 = Açıklama, 1 = Ürün Hakkında)
                int nextOrder = 0;
                if (!string.IsNullOrWhiteSpace(model.DescriptionTrHtml))
                    await AddDetailAsync(product.Id, model.DescriptionTrHtml!, nextOrder++, langs, tr.Id);

                if (!string.IsNullOrWhiteSpace(model.AboutTrHtml))
                    await AddDetailAsync(product.Id, model.AboutTrHtml!, nextOrder++, langs, tr.Id);

                // 9) Feature'lar (ürün bazlı)
                await AddFeaturesAsync(product.Id, model.Features ?? new List<ProductFeatureInput>(), langs, tr.Id);

                // 10) Commit + Redirect
                await tx.CommitAsync();
                TempData["ProductMsg"] = "Ürün oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.Error.WriteLine("CreateProduct ERROR: " + ex);
                TempData["ProductError"] = "Ürün oluşturulurken hata oluştu: " + ex.Message;
                model.Categories = await GetCategorySelectListAsync();
                return View("CreateProduct", model);
            }
        }
        // ========== UPDATE PRODUCT – GET ==========
        [HttpGet("update-product/{id:int}")]
        public async Task<IActionResult> UpdateProduct(int id)
        {
            // TR sabit
            var langs = await GetCanonicalLangsAsync();
            var tr = langs.FirstOrDefault(l => NormalizeLangKey(l.Code) == "tr");
            if (tr == default) return NotFound("TR language not found.");

            var p = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();

            var ptTr = await _context.ProductsTranslations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == id && x.LangCodeId == tr.Id);

            // Detaylar (Order 0= Açıklama, 1= Ürün Hakkında)
            var details = await _context.ProductDetails
                .AsNoTracking()
                .Where(d => d.ProductId == id)
                .ToListAsync();

            var det0 = details.FirstOrDefault(d => d.Order == 0);
            var det1 = details.FirstOrDefault(d => d.Order == 1);

            var det0Tr = det0 == null ? null :
                await _context.ProductDetailsTranslations.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ProductDetailsId == det0.Id && t.LangCodeId == tr.Id);

            var det1Tr = det1 == null ? null :
                await _context.ProductDetailsTranslations.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ProductDetailsId == det1.Id && t.LangCodeId == tr.Id);

            // Özellikler (TR)
            var feats = await (
                from f in _context.ProductDetailsFeatures.AsNoTracking().Where(x => x.ProductId == id)
                join ft in _context.ProductDetailsFeatureTranslations.AsNoTracking().Where(x => x.LangCodeId == tr.Id)
                     on f.Id equals ft.ProductDetailsFeatureId
                orderby f.Order, f.Id
                select new FeatureRowVM
                {
                    Id = f.Id,
                    TextTr = ft.ValueText,
                    Order = f.Order
                }).ToListAsync();

            var vm = new UpdateProductViewModel
            {
                Id = p.Id,
                CategoryId = p.CategoryId,
                NameTr = ptTr?.ValueText ?? "",
                Stock = p.Stock,
                Order = p.Order,
                ExistingImages = SplitCsv(p.ImageUrl),
                CoverImagePath = SplitCsv(p.ImageUrl).FirstOrDefault(),
                DescriptionTrHtml = det0Tr?.ValueText,
                AboutTrHtml = det1Tr?.ValueText,
                Features = feats,
                Categories = await GetCategorySelectListAsync()
            };

            return View("UpdateProduct", vm);
        }

        // ========== UPDATE PRODUCT – POST ==========
        [HttpPost("update-product/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(int id, UpdateProductViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var langs = await GetCanonicalLangsAsync();
            var tr = langs.First(l => NormalizeLangKey(l.Code) == "tr");

            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == id);
            if (product is null) return NotFound();

            // 1) Mevcut + silinecekleri çıkar
            var existingAll = SplitCsv(product.ImageUrl);
            var removeSet = new HashSet<string>(model.RemoveImagePaths ?? new(), StringComparer.OrdinalIgnoreCase);
            var existingAfter = existingAll.Where(x => !removeSet.Contains(x)).ToList();

            // 🔧 2) "NewImages" zorunluluğunu dinamik yönet
            // Önce eski hatayı temizle (kalıntı olmasın)
            ModelState.Remove(nameof(UpdateProductViewModel.NewImages));

            // 🔧 3) FINALDE EN AZ 1 GÖRSEL KALSIN: 
            bool anyNewPosted = (model.NewImages ?? new()).Any(f => f != null && f.Length > 0);
            // if (existingAfter.Count == 0 && !anyNewPosted)
            // {
            //     ModelState.AddModelError(nameof(UpdateProductViewModel.NewImages),
            //         "En az bir görsel zorunludur. Mevcutları sildiyseniz en az bir yeni görsel ekleyin.");
            //     model.Categories = await GetCategorySelectListAsync();
            //     model.ExistingImages = existingAfter;
            //     return View("UpdateProduct", model);
            // }

            // 4) Kaç yeni dosyaya izin var? (toplam 5 sınırı)
            var allowNew = Math.Max(0, 5 - existingAfter.Count);
            var postedNew = (model.NewImages ?? new())
                            .Where(f => f != null && f.Length > 0)
                            .Take(allowNew)
                            .ToList();

            List<string> savedNew = new();
            if (postedNew.Count > 0)
            {
                savedNew = await SaveImagesAsync(postedNew, ModelState);
            }
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 5) Yeni dosyaları kaydet (varsa)
                if (postedNew.Count > 0)
                    savedNew = await SaveImagesAsync(postedNew, ModelState);

                // 6) Final liste
                var finalImages = existingAfter.Concat(savedNew).ToList();

                // Emniyet: herhangi bir nedenle boş kaldıysa yakala
                if (finalImages.Count == 0)
                    throw new InvalidOperationException("En az bir görsel kalmalı.");

                // 7) Kapak seçili ama final listede yoksa, ilkini kapak say
                if (string.IsNullOrWhiteSpace(model.CoverImagePath) ||
                    !finalImages.Contains(model.CoverImagePath, StringComparer.OrdinalIgnoreCase))
                {
                    model.CoverImagePath = finalImages[0];
                }
                else
                {
                    // Kapak başa alınsın
                    finalImages.Remove(model.CoverImagePath);
                    finalImages.Insert(0, model.CoverImagePath);
                }

                // Ürün alanları
                product.CategoryId = model.CategoryId;
                product.Stock = model.Stock;
                product.Order = model.Order;
                product.ImageUrl = string.Join(",", finalImages);

                await _context.SaveChangesAsync();

                // Ürün TR çevirisi
                var ptTr = await _context.ProductsTranslations
                    .FirstOrDefaultAsync(x => x.ProductId == product.Id && x.LangCodeId == tr.Id);

                if (ptTr == null)
                {
                    ptTr = new ProductsTranslations
                    {
                        ProductId = product.Id,
                        LangCodeId = tr.Id,
                        KeyName = ptTr?.KeyName ?? "product" + product.Id // yoksa basit default
                    };
                    _context.ProductsTranslations.Add(ptTr);
                }
                ptTr.ValueText = model.NameTr;

                // Slug (TR)
                if (model.RegenerateEmptySlugs || string.IsNullOrWhiteSpace(ptTr.Slug))
                {
                    var trSlug = TextCaseHelper.ToLocalizedSlug(model.NameTr);
                    ptTr.Slug = await EnsureUniqueProductSlugAsync(tr.Id, trSlug, ptTr.Id);
                }

                // ImageAlts (TR)
                ptTr.ImageAlts = BuildAutoImageAltsCsv(ptTr.ValueText, finalImages.Count, "tr");
                await _context.SaveChangesAsync();

                // Diğer diller (opsiyonel yeniden üret)
                if (model.RegenerateOtherLangs)
                {
                    foreach (var lang in langs.Where(l => l.Id != tr.Id))
                    {
                        var pt = await _context.ProductsTranslations
                            .FirstOrDefaultAsync(x => x.ProductId == product.Id && x.LangCodeId == lang.Id);

                        if (pt == null)
                        {
                            pt = new ProductsTranslations
                            {
                                ProductId = product.Id,
                                LangCodeId = lang.Id,
                                KeyName = ptTr.KeyName // ürün anahtarı aynı
                            };
                            _context.ProductsTranslations.Add(pt);
                        }

                        string valueText = model.NameTr;
                        try
                        {
                            var t = await _translator.TranslateAsync(model.NameTr, "TR", NormalizeToDeepLCode(lang.Code));
                            if (!string.IsNullOrWhiteSpace(t)) valueText = t;
                        }
                        catch { /* TR fallback */ }

                        pt.ValueText = valueText;

                        var slug = TextCaseHelper.ToLocalizedSlug(valueText);
                        if (string.IsNullOrWhiteSpace(slug))
                            slug = ptTr.Slug; // en azından bir şey olsun
                        pt.Slug = await EnsureUniqueProductSlugAsync(lang.Id, slug, pt.Id);

                        pt.ImageAlts = BuildAutoImageAltsCsv(valueText, finalImages.Count, NormalizeLangKey(lang.Code));
                    }
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Yalnızca alts güncelle (resim sayısı değiştiyse faydalı)
                    var others = await _context.ProductsTranslations
                        .Where(x => x.ProductId == product.Id && x.LangCodeId != tr.Id)
                        .ToListAsync();

                    foreach (var pt in others)
                    {
                        // Dil kodunu bulalım
                        var code = langs.FirstOrDefault(l => l.Id == pt.LangCodeId).Code;
                        var lang2 = NormalizeLangKey(code);
                        pt.ImageAlts = BuildAutoImageAltsCsv(pt.ValueText, finalImages.Count, lang2);
                    }
                    await _context.SaveChangesAsync();
                }

                // Detaylar (0/1)
                await UpsertDetailHtmlAsync(product.Id, order: 0, trHtml: model.DescriptionTrHtml ?? "", langs, tr.Id, model.RegenerateOtherLangs);
                await UpsertDetailHtmlAsync(product.Id, order: 1, trHtml: model.AboutTrHtml ?? "", langs, tr.Id, model.RegenerateOtherLangs);

                // Özellikler
                var postedFeatures = RecoverFeaturesFromForm(model.Features); // binder güvenlik ağı
                await UpsertFeaturesAsync(product.Id, postedFeatures, langs, tr.Id, model.RegenerateOtherLangs);

                await tx.CommitAsync();
                TempData["ProductMsg"] = "Ürün güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // (opsiyonel: savedNew dosyalarını silme bloğu sizde zaten var)
                TempData["ProductError"] = "Ürün güncellenemedi: " + ex.Message;
                model.Categories = await GetCategorySelectListAsync();
                model.ExistingImages = existingAfter; // kullanıcıya kalanları göster
                return View("UpdateProduct", model);
            }
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

        // Görsel kaydetme (max 5), relative path listesi döner


        // TR HTML → düz metin
        private static string StripHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            return Regex.Replace(html, "<.*?>", string.Empty)
                        .Replace("&nbsp;", " ")
                        .Trim();
        }

        // Düz metni basit <p> bloklarına sar
        private static string PlainToParagraphs(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var parts = text.Replace("\r\n", "\n").Split('\n');
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length == 0) continue;
                sb.Append("<p>").Append(WebUtility.HtmlEncode(t)).Append("</p>");
            }
            return sb.ToString();
        }

        // Detay ekle (Order = 0/1)
        private async Task AddDetailAsync(
            int productId,
            string trHtml,
            int order,
            List<(int Id, string Code)> langs,
            int trId)
        {
            var pd = new ProductDetails
            {
                ProductId = productId,
                Order = order
            };
            _context.ProductDetails.Add(pd);
            await _context.SaveChangesAsync(); // pd.Id

            // TR HTML → aynen kaydet
            _context.ProductDetailsTranslations.Add(new ProductDetailsTranslations
            {
                ProductDetailsId = pd.Id,
                LangCodeId = trId,
                ValueText = trHtml ?? string.Empty
            });

            // Diğer diller: TR HTML -> strip -> çeviri -> <p> blokları
            var trPlain = StripHtml(trHtml);

            foreach (var lang in langs.Where(l => l.Id != trId))
            {
                string translated = trPlain;
                var target = NormalizeToDeepLCode(lang.Code);
                try
                {
                    translated = await _translator.TranslateAsync(trPlain, "TR", target);
                    if (string.IsNullOrWhiteSpace(translated)) translated = trPlain;
                }
                catch { translated = trPlain; }

                var htmlParagraphs = PlainToParagraphs(translated);

                _context.ProductDetailsTranslations.Add(new ProductDetailsTranslations
                {
                    ProductDetailsId = pd.Id,
                    LangCodeId = lang.Id,
                    ValueText = string.IsNullOrWhiteSpace(htmlParagraphs)
                                    ? System.Net.WebUtility.HtmlEncode(translated)
                                    : htmlParagraphs
                });
            }

            await _context.SaveChangesAsync();
        }

        // Ürüne bağlı feature’ları ekle
        private async Task AddFeaturesAsync(
        int productId,
        IEnumerable<ProductFeatureInput> features,
        List<(int Id, string Code)> langs,
        int trId)
        {
            // 1) Gelenleri temizle + sıralamayı belirle
            var clean = (features ?? Enumerable.Empty<ProductFeatureInput>())
                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.TextTr))
                .Select((f, i) => new
                {
                    TextTr = f.TextTr.Trim(),
                    Order = f.Order > 0 ? f.Order : i
                })
                .ToList();

            if (clean.Count == 0) return;

            // 2) Her özellik için ProductDetailsFeatures kaydı oluştur (ProductId + Order)
            var featureRows = clean.Select(c => new ProductDetailsFeatures
            {
                ProductId = productId,
                Order = c.Order
            }).ToList();

            _context.ProductDetailsFeatures.AddRange(featureRows);
            await _context.SaveChangesAsync(); // <-- Id'ler doldu

            // 3) Çeviri kayıtlarını toplu hazırla (TR + diğer diller)
            var transRows = new List<ProductDetailsFeatureTranslations>(clean.Count * langs.Count);

            for (int idx = 0; idx < clean.Count; idx++)
            {
                var baseTextTr = clean[idx].TextTr;
                var fRow = featureRows[idx];

                // TR kaydı
                transRows.Add(new ProductDetailsFeatureTranslations
                {
                    ProductDetailsFeatureId = fRow.Id,
                    LangCodeId = trId,
                    ValueText = baseTextTr
                });

                // Diğer diller
                foreach (var lang in langs)
                {
                    if (lang.Id == trId) continue;

                    string text = baseTextTr;
                    try
                    {
                        var target = NormalizeToDeepLCode(lang.Code);
                        var t = await _translator.TranslateAsync(baseTextTr, "TR", target);
                        if (!string.IsNullOrWhiteSpace(t)) text = t;
                    }
                    catch
                    {
                        // çeviri hatasında TR'ye düş
                        text = baseTextTr;
                    }

                    transRows.Add(new ProductDetailsFeatureTranslations
                    {
                        ProductDetailsFeatureId = fRow.Id,
                        LangCodeId = lang.Id,
                        ValueText = text
                    });
                }
            }

            // 4) Toplu insert
            _context.ProductDetailsFeatureTranslations.AddRange(transRows);
            await _context.SaveChangesAsync();
        }



        private static readonly HashSet<string> _allowedImageExts =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif" };

        // 10 MB
        private const long MAX_IMAGE_BYTES = 10 * 1024 * 1024;

        // Çoklu görsel kaydeder, göreli yolları döner (örn. /uploads/products/2025/09/xxx.jpg)
        private async Task<List<string>> SaveImagesAsync(
    IEnumerable<IFormFile> files,
    ModelStateDictionary? modelState = null)
        {
            // 🔧 Fallback: Binder tek dosya bind ederse Form.Files'tan tümünü al
            var list = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            if (list.Count == 0 && Request?.Form?.Files?.Count > 0)
                list = Request.Form.Files.Where(f => f != null && f.Length > 0).ToList();

            // (İsterseniz debug)
            Console.WriteLine($"[Upload] Posted files: model={(files == null ? 0 : files.Count())}, form={Request?.Form?.Files?.Count ?? 0}");

            var saved = new List<string>();
            if (list.Count == 0) return saved;

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var now = DateTime.UtcNow;
            var relFolder = $"/uploads/products/{now:yyyy}/{now:MM}";
            var absFolder = Path.Combine(webRoot, relFolder.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(absFolder);

            foreach (var f in list)
            {
                if (f.Length > MAX_IMAGE_BYTES)
                {
                    modelState?.AddModelError(nameof(CreateProductFullViewModel.Images),
                        $"{f.FileName} 10 MB sınırını aşıyor.");
                    continue;
                }

                var ext = Path.GetExtension(f.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !_allowedImageExts.Contains(ext))
                {
                    modelState?.AddModelError(nameof(CreateProductFullViewModel.Images),
                        $"{f.FileName} desteklenmeyen uzantı. İzin verilen: .jpg, .jpeg, .png, .webp, .jfif");
                    continue;
                }

                var safeBase = SlugifyFileBase(Path.GetFileNameWithoutExtension(f.FileName));
                if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "img";
                var unique = $"{now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}".Substring(0, 22);
                var fileName = $"{safeBase}_{unique}{ext.ToLowerInvariant()}";

                var absPath = Path.Combine(absFolder, fileName);
                using (var stream = System.IO.File.Create(absPath))
                    await f.CopyToAsync(stream);

                var relPath = $"{relFolder}/{fileName}".Replace("\\", "/");
                saved.Add(relPath);
            }

            Console.WriteLine($"[Upload] Saved images: {saved.Count}");
            return saved;
        }

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





        private List<FeatureRowVM> RecoverFeaturesFromForm(List<FeatureRowVM> features)
        {
            var list = features?.ToList() ?? new List<FeatureRowVM>();
            if (Request.Form["Features.Index"].Count > 0)
            {
                var rec = new List<FeatureRowVM>();
                foreach (var idx in Request.Form["Features.Index"])
                {
                    var idStr = Request.Form[$"Features[{idx}].Id"].FirstOrDefault() ?? "0";
                    var txt = Request.Form[$"Features[{idx}].TextTr"].FirstOrDefault();
                    var ordStr = Request.Form[$"Features[{idx}].Order"].FirstOrDefault() ?? "0";
                    var delStr = Request.Form[$"Features[{idx}].Delete"].FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(txt) && string.IsNullOrWhiteSpace(delStr)) continue;

                    rec.Add(new FeatureRowVM
                    {
                        Id = int.TryParse(idStr, out var idv) ? idv : 0,
                        TextTr = txt?.Trim() ?? "",
                        Order = int.TryParse(ordStr, out var ov) ? ov : 0,
                        Delete = string.Equals(delStr, "true", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(delStr, "on", StringComparison.OrdinalIgnoreCase)
                    });
                }
                if (rec.Count > 0) list = rec;
            }
            return list;
        }

        // Detay upsert (TR + opsiyon diğer diller)
        private async Task UpsertDetailHtmlAsync(
            int productId,
            int order,
            string trHtml,
            List<(int Id, string Code)> langs,
            int trId,
            bool regenOthers)
        {
            // ProductDetails
            var d = await _context.ProductDetails.FirstOrDefaultAsync(x => x.ProductId == productId && x.Order == order);
            if (d == null)
            {
                d = new ProductDetails { ProductId = productId, Order = order };
                _context.ProductDetails.Add(d);
                await _context.SaveChangesAsync();
            }

            // TR
            var dtr = await _context.ProductDetailsTranslations
                .FirstOrDefaultAsync(x => x.ProductDetailsId == d.Id && x.LangCodeId == trId);
            if (dtr == null)
                _context.ProductDetailsTranslations.Add(new ProductDetailsTranslations { ProductDetailsId = d.Id, LangCodeId = trId, ValueText = trHtml ?? "" });
            else
                dtr.ValueText = trHtml ?? "";

            await _context.SaveChangesAsync();

            if (!regenOthers) return;

            var trPlain = StripHtml(trHtml ?? "");
            foreach (var lang in langs.Where(l => l.Id != trId))
            {
                var row = await _context.ProductDetailsTranslations
                    .FirstOrDefaultAsync(x => x.ProductDetailsId == d.Id && x.LangCodeId == lang.Id);

                string text = trPlain;
                try
                {
                    var t = await _translator.TranslateAsync(trPlain, "TR", NormalizeToDeepLCode(lang.Code));
                    if (!string.IsNullOrWhiteSpace(t)) text = t;
                }
                catch { /* tr fallback */ }

                if (row == null)
                    _context.ProductDetailsTranslations.Add(new ProductDetailsTranslations { ProductDetailsId = d.Id, LangCodeId = lang.Id, ValueText = PlainToParagraphs(text) });
                else
                    row.ValueText = PlainToParagraphs(text);
            }
            await _context.SaveChangesAsync();
        }

        // Özellik upsert/sil (TR + opsiyon diğer diller)
        private async Task UpsertFeaturesAsync(
            int productId,
            List<FeatureRowVM> features,
            List<(int Id, string Code)> langs,
            int trId,
            bool regenOthers)
        {
            // Mevcut feature id’leri
            var existingIds = await _context.ProductDetailsFeatures
                .Where(x => x.ProductId == productId)
                .Select(x => x.Id)
                .ToListAsync();

            // Update / Delete / Insert
            foreach (var f in features)
            {
                if (f.Id > 0)
                {
                    // Mevcut
                    var row = await _context.ProductDetailsFeatures.FirstOrDefaultAsync(x => x.Id == f.Id && x.ProductId == productId);
                    if (row == null) continue;

                    if (f.Delete)
                    {
                        // çeviriler silinsin
                        var trs = _context.ProductDetailsFeatureTranslations.Where(t => t.ProductDetailsFeatureId == row.Id);
                        _context.ProductDetailsFeatureTranslations.RemoveRange(trs);
                        _context.ProductDetailsFeatures.Remove(row);
                        await _context.SaveChangesAsync();
                        continue;
                    }

                    // update order
                    row.Order = f.Order;

                    // TR çeviri
                    var trRow = await _context.ProductDetailsFeatureTranslations
                        .FirstOrDefaultAsync(t => t.ProductDetailsFeatureId == row.Id && t.LangCodeId == trId);
                    if (trRow == null)
                        _context.ProductDetailsFeatureTranslations.Add(new ProductDetailsFeatureTranslations { ProductDetailsFeatureId = row.Id, LangCodeId = trId, ValueText = f.TextTr });
                    else
                        trRow.ValueText = f.TextTr;

                    await _context.SaveChangesAsync();

                    if (regenOthers)
                    {
                        foreach (var lang in langs.Where(l => l.Id != trId))
                        {
                            var r = await _context.ProductDetailsFeatureTranslations
                                .FirstOrDefaultAsync(t => t.ProductDetailsFeatureId == row.Id && t.LangCodeId == lang.Id);

                            string val = f.TextTr;
                            try
                            {
                                var t = await _translator.TranslateAsync(f.TextTr, "TR", NormalizeToDeepLCode(lang.Code));
                                if (!string.IsNullOrWhiteSpace(t)) val = t;
                            }
                            catch { /* tr fallback */ }

                            if (r == null)
                                _context.ProductDetailsFeatureTranslations.Add(new ProductDetailsFeatureTranslations { ProductDetailsFeatureId = row.Id, LangCodeId = lang.Id, ValueText = val });
                            else
                                r.ValueText = val;
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Yeni
                    if (f.Delete || string.IsNullOrWhiteSpace(f.TextTr)) continue;

                    var nf = new ProductDetailsFeatures { ProductId = productId, Order = f.Order };
                    _context.ProductDetailsFeatures.Add(nf);
                    await _context.SaveChangesAsync();

                    // TR
                    _context.ProductDetailsFeatureTranslations.Add(new ProductDetailsFeatureTranslations
                    {
                        ProductDetailsFeatureId = nf.Id,
                        LangCodeId = trId,
                        ValueText = f.TextTr
                    });

                    // Diğer diller
                    foreach (var lang in langs.Where(l => l.Id != trId))
                    {
                        string val = f.TextTr;
                        try
                        {
                            var t = await _translator.TranslateAsync(f.TextTr, "TR", NormalizeToDeepLCode(lang.Code));
                            if (!string.IsNullOrWhiteSpace(t)) val = t;
                        }
                        catch { /* tr fallback */ }

                        _context.ProductDetailsFeatureTranslations.Add(new ProductDetailsFeatureTranslations
                        {
                            ProductDetailsFeatureId = nf.Id,
                            LangCodeId = lang.Id,
                            ValueText = val
                        });
                    }
                    await _context.SaveChangesAsync();
                }
            }
        }


        #endregion
    }
}

