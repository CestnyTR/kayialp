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
                        Order = c.Order,
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
        #region  CREATE PRODUCT
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
            product.ImageUrl = await SaveWebpVariantAsync(ordered[0], folder, "products", "cover", 1200, 1200);
            // Güvenlik yaması: metod yanlış klasör döndürürse düzelt
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                product.ImageUrl = product.ImageUrl.Replace("/uploads/categories/", "/uploads/products/");

            // Galeri (kalanlar)
            for (int i = 1; i < ordered.Count && i <= 4; i++)
            {
                var p = await SaveWebpVariantAsync(ordered[i], folder, "products", $"gallery-{i}", 1200, 1200);
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
                Slug = await EnsureUniqueProductSlugAsync(
                       langId: tr.Id,
                       slugCandidate: slugBaseTr,
                       excludeProductId: product.Id,
                       ct: CancellationToken.None),
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
                var slugT = await EnsureUniqueProductSlugAsync(
                                langId: lang.Id,
                                slugCandidate: slugBaseT,
                                excludeProductId: product.Id,
                                ct: CancellationToken.None);
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
        // ===================== CREATE PRODUCT (FULL) =====================
        #endregion

        // using System.Text.RegularExpressions;
        // using Microsoft.AspNetCore.Mvc;
        // using Microsoft.AspNetCore.Mvc.Rendering;
        // using Microsoft.EntityFrameworkCore;
        // using kayialp.ViewModels;
        // using kayialp.Models; // DbContext ve entity'lerin olduğu namespace

        #region Update Product

        [HttpGet("update-product/{id:int}")]
        public async Task<IActionResult> UpdateProduct(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            // Kategoriler: TR adıyla doldur (Create'teki pattern)
            var trId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync();
            var categories = await (from c in _context.Categories
                                    join t in _context.CategoriesTranslations on c.Id equals t.CategoriesId
                                    where t.LangCodeId == trId
                                    orderby c.Order, t.ValueText
                                    select new { c.Id, Name = t.ValueText }).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);

            // Diller
            var langs = await _context.Langs.AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.LangCode })
                .ToListAsync();

            // Ürün çevirileri
            var pts = await _context.ProductsTranslations
                .Where(t => t.ProductId == id)
                .ToListAsync();

            // İçerik blokları
            var content = await _context.ProductContents.FirstOrDefaultAsync(c => c.ProductId == id);
            var blocks = content != null
                ? await _context.ProductContentBlocks.Where(b => b.ProductContentId == content.Id).ToListAsync()
                : new List<ProductContentBlock>();
            var blockIds = blocks.Select(b => b.Id).ToList();

            var bts = blockIds.Any()
                ? await _context.ProductContentBlockTranslations.Where(t => blockIds.Contains(t.BlockId)).ToListAsync()
                : new List<ProductContentBlockTranslation>();

            // Özellikler (tek seferde)
            var group = await _context.ProductAttributeGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.ProductId == product.Id);

            var attrs = new List<ProductAttribute>();
            var attrTrs = new List<ProductAttributeTranslation>();
            if (group != null)
            {
                attrs = await _context.ProductAttributes
                    .Where(a => a.GroupId == group.Id)
                    .OrderBy(a => a.Order)
                    .ToListAsync();

                var attrIds = attrs.Select(a => a.Id).ToList();
                attrTrs = await _context.ProductAttributeTranslations
                    .Where(t => attrIds.Contains(t.AttributeId))
                    .ToListAsync();
            }

            // VM
            var vm = new UpdateProductViewModel
            {
                Id = product.Id,
                CategoryId = product.CategoryId,
                Stock = product.Stock,
                Order = product.Order,
                AutoTranslate = true,
                Langs = new List<UpdateProductLangVM>(),
                AttributeOrder = string.Join(",", attrs.Select(a => a.Id))
            };

            foreach (var l in langs)
            {
                var t = pts.FirstOrDefault(x => x.LangCodeId == l.Id);

                string sh = "", dh = "", ah = "";
                if (blocks.Any())
                {
                    var bShort = blocks.FirstOrDefault(b => b.BlockType == "short_desc");
                    var bDesc = blocks.FirstOrDefault(b => b.BlockType == "description");
                    var bAbout = blocks.FirstOrDefault(b => b.BlockType == "about");
                    if (bShort != null)
                        sh = bts.FirstOrDefault(z => z.BlockId == bShort.Id && z.LangCodeId == l.Id)?.Html ?? "";
                    if (bDesc != null)
                        dh = bts.FirstOrDefault(z => z.BlockId == bDesc.Id && z.LangCodeId == l.Id)?.Html ?? "";
                    if (bAbout != null)
                        ah = bts.FirstOrDefault(z => z.BlockId == bAbout.Id && z.LangCodeId == l.Id)?.Html ?? "";
                }

                var langVm = new UpdateProductLangVM
                {
                    LangCode = l.LangCode,
                    Name = t?.ValueText ?? "",
                    Slug = t?.Slug ?? "",
                    ImageAltsCsv = t?.ImageAlts ?? "",
                    ShortHtml = sh,
                    DescHtml = dh,
                    AboutHtml = ah,
                    Attrs = new List<LangAttributeEditVM>()
                };

                // Bu dildeki özellik çevirileri
                if (attrs.Any())
                {
                    var listForLang = (from a in attrs
                                       join tr in attrTrs on a.Id equals tr.AttributeId into gj
                                       from tr in gj.Where(x => x.LangCodeId == l.Id).DefaultIfEmpty()
                                       orderby a.Order
                                       select new LangAttributeEditVM
                                       {
                                           AttributeId = a.Id,
                                           Order = a.Order,
                                           Name = tr?.Name ?? "",
                                           Value = tr?.Value ?? ""
                                       }).ToList();

                    langVm.Attrs.AddRange(listForLang);

                    // TR paneli için alttaki CRUD listesi
                    if (l.LangCode == "tr")
                    {
                        vm.Attributes = listForLang
                            .Select(x => new UpdateProductAttributeRow
                            {
                                Id = x.AttributeId,
                                Order = x.Order,
                                NameTr = x.Name,
                                ValueTr = x.Value
                            })
                            .ToList();

                        vm.NameTr = langVm.Name ?? "";
                        vm.ShortDescriptionTr = langVm.ShortHtml ?? "";
                        vm.DescriptionTr = langVm.DescHtml ?? "";
                        vm.AboutTr = langVm.AboutHtml ?? "";
                        vm.ImageAltsTr = langVm.ImageAltsCsv ?? "";
                    }
                }

                vm.Langs.Add(langVm);
            }

            // Mevcut görseller
            var folder = Path.Combine(_env.WebRootPath, "uploads", "products", product.Id.ToString());
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder, "*.webp").Select(Path.GetFileName).ToList();
                var coverFile = Path.GetFileName(product.ImageUrl ?? "");
                if (!string.IsNullOrWhiteSpace(coverFile) && files.Contains(coverFile))
                    files = files.Where(f => f != coverFile).Prepend(coverFile).ToList();
                ViewBag.ExistingImages = files;
            }
            else
            {
                ViewBag.ExistingImages = new List<string>();
            }

            return View("UpdateProduct", vm);
        }

        [HttpPost("update-product/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(int id, UpdateProductViewModel model, CancellationToken ct)
        {
            if (id != model.Id) return BadRequest();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var trLang = langs.FirstOrDefault(x => x.LangCode == "tr");
            if (trLang == null) throw new InvalidOperationException("TR language not found.");

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1) Temel
                product.CategoryId = model.CategoryId;
                product.Stock = model.Stock;
                product.Order = model.Order;
                await _context.SaveChangesAsync(ct);

                // 2) Görseller
                var folder = Path.Combine(_env.WebRootPath, "uploads", "products", product.Id.ToString());
                Directory.CreateDirectory(folder);

                if (model.RemoveImages != null)
                {
                    foreach (var name in model.RemoveImages.Where(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        var path = Path.Combine(folder, name);
                        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    }
                }
                if (model.NewImages != null && model.NewImages.Any())
                {
                    int existing = Directory.GetFiles(folder, "*.webp").Length;
                    foreach (var f in model.NewImages)
                    {
                        if (existing >= 5) break;
                        var safe = MakeSafeFileBaseName(Path.GetFileNameWithoutExtension(f.FileName));
                        var outName = $"{safe}-{Guid.NewGuid():N}";
                        await SaveWebpVariantAsync(f, folder, "products", outName, 1200, 1200);
                        existing++;
                    }
                }

                var nowFiles = Directory.GetFiles(folder, "*.webp").Select(Path.GetFileName)!.ToList();
                if (!nowFiles.Any()) throw new InvalidOperationException("En az 1 görsel olmalı.");

                List<string> ordered;
                if (!string.IsNullOrWhiteSpace(model.ImageOrder))
                {
                    var pref = model.ImageOrder.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                    ordered = pref.Where(p => nowFiles.Contains(p)).ToList();
                    ordered.AddRange(nowFiles.Where(f => !ordered.Contains(f)));
                }
                else ordered = nowFiles;

                if (model.CoverIndex.HasValue && model.CoverIndex >= 0 && model.CoverIndex < ordered.Count)
                {
                    var cover = ordered[model.CoverIndex.Value];
                    ordered = ordered.Where(x => x != cover).Prepend(cover).ToList();
                }
                product.ImageUrl = $"/uploads/products/{product.Id}/{ordered[0]}";
                await _context.SaveChangesAsync(ct);

                // 3) İçerik blokları
                var content = await _context.ProductContents.FirstOrDefaultAsync(c => c.ProductId == product.Id, ct)
                              ?? new ProductContent { ProductId = product.Id, Order = 0, IsActive = true };
                if (content.Id == 0) { _context.ProductContents.Add(content); await _context.SaveChangesAsync(ct); }

                var blocks = await _context.ProductContentBlocks.Where(b => b.ProductContentId == content.Id).ToListAsync(ct);
                ProductContentBlock EnsureBlock(string type, int order)
                {
                    var b = blocks.FirstOrDefault(x => x.BlockType == type);
                    if (b == null)
                    {
                        b = new ProductContentBlock { ProductContentId = content.Id, BlockType = type, Order = order, IsActive = true };
                        _context.ProductContentBlocks.Add(b);
                        _context.SaveChanges();
                        blocks.Add(b);
                    }
                    return b;
                }
                var bShort = EnsureBlock("short_desc", 0);
                var bDesc = EnsureBlock("description", 1);
                var bAbout = EnsureBlock("about", 2);

                // 4) Dil sekmeleri
                var postedByLang = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                if (!postedByLang.TryGetValue("tr", out var trVm)) trVm = new UpdateProductLangVM { LangCode = "tr" };

                // TR — ProductsTranslations + bloklar
                try
                {
                    await UpsertProductTranslationAsync(product.Id, trLang.Id,
                        name: trVm.Name ?? "",
                        slugInput: trVm.Slug, // kullanıcı girişi; boşsa içeride üretilecek
                        imageAltsCsv: NormalizeOrBuildAlts(trVm.ImageAltsCsv, trVm.Name ?? "", ordered.Count),
                        ct: ct);
                }
                catch (ValidationException vex)
                {
                    ModelState.AddModelError("", vex.Message);
                    return View("UpdateProduct", model); // viewbag dolduruyorsan burada tazele
                }


                await UpsertBlockHtmlAsync(bShort.Id, trLang.Id, trVm.ShortHtml ?? "", ct);
                await UpsertBlockHtmlAsync(bDesc.Id, trLang.Id, trVm.DescHtml ?? "", ct);
                await UpsertBlockHtmlAsync(bAbout.Id, trLang.Id, trVm.AboutHtml ?? "", ct);

                // Diğer diller
                foreach (var lang in langs.Where(l => l.Id != trLang.Id))
                {
                    postedByLang.TryGetValue(lang.LangCode, out var vmLang);

                    string pick(string? targetVal, string trVal) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(targetVal))
                            ? (targetVal ?? "")
                            : TryTranslate(trVal, "tr", lang.LangCode);

                    var nameT = pick(vmLang?.Name, trVm.Name ?? "");
                    var shortT = pick(vmLang?.ShortHtml, trVm.ShortHtml ?? "");
                    var descT = pick(vmLang?.DescHtml, trVm.DescHtml ?? "");
                    var aboutT = pick(vmLang?.AboutHtml, trVm.AboutHtml ?? "");

                    var altsT = SplitCsv(vmLang?.ImageAltsCsv);
                    if (altsT.Count == 0)
                    {
                        var trAlts = SplitCsv(trVm.ImageAltsCsv);
                        if (trAlts.Count == 0) trAlts = BuildAutoImageAltsList(ordered.Count, trVm.Name ?? "");
                        altsT = model.AutoTranslate
                            ? trAlts.Select(a => TryTranslate(a, "tr", lang.LangCode)).ToList()
                            : trAlts.ToList();
                    }
                    altsT = BalanceAltCount(altsT, ordered.Count, nameT);
                    var altsCsvT = string.Join(", ", altsT);

                    var baseSlug = !string.IsNullOrWhiteSpace(vmLang?.Slug)
                        ? vmLang!.Slug!
                        : ToLocalizedSlugSafe(string.IsNullOrWhiteSpace(nameT) ? trVm.Name ?? "" : nameT);

                    try
                    {
                        await UpsertProductTranslationAsync(product.Id, lang.Id,
                            name: nameT, slugInput: vmLang?.Slug, imageAltsCsv: altsCsvT, ct: ct);
                    }
                    catch (ValidationException vex)
                    {
                        ModelState.AddModelError("", $"[{lang.LangCode.ToUpper()}] {vex.Message}");
                        return View("UpdateProduct", model);
                    }

                    await UpsertBlockHtmlAsync(bShort.Id, lang.Id, shortT, ct);
                    await UpsertBlockHtmlAsync(bDesc.Id, lang.Id, descT, ct);
                    await UpsertBlockHtmlAsync(bAbout.Id, lang.Id, aboutT, ct);
                }

                // 5) TR özellik CRUD (ekle/güncelle/sil)
                var group = await _context.ProductAttributeGroups.FirstOrDefaultAsync(g => g.ProductId == product.Id, ct)
                            ?? new ProductAttributeGroup { ProductId = product.Id, Order = 0, IsActive = true };
                if (group.Id == 0) { _context.ProductAttributeGroups.Add(group); await _context.SaveChangesAsync(ct); }

                if (model.Attributes != null)
                {
                    foreach (var row in model.Attributes)
                    {
                        // sil
                        if (row.Id.HasValue && row.Delete == true)
                        {
                            var delTr = _context.ProductAttributeTranslations.Where(x => x.AttributeId == row.Id.Value);
                            _context.ProductAttributeTranslations.RemoveRange(delTr);
                            var del = await _context.ProductAttributes.FirstOrDefaultAsync(x => x.Id == row.Id.Value, ct);
                            if (del != null) _context.ProductAttributes.Remove(del);
                            continue;
                        }

                        if (!row.Id.HasValue)
                        {
                            var key = TextCaseHelper.ToCamelCaseKey(row.NameTr ?? $"attr-{Guid.NewGuid():N}");
                            var attr = new ProductAttribute { GroupId = group.Id, KeyName = key, Order = row.Order };
                            _context.ProductAttributes.Add(attr);
                            await _context.SaveChangesAsync(ct);

                            _context.ProductAttributeTranslations.Add(new ProductAttributeTranslation
                            {
                                AttributeId = attr.Id,
                                LangCodeId = trLang.Id,
                                Name = row.NameTr ?? "",
                                Value = row.ValueTr ?? ""
                            });

                            foreach (var lang in langs.Where(l => l.Id != trLang.Id))
                            {
                                var nameT = model.AutoTranslate ? TryTranslate(row.NameTr ?? "", "tr", lang.LangCode) : (row.NameTr ?? "");
                                var valueT = model.AutoTranslate ? TryTranslate(row.ValueTr ?? "", "tr", lang.LangCode) : (row.ValueTr ?? "");
                                _context.ProductAttributeTranslations.Add(new ProductAttributeTranslation
                                {
                                    AttributeId = attr.Id,
                                    LangCodeId = lang.Id,
                                    Name = nameT,
                                    Value = valueT
                                });
                            }
                        }
                        else
                        {
                            var attr = await _context.ProductAttributes.FirstOrDefaultAsync(a => a.Id == row.Id.Value, ct);
                            if (attr == null) continue;
                            attr.Order = row.Order;

                            var trAttr = await _context.ProductAttributeTranslations
                                .FirstOrDefaultAsync(x => x.AttributeId == attr.Id && x.LangCodeId == trLang.Id, ct);
                            if (trAttr == null)
                            {
                                _context.ProductAttributeTranslations.Add(new ProductAttributeTranslation
                                {
                                    AttributeId = attr.Id,
                                    LangCodeId = trLang.Id,
                                    Name = row.NameTr ?? "",
                                    Value = row.ValueTr ?? ""
                                });
                            }
                            else
                            {
                                trAttr.Name = row.NameTr ?? "";
                                trAttr.Value = row.ValueTr ?? "";
                            }
                        }
                    }
                    await _context.SaveChangesAsync(ct);
                }

                // 6) Dil sekmelerindeki ad/değerleri kaydet (tüm diller)
                var attrGroup = await _context.ProductAttributeGroups.FirstOrDefaultAsync(g => g.ProductId == product.Id, ct);
                if (attrGroup != null)
                {
                    var currentAttrIds = await _context.ProductAttributes
                        .Where(a => a.GroupId == attrGroup.Id)
                        .Select(a => a.Id)
                        .ToListAsync(ct);

                    foreach (var lang in langs)
                    {
                        if (!postedByLang.TryGetValue(lang.LangCode, out var langVm) || langVm.Attrs == null) continue;

                        foreach (var row in langVm.Attrs)
                        {
                            if (!currentAttrIds.Contains(row.AttributeId)) continue; // TR'de yeni silinmiş olabilir

                            var t = await _context.ProductAttributeTranslations
                                .FirstOrDefaultAsync(x => x.AttributeId == row.AttributeId && x.LangCodeId == lang.Id, ct);

                            if (t == null)
                            {
                                _context.ProductAttributeTranslations.Add(new ProductAttributeTranslation
                                {
                                    AttributeId = row.AttributeId,
                                    LangCodeId = lang.Id,
                                    Name = row.Name ?? "",
                                    Value = row.Value ?? ""
                                });
                            }
                            else
                            {
                                t.Name = row.Name ?? "";
                                t.Value = row.Value ?? "";
                            }
                        }
                    }
                    await _context.SaveChangesAsync(ct);
                }

                // 7) Özellik global sırası (CSV -> ProductAttributes.Order)
                if (!string.IsNullOrWhiteSpace(model.AttributeOrder))
                {
                    var ids = model.AttributeOrder
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var n) ? n : -1)
                        .Where(n => n > 0)
                        .ToList();

                    for (int idx = 0; idx < ids.Count; idx++)
                    {
                        var attr = await _context.ProductAttributes.FirstOrDefaultAsync(a => a.Id == ids[idx], ct);
                        if (attr != null) attr.Order = idx;
                    }
                    await _context.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);
                TempData["ProductMsg"] = "Ürün güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Güncelleme hatası: {ex.Message}");
                return View("UpdateProduct", model);
            }
        }
        #endregion

        #region Delete Product
        // --------------- POST /admin/delete-product/{id} (sert sil) ---------------

        [HttpPost("delete-product/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product == null) return NotFound(new { success = false, error = "Ürün bulunamadı." });

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // İlişkiler
                var translations = _context.ProductsTranslations.Where(t => t.ProductId == id);

                var content = await _context.ProductContents.FirstOrDefaultAsync(c => c.ProductId == id, ct);
                List<ProductContentBlock> blocks = new();
                if (content != null)
                {
                    blocks = await _context.ProductContentBlocks.Where(b => b.ProductContentId == content.Id).ToListAsync(ct);
                    var blockIds = blocks.Select(b => b.Id).ToList();
                    var blockTrs = _context.ProductContentBlockTranslations.Where(t => blockIds.Contains(t.BlockId));
                    _context.ProductContentBlockTranslations.RemoveRange(blockTrs);
                    _context.ProductContentBlocks.RemoveRange(blocks);
                    _context.ProductContents.Remove(content);
                }

                var attrGroup = await _context.ProductAttributeGroups.FirstOrDefaultAsync(g => g.ProductId == id, ct);
                if (attrGroup != null)
                {
                    var attrs = await _context.ProductAttributes.Where(a => a.GroupId == attrGroup.Id).ToListAsync(ct);
                    var attrIds = attrs.Select(a => a.Id).ToList();
                    var attrTrs = _context.ProductAttributeTranslations.Where(t => attrIds.Contains(t.AttributeId));
                    _context.ProductAttributeTranslations.RemoveRange(attrTrs);
                    _context.ProductAttributes.RemoveRange(attrs);
                    _context.ProductAttributeGroups.Remove(attrGroup);
                }

                _context.ProductsTranslations.RemoveRange(translations);
                _context.Products.Remove(product);

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Dosyalar: DB commit SONRASI temizle
                try
                {
                    var folder = Path.Combine(_env.WebRootPath, "uploads", "products", id.ToString());
                    if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                }
                catch
                {
                    // klasör silinemezse sessiz geç; istersen logla
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(500, new { success = false, error = $"Silme hatası: {ex.Message}" });
            }
        }
        #endregion

        #region reorder Product
        [HttpPost("reorder-products")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderProducts([FromForm] string[]? order, [FromForm] string? ids, CancellationToken ct)
        {
            var idsList = NormalizeOrderPayload(order, ids);
            if (idsList.Count == 0)
                return BadRequest(new { success = false, error = "Geçersiz veya boş sıralama listesi." });

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // Sadece gelen id'leri sırala; olmayanları yok say
                for (int i = 0; i < idsList.Count; i++)
                {
                    var id = idsList[i];
                    var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
                    if (p != null) p.Order = i;
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(500, new { success = false, error = $"Sıralama hatası: {ex.Message}" });
            }
        }
        #endregion

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
                        model.CardImage, folder, "categories", "card-312x240", 312, 240);
                }

                if (model.ShowcaseImage != null && model.ShowcaseImage.Length > 0)
                {
                    category.ImageShowcase423x636 = await SaveWebpVariantAsync(
                        model.ShowcaseImage, folder, "categories", "showcase-423x636", 423, 636);
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
                    model.NewCardImage, folder, "blog", "card-312x240", 312, 240);
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
                    model.NewShowcaseImage, folder, "blog", "showcase-423x636", 423, 636);
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
        [HttpPost("reorder-categories")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderCategories([FromForm] string[]? order, [FromForm] string? ids, CancellationToken ct)
        {
            var idsList = NormalizeOrderPayload(order, ids);
            if (idsList.Count == 0)
                return BadRequest(new { success = false, error = "Geçersiz veya boş sıralama listesi." });

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                for (int i = 0; i < idsList.Count; i++)
                {
                    var id = idsList[i];
                    var c = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
                    if (c != null) c.Order = i;
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(500, new { success = false, error = $"Sıralama hatası: {ex.Message}" });
            }
        }
        #endregion

        #region Blog sections
        #region Blog CRUD 
        // === BLOG LISTE ===
        [HttpGet("blog")]
        public async Task<IActionResult> BlogIndex()
        {
            var trId = await _context.Langs.AsNoTracking()
                .Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync();

            var categories = await (from c in _context.BlogCategories.AsNoTracking().OrderBy(c => c.Order)
                                    join t in _context.BlogCategoriesTranslations.AsNoTracking().Where(x => x.LangCodeId == trId)
                                      on c.Id equals t.BlogCategoryId
                                    select new AdminBlogCategoryVM { Id = c.Id, Name = t.ValueText })
                                    .ToListAsync();

            var posts = await (from p in _context.BlogPosts.AsNoTracking().OrderBy(p => p.Order).ThenByDescending(p => p.Id)
                               join t in _context.BlogPostsTranslations.AsNoTracking().Where(x => x.LangCodeId == trId)
                                 on p.Id equals t.PostId
                               join ct in _context.BlogCategoriesTranslations.AsNoTracking().Where(x => x.LangCodeId == trId)
                                 on p.CategoryId equals ct.BlogCategoryId into catj
                               from cat in catj.DefaultIfEmpty()
                               select new AdminBlogListItemVM
                               {
                                   Id = p.Id,
                                   Title = t.ValueTitle,
                                   CategoryName = cat != null ? cat.ValueText : "-",
                                   Order = p.Order,
                                   IsActive = p.IsActive,
                                   Slug = t.Slug,
                                   CoverUrl = p.Cover312x240
                               }).ToListAsync();

            var vm = new AdminBlogIndexVM { Categories = categories, Posts = posts };
            return View("BlogIndex", vm);
        }

        // /admin/create-post (GET)
        [HttpGet("/admin/create-post")]
        public async Task<IActionResult> CreatePost(CancellationToken ct)
        {
            var trId = await _context.Langs.Where(l => l.LangCode == "tr")
                .Select(l => l.Id).FirstAsync(ct);

            var catList = await (from c in _context.BlogCategories
                                 join t in _context.BlogCategoriesTranslations on c.Id equals t.BlogCategoryId
                                 where t.LangCodeId == trId
                                 orderby c.Order, t.ValueText
                                 select new { c.Id, Name = t.ValueText })
                                 .ToListAsync(ct);

            ViewBag.BlogCategories = new SelectList(catList, "Id", "Name");
            return View(new CreatePostVM());
        }


        // /admin/create-post (POST)
        [HttpPost("/admin/create-post")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(CreatePostVM model, CancellationToken ct)
        {
            // Görseller zorunlu
            if (model.CoverImage == null) ModelState.AddModelError(nameof(model.CoverImage), "Kapak görseli zorunlu (312×240).");
            if (model.InnerImage == null) ModelState.AddModelError(nameof(model.InnerImage), "İç görsel zorunlu (856×460).");

            var trId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync(ct);
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);

            if (!ModelState.IsValid)
            {
                var catList = await (from c in _context.BlogCategories
                                     join t in _context.BlogCategoriesTranslations on c.Id equals t.BlogCategoryId
                                     where t.LangCodeId == trId
                                     orderby c.Order, t.ValueText
                                     select new { c.Id, Name = t.ValueText })
                                     .ToListAsync(ct);
                ViewBag.BlogCategories = new SelectList(catList, "Id", "Name", model.CategoryId);
                return View("CreatePost", model);
            }

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1) Ana kayıt
                var post = new BlogPosts
                {
                    CategoryId = model.CategoryId,
                    Order = model.Order,
                    IsActive = model.IsActive
                };
                _context.BlogPosts.Add(post);
                await _context.SaveChangesAsync(ct);

                // 2) Görseller
                var folder = Path.Combine(_env.WebRootPath, "uploads", "blog", post.Id.ToString());
                Directory.CreateDirectory(folder);

                post.Cover312x240 = await SaveWebpVariantAsync(model.CoverImage!, folder, "blog", "cover-312x240", 312, 240);
                post.Inner856x460 = await SaveWebpVariantAsync(model.InnerImage!, folder, "blog", "inner-856x460", 856, 460);



                await _context.SaveChangesAsync(ct);

                // 3) Content + bloklar
                var content = new BlogPostContent { PostId = post.Id, Order = 0, IsActive = true };
                _context.BlogPostContents.Add(content);
                await _context.SaveChangesAsync(ct);

                var bSummary = new BlogPostContentBlock { PostContentId = content.Id, BlockType = "summary", Order = 0, IsActive = true };
                var bBody = new BlogPostContentBlock { PostContentId = content.Id, BlockType = "body", Order = 1, IsActive = true };
                _context.BlogPostContentBlocks.AddRange(bSummary, bBody);
                await _context.SaveChangesAsync(ct);

                // 4) Dil bazlı kayıt
                var postedByLang = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedByLang.TryGetValue("tr", out var trVm);

                var trTitle = trVm?.Title ?? model.TitleTr ?? "";
                var trSum = trVm?.SummaryHtml ?? model.SummaryTr ?? "";
                var trBodyHtml = trVm?.BodyHtml ?? model.BodyTr ?? "";
                var trAltCover = trVm?.ImageAltCover ?? model.AltCoverTr ?? trTitle;
                var trAltInner = trVm?.ImageAltInner ?? model.AltInnerTr ?? trTitle;
                var KeyValue = await BuildCanonicalKeyNameAsync(trTitle, post.Id, ct);

                await UpsertPostTranslationAsync(post.Id, trId, KeyValue, trTitle, trVm?.Slug, trAltCover, trAltInner, ct);
                await UpsertPostBlockHtmlAsync(bSummary.Id, trId, trSum, ct);
                await UpsertPostBlockHtmlAsync(bBody.Id, trId, trBodyHtml, ct);

                // diğer diller
                foreach (var l in langs.Where(x => x.Id != trId))
                {
                    postedByLang.TryGetValue(l.LangCode, out var vm);

                    string pick(string? v, string trv, string langCode) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v))
                            ? (v ?? "")
                            : TryTranslate(trv, "tr", langCode);

                    var titleT = pick(vm?.Title, trTitle, l.LangCode);
                    var sumT = pick(vm?.SummaryHtml, trSum, l.LangCode);
                    var bodyT = pick(vm?.BodyHtml, trBodyHtml, l.LangCode);
                    var altCoverT = pick(vm?.ImageAltCover, trAltCover, l.LangCode);
                    var altInnerT = pick(vm?.ImageAltInner, trAltInner, l.LangCode);

                    await UpsertPostTranslationAsync(post.Id, l.Id, KeyValue, titleT, vm?.Slug, altCoverT, altInnerT, ct);
                    await UpsertPostBlockHtmlAsync(bSummary.Id, l.Id, sumT, ct);
                    await UpsertPostBlockHtmlAsync(bBody.Id, l.Id, bodyT, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["BlogMsg"] = "Blog yazısı başarıyla eklendi.";
                return RedirectToAction(nameof(BlogIndex));
            }
            catch (ValidationException vex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", vex.Message);
                return await RefillCreatePostView(model, trId, ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Oluşturma hatası: {ex.Message}");
                return await RefillCreatePostView(model, trId, ct);
            }
        }

        private async Task<IActionResult> RefillCreatePostView(CreatePostVM model, int trId, CancellationToken ct)
        {
            var catList = await (from c in _context.BlogCategories
                                 join t in _context.BlogCategoriesTranslations on c.Id equals t.BlogCategoryId
                                 where t.LangCodeId == trId
                                 orderby c.Order, t.ValueText
                                 select new { c.Id, Name = t.ValueText }).ToListAsync(ct);
            ViewBag.BlogCategories = new SelectList(catList, "Id", "Name", model.CategoryId);
            return View("CreatePost", model);
        }

        // === BLOG UPDATE GET ===
        [HttpGet("update-post/{id:int}")]
        public async Task<IActionResult> UpdatePost(int id)
        {
            var post = await _context.BlogPosts.FirstOrDefaultAsync(x => x.Id == id);
            if (post == null) return NotFound();

            var trId = await _context.Langs.Where(l => l.LangCode == "tr")
                                           .Select(l => l.Id).FirstAsync();
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync();

            var catList = await (from c in _context.BlogCategories
                                 join t in _context.BlogCategoriesTranslations on c.Id equals t.BlogCategoryId
                                 where t.LangCodeId == trId
                                 orderby c.Order, t.ValueText
                                 select new { c.Id, Name = t.ValueText }).ToListAsync();
            ViewBag.BlogCategories = new SelectList(catList, "Id", "Name", post.CategoryId);

            var pts = await _context.BlogPostsTranslations
                                    .Where(t => t.PostId == id).ToListAsync();

            var content = await _context.BlogPostContents
                                        .FirstOrDefaultAsync(c => c.PostId == id);

            var blocks = content != null
                ? await _context.BlogPostContentBlocks
                                .Where(b => b.PostContentId == content.Id).ToListAsync()
                : new List<BlogPostContentBlock>();

            var blockIds = blocks.Select(b => b.Id).ToList();

            var bts = blockIds.Any()
                ? await _context.BlogPostContentBlockTranslations
                                 .Where(t => blockIds.Contains(t.BlockId)).ToListAsync()
                : new List<BlogPostContentBlockTranslation>();

            var vm = new UpdatePostVM
            {
                Id = post.Id,
                CategoryId = post.CategoryId,
                Order = post.Order,
                IsActive = post.IsActive,
                ExistingCover = post.Cover312x240,
                ExistingInner = post.Inner856x460,
                AutoTranslate = true,
                Langs = new List<BlogLangVM>()
            };

            // Bloğu türüne göre bul
            var bSummary = blocks.FirstOrDefault(b => b.BlockType == "summary");
            var bBody = blocks.FirstOrDefault(b => b.BlockType == "body");

            foreach (var l in langs)
            {
                var t = pts.FirstOrDefault(x => x.LangCodeId == l.Id);

                string sum = "", body = "";
                if (bSummary != null)
                    sum = bts.FirstOrDefault(z => z.BlockId == bSummary.Id && z.LangCodeId == l.Id)?.Html ?? "";
                if (bBody != null)
                    body = bts.FirstOrDefault(z => z.BlockId == bBody.Id && z.LangCodeId == l.Id)?.Html ?? "";

                var langVm = new BlogLangVM
                {
                    LangCode = l.LangCode,
                    Title = t?.ValueTitle ?? "",
                    Slug = t?.Slug ?? "",
                    ImageAltCover = t?.ImageAltCover ?? "",
                    ImageAltInner = t?.ImageAltInner ?? "",
                    SummaryHtml = sum,
                    BodyHtml = body
                };
                vm.Langs.Add(langVm);

                if (l.LangCode == "tr")
                {
                    vm.TitleTr = langVm.Title;
                    vm.SummaryTr = langVm.SummaryHtml;
                    vm.BodyTr = langVm.BodyHtml;
                    vm.AltCoverTr = langVm.ImageAltCover;
                    vm.AltInnerTr = langVm.ImageAltInner;
                }
            }

            return View("UpdatePost", vm);
        }
        // === BLOG UPDATE POST ===
        [HttpPost("update-post/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePost(int id, UpdatePostVM model, CancellationToken ct)
        {
            if (id != model.Id) return BadRequest();

            var post = await _context.BlogPosts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (post == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var trLang = langs.FirstOrDefault(x => x.LangCode == "tr");
            if (trLang == null) throw new InvalidOperationException("TR language not found.");

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1) temel alanlar
                post.CategoryId = model.CategoryId;
                post.Order = model.Order;
                post.IsActive = model.IsActive;
                await _context.SaveChangesAsync(ct);

                // 2) görseller (kapak 312x240, iç 856x460)
                var folder = Path.Combine(_env.WebRootPath, "uploads", "blog", post.Id.ToString());
                Directory.CreateDirectory(folder);

                // kapak: sil?
                if (model.RemoveCover && !string.IsNullOrWhiteSpace(post.Cover312x240))
                {
                    var path = Path.Combine(_env.WebRootPath, post.Cover312x240.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    post.Cover312x240 = null;
                }
                // iç: sil?
                if (model.RemoveInner && !string.IsNullOrWhiteSpace(post.Inner856x460))
                {
                    var path = Path.Combine(_env.WebRootPath, post.Inner856x460.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    post.Inner856x460 = null;
                }

                // yeni kapak?
                if (model.CoverImage != null)
                    post.Cover312x240 = await SaveWebpVariantAsync(model.CoverImage, folder, "blog", "cover-312x240", 312, 240);

                // yeni iç?
                if (model.InnerImage != null)
                    post.Inner856x460 = await SaveWebpVariantAsync(model.InnerImage, folder, "blog", "inner-856x460", 856, 460);

                await _context.SaveChangesAsync(ct);

                // 3) içerik & bloklar (summary/body) — varsa getir yoksa oluştur
                var content = await _context.BlogPostContents.FirstOrDefaultAsync(c => c.PostId == post.Id, ct)
                              ?? new BlogPostContent { PostId = post.Id, Order = 0, IsActive = true };
                if (content.Id == 0) { _context.BlogPostContents.Add(content); await _context.SaveChangesAsync(ct); }

                var blocks = await _context.BlogPostContentBlocks
                                           .Where(b => b.PostContentId == content.Id).ToListAsync(ct);

                BlogPostContentBlock EnsureBlock(string type, int order)
                {
                    var b = blocks.FirstOrDefault(x => x.BlockType == type);
                    if (b == null)
                    {
                        b = new BlogPostContentBlock { PostContentId = content.Id, BlockType = type, Order = order, IsActive = true };
                        _context.BlogPostContentBlocks.Add(b);
                        _context.SaveChanges(); // id almak için küçük sync save
                        blocks.Add(b);
                    }
                    return b;
                }

                var bSummary = EnsureBlock("summary", 0);
                var bBody = EnsureBlock("body", 1);

                // 4) sekmeler / AutoTranslate: yalnız boş alanları TR’den doldur
                var postedByLang = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedByLang.TryGetValue("tr", out var trVm);

                var trTitle = trVm?.Title ?? model.TitleTr ?? "";
                var trSum = trVm?.SummaryHtml ?? model.SummaryTr ?? "";
                var trBodyHtml = trVm?.BodyHtml ?? model.BodyTr ?? "";
                var trAltCover = trVm?.ImageAltCover ?? model.AltCoverTr ?? trTitle;
                var trAltInner = trVm?.ImageAltInner ?? model.AltInnerTr ?? trTitle;
                var KeyValue = await BuildCanonicalKeyNameAsync(trTitle, post.Id, ct);

                // TR upsert
                await UpsertPostTranslationAsync(post.Id, trLang.Id, KeyValue, trTitle, trVm?.Slug, trAltCover, trAltInner, ct);
                await UpsertPostBlockHtmlAsync(bSummary.Id, trLang.Id, trSum, ct);
                await UpsertPostBlockHtmlAsync(bBody.Id, trLang.Id, trBodyHtml, ct);

                // diğer diller
                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    postedByLang.TryGetValue(l.LangCode, out var vm);

                    string pick(string? v, string trv) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);

                    var titleT = pick(vm?.Title, trTitle);
                    var sumT = pick(vm?.SummaryHtml, trSum);
                    var bodyT = pick(vm?.BodyHtml, trBodyHtml);
                    var altCoverT = pick(vm?.ImageAltCover, trAltCover);
                    var altInnerT = pick(vm?.ImageAltInner, trAltInner);

                    await UpsertPostTranslationAsync(post.Id, l.Id, KeyValue, titleT, vm?.Slug, altCoverT, altInnerT, ct);
                    await UpsertPostBlockHtmlAsync(bSummary.Id, l.Id, sumT, ct);
                    await UpsertPostBlockHtmlAsync(bBody.Id, l.Id, bodyT, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["BlogMsg"] = "Blog yazısı güncellendi.";
                return RedirectToAction(nameof(BlogIndex));
            }
            catch (ValidationException vex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", vex.Message);

                // dropdown’u tekrar doldur
                var trId = await _context.Langs.Where(l => l.LangCode == "tr")
                                               .Select(l => l.Id).FirstAsync(ct);
                var catList = await (from c in _context.BlogCategories
                                     join t in _context.BlogCategoriesTranslations on c.Id equals t.BlogCategoryId
                                     where t.LangCodeId == trId
                                     orderby c.Order, t.ValueText
                                     select new { c.Id, Name = t.ValueText }).ToListAsync(ct);
                ViewBag.BlogCategories = new SelectList(catList, "Id", "Name", model.CategoryId);

                return View("UpdatePost", model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Güncelleme hatası: {ex.Message}");

                var trId = await _context.Langs.Where(l => l.LangCode == "tr")
                                               .Select(l => l.Id).FirstAsync(ct);
                var catList = await (from c in _context.BlogCategories
                                     join t in _context.BlogCategoriesTranslations on c.Id equals t.BlogCategoryId
                                     where t.LangCodeId == trId
                                     orderby c.Order, t.ValueText
                                     select new { c.Id, Name = t.ValueText }).ToListAsync(ct);
                ViewBag.BlogCategories = new SelectList(catList, "Id", "Name", model.CategoryId);

                return View("UpdatePost", model);
            }
        }
        // === POST /admin/delete-post/{id} (sert sil) ===
        [HttpPost("delete-post/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id, CancellationToken ct)
        {
            var post = await _context.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (post == null) return NotFound(new { success = false, error = "Kayıt bulunamadı." });

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var tr = _context.BlogPostsTranslations.Where(t => t.PostId == id);

                var content = await _context.BlogPostContents.FirstOrDefaultAsync(c => c.PostId == id, ct);
                if (content != null)
                {
                    var blocks = await _context.BlogPostContentBlocks.Where(b => b.PostContentId == content.Id).ToListAsync(ct);
                    var blockIds = blocks.Select(b => b.Id).ToList();
                    var blockTrs = _context.BlogPostContentBlockTranslations.Where(t => blockIds.Contains(t.BlockId));
                    _context.BlogPostContentBlockTranslations.RemoveRange(blockTrs);
                    _context.BlogPostContentBlocks.RemoveRange(blocks);
                    _context.BlogPostContents.Remove(content);
                }

                _context.BlogPostsTranslations.RemoveRange(tr);
                _context.BlogPosts.Remove(post);

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // dosyalar
                try
                {
                    var folder = Path.Combine(_env.WebRootPath, "uploads", "blog", id.ToString());
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                }
                catch { /* log optional */ }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(500, new { success = false, error = $"Silme hatası: {ex.Message}" });
            }
        }
        #endregion
        #region  order
        // === POST /admin/reorder-posts ===
        [HttpPost("reorder-posts")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderPosts([FromForm] string[]? order, [FromForm] string? ids, CancellationToken ct)
        {
            List<int> Normalize(string[]? arr, string? csv)
            {
                var list = new List<int>();
                if (arr != null && arr.Length > 0)
                {
                    foreach (var s in arr)
                        if (int.TryParse(s, out var n) && n > 0) list.Add(n);
                    return list;
                }
                if (!string.IsNullOrWhiteSpace(csv))
                {
                    foreach (var s in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        if (int.TryParse(s.Trim(), out var n) && n > 0) list.Add(n);
                }
                return list;
            }

            var idsList = Normalize(order, ids);
            if (idsList.Count == 0)
                return BadRequest(new { success = false, error = "Geçersiz/boş sıralama listesi." });

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                for (int i = 0; i < idsList.Count; i++)
                {
                    var id = idsList[i];
                    var p = await _context.BlogPosts.FirstOrDefaultAsync(x => x.Id == id, ct);
                    if (p != null) p.Order = i;
                }
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(500, new { success = false, error = $"Sıralama hatası: {ex.Message}" });
            }
        }
        #endregion

        #region  Blog Category
        // AdminController.cs (ilgili using'ler: System.ComponentModel.DataAnnotations, Microsoft.EntityFrameworkCore vs.)

        [HttpGet("create-blog-category")]
        public async Task<IActionResult> CreateBlogCategory(CancellationToken ct)
        {
            // Diller (küçük harf)
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);

            var vm = new CreateBlogCategoryVM
            {
                Order = 0,
                IsActive = true,
                AutoTranslate = true,
                Langs = langs.Select(l => new BlogCategoryLangVM { LangCode = l.LangCode }).ToList()
            };
            return View("CreateBlogCategory", vm);
        }

        [HttpPost("create-blog-category")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlogCategory(CreateBlogCategoryVM model, CancellationToken ct)
        {
            // TR dili gerekli
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var tr = langs.FirstOrDefault(l => l.LangCode == "tr");
            if (tr == null) throw new InvalidOperationException("TR language not found.");

            if (!ModelState.IsValid)
                return View("CreateBlogCategory", model);

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1) Ana kayıt
                var cat = new BlogCategories { Order = model.Order, IsActive = model.IsActive };
                _context.BlogCategories.Add(cat);
                await _context.SaveChangesAsync(ct); // Id lazım

                // 2) TR kaynak (sekmeden varsa onu, yoksa NameTr)
                var postedByLang = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedByLang.TryGetValue("tr", out var trVm);
                var trName = trVm?.Name ?? model.NameTr ?? "";

                // 3) EN’den KeyName (tek ve sabit)
                var keyName = await BuildCanonicalKeyNameForCategoryAsync(trName, cat.Id, ct);

                // 4) TR çeviri
                await UpsertBlogCategoryTranslationAsync(cat.Id, tr.Id,
                    keyName: keyName,
                    name: trName,
                    slugOrTitle: trVm?.Slug, // boşsa name’den türetilir
                    ct: ct);

                // 5) Diğer diller
                foreach (var l in langs.Where(x => x.Id != tr.Id))
                {
                    postedByLang.TryGetValue(l.LangCode, out var vm);

                    string pick(string? v, string trv) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);

                    var nameT = pick(vm?.Name, trName);
                    await UpsertBlogCategoryTranslationAsync(cat.Id, l.Id,
                        keyName: keyName,
                        name: nameT,
                        slugOrTitle: vm?.Slug, // boşsa nameT’den türet
                        ct: ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["BlogMsg"] = "Blog kategorisi oluşturuldu.";
                return RedirectToAction(nameof(BlogIndex)); // blog liste sayfan
            }
            catch (ValidationException vex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", vex.Message);
                return View("CreateBlogCategory", model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Oluşturma hatası: {ex.Message}");
                return View("CreateBlogCategory", model);
            }
        }

        /* === Helpers === */

        // EN’den kısa, camelCase key üret; boşsa fallback
        private async Task<string> BuildCanonicalKeyNameForCategoryAsync(string trName, int idForFallback, CancellationToken ct)
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
        private async Task UpsertBlogCategoryTranslationAsync(
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
        // AdminController.cs (ilgili using'ler mevcut olmalı)

        #region Blog Categories: List page

        [HttpGet("blog-categories")]
        public async Task<IActionResult> BlogCategories(CancellationToken ct)
        {
            var trId = await _context.Langs.Where(l => l.LangCode == "tr")
                                           .Select(l => l.Id).FirstAsync(ct);

            var list = await (from c in _context.BlogCategories.AsNoTracking()
                              join t in _context.BlogCategoriesTranslations.AsNoTracking()
                                   .Where(x => x.LangCodeId == trId)
                                   on c.Id equals t.BlogCategoryId into jt
                              from tt in jt.DefaultIfEmpty()
                              orderby c.Order, c.Id
                              select new BlogCategoryListItemVM
                              {
                                  Id = c.Id,
                                  Name = tt != null ? tt.ValueText : "-",
                                  Order = c.Order,
                                  IsActive = c.IsActive
                              }).ToListAsync(ct);

            return View("BlogCategories", list);
        }
        // ===============================
        // BLOG CATEGORIES — REORDER + DELETE
        // ===============================

        public sealed class ReorderIdsReq
        {
            public List<int> Ids { get; set; } = new();
        }

        // Drag&drop sonrası sıralamayı kaydet
        [HttpPost("reorder-blog-categories")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderBlogCategories([FromBody] ReorderIdsReq req, CancellationToken ct)
        {
            if (req?.Ids == null || req.Ids.Count == 0)
                return BadRequest(new { ok = false, error = "Geçersiz liste." });

            // Güvenlik: sadece var olan kategoriler ve sizin listeniz kadar
            var cats = await _context.BlogCategories
                .Where(c => req.Ids.Contains(c.Id))
                .ToListAsync(ct);

            // Sıra: listedeki sıraya göre 0..N-1
            var orderMap = req.Ids.Select((id, idx) => (id, idx))
                                  .ToDictionary(x => x.id, x => x.idx);

            foreach (var c in cats)
                if (orderMap.TryGetValue(c.Id, out var newOrder))
                    c.Order = newOrder;

            await _context.SaveChangesAsync(ct);
            return Json(new { ok = true });
        }

        // Sert sil
        [HttpPost("delete-blog-category/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlogCategory(int id, CancellationToken ct)
        {
            var cat = await _context.BlogCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cat == null)
            {
                TempData["BlogMsg"] = "Kategori bulunamadı.";
                return RedirectToAction(nameof(BlogCategories));
            }

            try
            {
                // Bağlı çevirileri sil
                var trans = await _context.BlogCategoriesTranslations
                    .Where(t => t.BlogCategoryId == id)
                    .ToListAsync(ct);
                _context.BlogCategoriesTranslations.RemoveRange(trans);

                // İsteğe bağlı: eğer bu kategoriye bağlı blog post varsa, engelleyin
                var hasPosts = await _context.BlogPosts.AnyAsync(p => p.CategoryId == id, ct);
                if (hasPosts)
                    throw new InvalidOperationException("Bu kategoriye bağlı blog yazıları var. Önce yazıları başka kategoriye taşıyın ya da silin.");

                _context.BlogCategories.Remove(cat);
                await _context.SaveChangesAsync(ct);

                TempData["BlogMsg"] = "Kategori kalıcı olarak silindi.";
            }
            catch (Exception ex)
            {
                TempData["BlogMsg"] = "Silme başarısız: " + ex.Message;
            }

            return RedirectToAction(nameof(BlogCategories));
        }

        #endregion


        #region Blog Categories: Update GET

        [HttpGet("update-blog-category/{id:int}")]
        public async Task<IActionResult> UpdateBlogCategory(int id, CancellationToken ct)
        {
            var cat = await _context.BlogCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cat == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var trs = await _context.BlogCategoriesTranslations
                                      .Where(x => x.BlogCategoryId == id)
                                      .ToListAsync(ct);

            var vm = new UpdateBlogCategoryVM
            {
                Id = cat.Id,
                Order = cat.Order,
                IsActive = cat.IsActive,
                AutoTranslate = true,
                Langs = new List<UpdateBlogCategoryLangVM>()
            };

            foreach (var l in langs)
            {
                var t = trs.FirstOrDefault(x => x.LangCodeId == l.Id);
                vm.Langs.Add(new UpdateBlogCategoryLangVM
                {
                    LangCode = l.LangCode,
                    Name = t?.ValueText ?? "",
                    Slug = t?.Slug ?? ""
                });

                if (l.LangCode == "tr")
                    vm.NameTr = t?.ValueText ?? "";
            }

            return View("UpdateBlogCategory", vm);
        }

        #endregion


        #region Blog Categories: Update POST

        [HttpPost("update-blog-category/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBlogCategory(int id, UpdateBlogCategoryVM model, CancellationToken ct)
        {
            if (id != model.Id) return BadRequest();

            var cat = await _context.BlogCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cat == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var tr = langs.First(l => l.LangCode == "tr");

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                cat.Order = model.Order;
                cat.IsActive = model.IsActive;
                await _context.SaveChangesAsync(ct);

                var postedBy = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedBy.TryGetValue("tr", out var trVm);
                var trName = trVm?.Name ?? model.NameTr ?? "";

                var keyName = await BuildCanonicalKeyNameForCategoryAsync(trName, cat.Id, ct);

                // TR
                await UpsertBlogCategoryTranslationAsync(cat.Id, tr.Id, keyName, trName, trVm?.Slug, ct);

                // Other langs
                foreach (var l in langs.Where(x => x.Id != tr.Id))
                {
                    postedBy.TryGetValue(l.LangCode, out var vm);

                    string pick(string? v, string trv) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);

                    var nameT = pick(vm?.Name, trName);

                    await UpsertBlogCategoryTranslationAsync(cat.Id, l.Id, keyName, nameT, vm?.Slug, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["BlogMsg"] = "Kategori güncellendi.";
                return RedirectToAction(nameof(BlogCategories));
            }
            catch (ValidationException vex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", vex.Message);
                return View("UpdateBlogCategory", model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Güncelleme hatası: {ex.Message}");
                return View("UpdateBlogCategory", model);
            }
        }

        #endregion


        #region Blog Categories: Quick Create (for CreatePost modal)

        public sealed class QuickBlogCategoryReq
        {
            public string? NameTr { get; set; }
            public int Order { get; set; } = 0;
            public bool IsActive { get; set; } = true;
        }

        [HttpPost("create-blog-category-quick")]
        public async Task<IActionResult> CreateBlogCategoryQuick([FromBody] QuickBlogCategoryReq req, CancellationToken ct)
        {
            try
            {
                var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
                var tr = langs.First(x => x.LangCode == "tr");

                var cat = new BlogCategories { Order = req.Order, IsActive = req.IsActive };
                _context.BlogCategories.Add(cat);
                await _context.SaveChangesAsync(ct);

                var trName = (req.NameTr ?? "").Trim();
                var keyName = await BuildCanonicalKeyNameForCategoryAsync(trName, cat.Id, ct);

                // TR
                await UpsertBlogCategoryTranslationAsync(cat.Id, tr.Id, keyName, trName, null, ct);

                // others (AutoTranslate: evet, boşları doldur)
                foreach (var l in langs.Where(x => x.Id != tr.Id))
                {
                    var nameT = string.IsNullOrWhiteSpace(trName) ? "" : TryTranslate(trName, "tr", l.LangCode);
                    await UpsertBlogCategoryTranslationAsync(cat.Id, l.Id, keyName, nameT, null, ct);
                }
                await _context.SaveChangesAsync(ct);

                // TR adını geri dön dropdown için
                var trText = await _context.BlogCategoriesTranslations
                    .Where(x => x.BlogCategoryId == cat.Id && x.LangCodeId == tr.Id)
                    .Select(x => x.ValueText)
                    .FirstAsync(ct);

                return Json(new { ok = true, id = cat.Id, name = trText });
            }
            catch (ValidationException vex)
            {
                return Json(new { ok = false, error = vex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        #endregion


        #endregion


        #region Fairs sections

        // ===== AdminController.cs — FAIRS Bölümü =====
        // --- Reorder request tipleri (isim çakışması yaşamamak için ayrı) ---
        public sealed class FairReorderReq
        {
            public List<int> Ids { get; set; } = new();
        }

        // --- Helpers: KeyName ve Translation upsert ---

        // EN başlıktan camelCase key üret (boş ise yedek)
        private async Task<string> BuildCanonicalKeyForFairAsync(string trTitle, int fairId, CancellationToken ct)
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
        private async Task UpsertFairTranslationAsync(
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

        // ========== LIST (Admin) ==========
        [HttpGet("fairs")]
        public async Task<IActionResult> FairsIndex(CancellationToken ct)
        {
            var trId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync(ct);

            var list = await (from f in _context.Fairs.AsNoTracking()
                              join t in _context.FairTranslations.AsNoTracking().Where(x => x.LangCodeId == trId)
                                on f.Id equals t.FairId into jt
                              from tt in jt.DefaultIfEmpty()
                              orderby f.Order, f.Id
                              select new
                              {
                                  f.Id,
                                  f.StartDate,
                                  f.EndDate,
                                  f.Country,
                                  f.City,
                                  f.Venue,
                                  f.Order,
                                  f.IsActive,
                                  f.Cover424x460,
                                  Title = tt != null ? tt.Title : "(Başlık yok)"
                              }).ToListAsync(ct);

            ViewBag.Fairs = list;
            return View("FairsIndex"); // Razor'ı önceki mesajımda verdim
        }

        // ========== CREATE GET ==========
        [HttpGet("create-fair")]
        public async Task<IActionResult> CreateFair(CancellationToken ct)
        {
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var vm = new CreateFairVM
            {
                AutoTranslate = true,
                Langs = langs.Select(l => new FairLangVM { LangCode = l.LangCode }).ToList()
            };
            return View("CreateFair", vm);
        }

        // ========== CREATE POST ==========
        [HttpPost("create-fair")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFair(CreateFairVM model, CancellationToken ct)
        {
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var trLang = langs.First(x => x.LangCode == "tr");

            if (model.Cover == null)
                ModelState.AddModelError(nameof(model.Cover), "Kapak görseli zorunlu (424×460).");

            if (!ModelState.IsValid) return View("CreateFair", model);

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var fair = new Fairs
                {
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Country = model.Country,
                    City = model.City,
                    Venue = model.Venue,
                    WebsiteUrl = model.WebsiteUrl,
                    Order = model.Order,
                    IsActive = model.IsActive
                };
                _context.Fairs.Add(fair);
                await _context.SaveChangesAsync(ct); // Id için

                // Kapak
                var folder = Path.Combine(_env.WebRootPath, "uploads", "fairs", fair.Id.ToString());
                Directory.CreateDirectory(folder);
                fair.Cover424x460 = await SaveWebpVariantAsync(model.Cover!, folder, "fairs", "cover-424x460", 424, 460);
                await _context.SaveChangesAsync(ct);

                // Diller
                var postedBy = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedBy.TryGetValue("tr", out var trVm);
                var trTitle = trVm?.Title ?? model.TitleTr ?? "";

                var keyName = await BuildCanonicalKeyForFairAsync(trTitle, fair.Id, ct);

                // TR
                await UpsertFairTranslationAsync(fair.Id, trLang.Id, keyName, trTitle, trVm?.Slug, ct);

                // Diğer diller
                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    postedBy.TryGetValue(l.LangCode, out var vm);
                    string pick(string? v, string trv) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);

                    var titleT = pick(vm?.Title, trTitle);
                    await UpsertFairTranslationAsync(fair.Id, l.Id, keyName, titleT, vm?.Slug, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                TempData["FairMsg"] = "Fuar eklendi.";
                return RedirectToAction(nameof(FairsIndex));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", ex.Message);
                return View("CreateFair", model);
            }
        }

        // ========== UPDATE GET ==========
        [HttpGet("update-fair/{id:int}")]
        public async Task<IActionResult> UpdateFair(int id, CancellationToken ct)
        {
            var fair = await _context.Fairs.FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fair == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var trs = await _context.FairTranslations.Where(x => x.FairId == id).ToListAsync(ct);

            var vm = new UpdateFairVM
            {
                Id = fair.Id,
                StartDate = fair.StartDate,
                EndDate = fair.EndDate,
                Country = fair.Country,
                City = fair.City,
                Venue = fair.Venue,
                WebsiteUrl = fair.WebsiteUrl,
                Order = fair.Order,
                IsActive = fair.IsActive,
                ExistingCover = fair.Cover424x460,
                AutoTranslate = true,
                Langs = new List<FairLangVM>()
            };

            foreach (var l in langs)
            {
                var t = trs.FirstOrDefault(x => x.LangCodeId == l.Id);
                vm.Langs.Add(new FairLangVM { LangCode = l.LangCode, Title = t?.Title ?? "", Slug = t?.Slug ?? "" });
                if (l.LangCode == "tr") vm.TitleTr = t?.Title ?? "";
            }

            return View("UpdateFair", vm);
        }

        // ========== UPDATE POST ==========
        [HttpPost("update-fair/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFair(int id, UpdateFairVM model, CancellationToken ct)
        {
            if (id != model.Id) return BadRequest();
            var fair = await _context.Fairs.FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fair == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var trLang = langs.First(x => x.LangCode == "tr");

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                fair.StartDate = model.StartDate;
                fair.EndDate = model.EndDate;
                fair.Country = model.Country; fair.City = model.City; fair.Venue = model.Venue;
                fair.WebsiteUrl = model.WebsiteUrl;
                fair.Order = model.Order; fair.IsActive = model.IsActive;
                await _context.SaveChangesAsync(ct);

                if (model.Cover != null)
                {
                    var folder = Path.Combine(_env.WebRootPath, "uploads", "fairs", fair.Id.ToString());
                    Directory.CreateDirectory(folder);
                    fair.Cover424x460 = await SaveWebpVariantAsync(model.Cover!, folder, "fairs", "cover-424x460", 424, 460);
                    await _context.SaveChangesAsync(ct);
                }

                var postedBy = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedBy.TryGetValue("tr", out var trVm);
                var trTitle = trVm?.Title ?? model.TitleTr ?? "";

                var keyName = await BuildCanonicalKeyForFairAsync(trTitle, fair.Id, ct);

                // TR
                await UpsertFairTranslationAsync(fair.Id, trLang.Id, keyName, trTitle, trVm?.Slug, ct);

                // Diğer diller
                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    postedBy.TryGetValue(l.LangCode, out var vm);
                    string pick(string? v, string trv) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);
                    var titleT = pick(vm?.Title, trTitle);
                    await UpsertFairTranslationAsync(fair.Id, l.Id, keyName, titleT, vm?.Slug, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                TempData["FairMsg"] = "Fuar güncellendi.";
                return RedirectToAction(nameof(FairsIndex));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", ex.Message);
                return View("UpdateFair", model);
            }
        }

        // ========== DELETE (sert) ==========
        [HttpPost("delete-fair/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFair(int id, CancellationToken ct)
        {
            var f = await _context.Fairs.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (f == null) { TempData["FairMsg"] = "Bulunamadı."; return RedirectToAction(nameof(FairsIndex)); }

            var trs = await _context.FairTranslations.Where(x => x.FairId == id).ToListAsync(ct);
            _context.FairTranslations.RemoveRange(trs);
            _context.Fairs.Remove(f);
            await _context.SaveChangesAsync(ct);

            // klasörü de silmek istersen
            try
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "fairs", id.ToString());
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch { /* ignore */ }

            TempData["FairMsg"] = "Silindi.";
            return RedirectToAction(nameof(FairsIndex));
        }

        // ========== REORDER (drag&drop) ==========
        [HttpPost("reorder-fairs")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderFairs([FromBody] FairReorderReq req, CancellationToken ct)
        {
            if (req?.Ids == null || req.Ids.Count == 0) return BadRequest();

            var items = await _context.Fairs.Where(x => req.Ids.Contains(x.Id)).ToListAsync(ct);
            var map = req.Ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

            foreach (var it in items) if (map.TryGetValue(it.Id, out var ord)) it.Order = ord;

            await _context.SaveChangesAsync(ct);
            return Json(new { ok = true });
        }

        // ========== QUICK ADD (modal) ==========
        [HttpPost("create-fair-quick")]
        public async Task<IActionResult> CreateFairQuick([FromBody] QuickFairReq req, CancellationToken ct)
        {
            try
            {
                var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
                var tr = langs.First(x => x.LangCode == "tr");

                var f = new Fairs
                {
                    StartDate = req.StartDate,
                    EndDate = req.EndDate,
                    Country = req.Country,
                    City = req.City,
                    Venue = req.Venue,
                    Order = req.Order,
                    IsActive = req.IsActive
                };
                _context.Fairs.Add(f);
                await _context.SaveChangesAsync(ct);

                var trTitle = (req.TitleTr ?? "").Trim();
                var key = await BuildCanonicalKeyForFairAsync(trTitle, f.Id, ct);

                await UpsertFairTranslationAsync(f.Id, tr.Id, key, trTitle, null, ct);
                foreach (var l in langs.Where(x => x.Id != tr.Id))
                {
                    var titleT = string.IsNullOrWhiteSpace(trTitle) ? "" : TryTranslate(trTitle, "tr", l.LangCode);
                    await UpsertFairTranslationAsync(f.Id, l.Id, key, titleT, null, ct);
                }
                await _context.SaveChangesAsync(ct);

                return Json(new { ok = true, id = f.Id });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }


        #endregion

        #region Company
        // AdminController.cs içine
        [HttpGet("company")]
        public async Task<IActionResult> Company(CancellationToken ct)
        {
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var entity = await _context.CompanyInfos
                .Include(c => c.Translations)
                .FirstOrDefaultAsync(ct);

            var vm = new CompanyInfoVM();
            if (entity != null)
            {
                vm.Name = entity.Name;
                vm.LegalName = entity.LegalName;
                vm.TaxNumber = entity.TaxNumber;
                vm.MersisNo = entity.MersisNo;
                vm.Email = entity.Email;
                vm.Email2 = entity.Email2;
                vm.Phone = entity.Phone;
                vm.Phone2 = entity.Phone2;
                vm.Whatsapp = entity.Whatsapp;
                vm.Fax = entity.Fax;
                vm.Website = entity.Website;
                vm.Country = entity.Country;
                vm.City = entity.City;
                vm.District = entity.District;
                vm.AddressLine = entity.AddressLine;
                vm.PostalCode = entity.PostalCode;
                vm.MapEmbedUrl = entity.MapEmbedUrl;
                vm.WorkingHours = entity.WorkingHours;
                vm.FacebookUrl = entity.FacebookUrl;
                vm.TwitterUrl = entity.TwitterUrl;
                vm.InstagramUrl = entity.InstagramUrl;
                vm.LinkedInUrl = entity.LinkedInUrl;
                vm.YoutubeUrl = entity.YoutubeUrl;
                vm.LogoUrl = entity.LogoUrl;
                vm.MobilLogoUrl = entity.MobilLogoUrl;
                vm.IconUrl = entity.IconUrl;
                vm.LogoUrl = entity.LogoUrl;
                vm.MobilLogoUrl = entity.MobilLogoUrl;
                vm.IconUrl = entity.IconUrl;

                foreach (var l in langs)
                {
                    var tr = entity.Translations.FirstOrDefault(t => t.LangCodeId == l.Id);
                    vm.Langs.Add(new CompanyInfoLangVM
                    {
                        LangCode = l.LangCode,
                        AboutHtml = tr?.AboutHtml,
                        MissionHtml = tr?.MissionHtml,
                        VisionHtml = tr?.VisionHtml
                    });
                }
            }
            else
            {
                foreach (var l in langs)
                {
                    vm.Langs.Add(new CompanyInfoLangVM { LangCode = l.LangCode });
                }
            }

            return View("Company", vm);
        }
        [HttpPost("company")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Company(CompanyInfoVM model, CancellationToken ct)
        {
            var entity = await _context.CompanyInfos.Include(c => c.Translations).FirstOrDefaultAsync(ct);
            if (entity == null)
            {
                entity = new CompanyInfo();
                _context.CompanyInfos.Add(entity);
            }

            // tekil alanlar
            entity.Name = model.Name;
            entity.LegalName = model.LegalName;
            entity.TaxNumber = model.TaxNumber;
            entity.MersisNo = model.MersisNo;
            entity.Email = model.Email;
            entity.Email2 = model.Email2;
            entity.Phone = model.Phone;
            entity.Phone2 = model.Phone2;
            entity.Whatsapp = model.Whatsapp;
            entity.Fax = model.Fax;
            entity.Website = model.Website;
            entity.Country = model.Country;
            entity.City = model.City;
            entity.District = model.District;
            entity.AddressLine = model.AddressLine;
            entity.PostalCode = model.PostalCode;
            entity.MapEmbedUrl = model.MapEmbedUrl;
            entity.WorkingHours = model.WorkingHours;
            entity.FacebookUrl = model.FacebookUrl;
            entity.TwitterUrl = model.TwitterUrl;
            entity.InstagramUrl = model.InstagramUrl;
            entity.LinkedInUrl = model.LinkedInUrl;
            entity.YoutubeUrl = model.YoutubeUrl;


            // Görseller
            var folder = Path.Combine(_env.WebRootPath, "uploads", "company");
            Directory.CreateDirectory(folder);

            if (model.Logo != null && model.Logo.Length > 0)
            {
                TryDeleteFileByWebPath(entity.LogoUrl);
                await SaveWebpVariantAsync(model.Logo, folder, "company", "Logo", 146, 44);
            }
            if (model.MobilLogo != null && model.MobilLogo.Length > 0)
            {
                TryDeleteFileByWebPath(entity.MobilLogoUrl);
                await SaveWebpVariantAsync(model.MobilLogo, folder, "company", "MobilLogo", 268, 113);
            }
            if (model.Icon != null && model.Icon.Length > 0)
            {
                TryDeleteFileByWebPath(entity.IconUrl);
                await SaveWebpVariantAsync(model.Icon, folder, "company", "Icon", 192, 192);
            }

            // translations
            var langs = await _context.Langs.AsNoTracking().ToListAsync(ct);
            foreach (var l in langs)
            {
                var vmLang = model.Langs.FirstOrDefault(x => x.LangCode == l.LangCode);
                var tr = entity.Translations.FirstOrDefault(x => x.LangCodeId == l.Id);
                if (tr == null)
                {
                    tr = new CompanyInfoTranslation { LangCodeId = l.Id, CompanyInfoId = entity.Id };
                    entity.Translations.Add(tr);
                }
                tr.AboutHtml = vmLang?.AboutHtml ?? "";
                tr.MissionHtml = vmLang?.MissionHtml ?? "";
                tr.VisionHtml = vmLang?.VisionHtml ?? "";
            }

            await _context.SaveChangesAsync(ct);
            TempData["CompanyMsg"] = "Şirket bilgileri güncellendi.";
            return RedirectToAction(nameof(Company));
        }

        #endregion
        #region LEGAL (KVKK & Privacy)

        // Liste
        [HttpGet("legal")]
        public async Task<IActionResult> LegalIndex(CancellationToken ct)
        {
            var trId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync(ct);

            // yoksa 2 kayıt oluştur (privacy/kvkk)
            async Task EnsureRowAsync(string key)
            {
                var exists = await _context.LegalPages.AnyAsync(x => x.Key == key, ct);
                if (!exists)
                {
                    var p = new LegalPage { Key = key, IsActive = true, Order = key == "privacy" ? 0 : 1 };
                    _context.LegalPages.Add(p);
                    await _context.SaveChangesAsync(ct);
                    _context.LegalPageTranslations.Add(new LegalPageTranslation
                    {
                        LegalPageId = p.Id,
                        LangCodeId = trId,
                        Title = key.ToUpperInvariant(),
                        Html = ""
                    });
                    await _context.SaveChangesAsync(ct);
                }
            }
            await EnsureRowAsync("privacy");
            await EnsureRowAsync("kvkk");

            var list = await (from p in _context.LegalPages.AsNoTracking()
                              join t in _context.LegalPageTranslations.AsNoTracking().Where(x => x.LangCodeId == trId)
                                on p.Id equals t.LegalPageId into jt
                              from tt in jt.DefaultIfEmpty()
                              orderby p.Order, p.Id
                              select new LegalListItemVM
                              {
                                  Id = p.Id,
                                  Key = p.Key,
                                  IsActive = p.IsActive,
                                  Order = p.Order,
                                  TitleTr = tt != null ? tt.Title : "(Başlık yok)"
                              }).ToListAsync(ct);

            return View("LegalIndex", list);
        }

        // Düzenle GET
        [HttpGet("legal/{key}")]
        public async Task<IActionResult> EditLegal(string key, CancellationToken ct)
        {
            key = (key ?? "").ToLowerInvariant();
            if (key != "privacy" && key != "kvkk") return NotFound();

            var page = await _context.LegalPages.FirstOrDefaultAsync(x => x.Key == key, ct);
            if (page == null) return RedirectToAction(nameof(LegalIndex)); // güvenli

            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var trs = await _context.LegalPageTranslations.Where(x => x.LegalPageId == page.Id).ToListAsync(ct);

            var vm = new EditLegalVM
            {
                Id = page.Id,
                Key = page.Key,
                IsActive = page.IsActive,
                AutoTranslate = true,
                Langs = new()
            };

            foreach (var l in langs)
            {
                var t = trs.FirstOrDefault(x => x.LangCodeId == l.Id);
                vm.Langs.Add(new EditLegalLangVM
                {
                    LangCode = l.LangCode,
                    Title = t?.Title ?? "",
                    Html = t?.Html ?? "",
                    Slug = t?.Slug
                });
                if (l.LangCode == "tr")
                {
                    vm.TitleTr = t?.Title ?? "";
                    vm.HtmlTr = t?.Html ?? "";
                }
            }
            return View("EditLegal", vm);
        }

        // Yardımcı: Upsert translation + (opsiyonel) slug kontrolü (dil bazında benzersiz)
        private async Task UpsertLegalTranslationAsync(
            int pageId, int langId, string title, string html, string? slug, CancellationToken ct)
        {
            string? normSlug = null;
            if (!string.IsNullOrWhiteSpace(slug))
            {
                normSlug = ToLocalizedSlugSafe(slug);
                var taken = await _context.LegalPageTranslations
                    .AnyAsync(x => x.LangCodeId == langId && x.Slug == normSlug && x.LegalPageId != pageId, ct);
                if (taken) throw new ValidationException($"Slug çakışması: {normSlug}");
            }

            var row = await _context.LegalPageTranslations
                .FirstOrDefaultAsync(x => x.LegalPageId == pageId && x.LangCodeId == langId, ct);
            if (row == null)
            {
                _context.LegalPageTranslations.Add(new LegalPageTranslation
                {
                    LegalPageId = pageId,
                    LangCodeId = langId,
                    Title = title ?? "",
                    Html = html ?? "",
                    Slug = normSlug
                });
            }
            else
            {
                row.Title = title ?? "";
                row.Html = html ?? "";
                row.Slug = normSlug;
            }
        }

        // Düzenle POST
        [HttpPost("legal/{key}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLegal(string key, EditLegalVM model, CancellationToken ct)
        {
            key = (key ?? "").ToLowerInvariant();
            if (key != "privacy" && key != "kvkk") return NotFound();

            var page = await _context.LegalPages.FirstOrDefaultAsync(x => x.Id == model.Id && x.Key == key, ct);
            if (page == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var trLang = langs.First(x => x.LangCode == "tr");

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                page.IsActive = model.IsActive;
                page.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                var postedBy = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                postedBy.TryGetValue("tr", out var trVm);
                var trTitle = trVm?.Title ?? model.TitleTr ?? "";
                var trHtml = trVm?.Html ?? model.HtmlTr ?? "";

                // TR
                await UpsertLegalTranslationAsync(page.Id, trLang.Id, trTitle, trHtml, trVm?.Slug, ct);

                // Diğer diller: AutoTranslate sadece BOŞ alanları TR’den doldurur
                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    postedBy.TryGetValue(l.LangCode, out var vm);

                    string pick(string? v, string trv) =>
                        (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);

                    var titleT = pick(vm?.Title, trTitle);
                    var htmlT = pick(vm?.Html, trHtml);

                    await UpsertLegalTranslationAsync(page.Id, l.Id, titleT, htmlT, vm?.Slug, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["LegalMsg"] = $"{key.ToUpperInvariant()} güncellendi.";
                return RedirectToAction(nameof(LegalIndex));
            }
            catch (ValidationException vex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", vex.Message);
                return View("EditLegal", model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Hata: {ex.Message}");
                return View("EditLegal", model);
            }
        }
        #endregion

        #region SLIDES

        [HttpGet("slides")]
        public async Task<IActionResult> Slides(CancellationToken ct)
        {
            var trId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync(ct);

            var list = await (from s in _context.HomeSlides.AsNoTracking()
                              join t in _context.HomeSlideTranslations.AsNoTracking().Where(x => x.LangCodeId == trId)
                                on s.Id equals t.HomeSlideId into jt
                              from tt in jt.DefaultIfEmpty()
                              orderby s.Order, s.Id
                              select new SlideListItemVM
                              {
                                  Id = s.Id,
                                  IsActive = s.IsActive,
                                  Order = s.Order,
                                  Cover = s.Cover1920x900,
                                  TitleTr = tt != null ? (tt.Title ?? "") : ""
                              }).ToListAsync(ct);

            return View("Slides", list);
        }

        [HttpGet("slides/create")]
        public async Task<IActionResult> CreateSlide(CancellationToken ct)
        {
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var vm = new EditSlideVM
            {
                IsActive = true,
                AutoTranslate = true,
                Langs = langs.Select(l => new EditSlideLangVM { LangCode = l.LangCode }).ToList()
            };
            return View("EditSlide", vm);
        }

        [HttpGet("slides/{id:int}")]
        public async Task<IActionResult> UpdateSlide(int id, CancellationToken ct)
        {
            var s = await _context.HomeSlides.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s == null) return NotFound();

            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var trs = await _context.HomeSlideTranslations.Where(x => x.HomeSlideId == id).ToListAsync(ct);

            var vm = new EditSlideVM
            {
                Id = s.Id,
                IsActive = s.IsActive,
                Order = s.Order,
                AutoTranslate = true,
                ExistingCover = s.Cover1920x900,
                ExistingCoverMobile = s.CoverMobile768x1024,
                Langs = new()
            };

            foreach (var l in langs)
            {
                var t = trs.FirstOrDefault(x => x.LangCodeId == l.Id);
                vm.Langs.Add(new EditSlideLangVM
                {
                    LangCode = l.LangCode,
                    Slogan = t?.Slogan,
                    Title = t?.Title,
                    Content = t?.Content,
                    Cta1Text = t?.Cta1Text,
                    Cta1Url = t?.Cta1Url,
                    Cta2Text = t?.Cta2Text,
                    Cta2Url = t?.Cta2Url
                });

                if (l.LangCode == "tr")
                {
                    vm.SloganTr = t?.Slogan; vm.TitleTr = t?.Title; vm.ContentTr = t?.Content;
                    vm.Cta1TextTr = t?.Cta1Text; vm.Cta1UrlTr = t?.Cta1Url;
                    vm.Cta2TextTr = t?.Cta2Text; vm.Cta2UrlTr = t?.Cta2Url;
                }
            }
            return View("EditSlide", vm);
        }

        [HttpPost("slides/save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSlide(EditSlideVM model, CancellationToken ct)
        {
            var langs = await _context.Langs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
            var trLang = langs.First(l => l.LangCode == "tr");

            // Create'de kapak zorunlu
            if (model.Id == null && (model.Cover == null || model.Cover.Length == 0))
                ModelState.AddModelError(nameof(model.Cover), "Kapak görseli (1920x900) zorunlu.");

            if (!ModelState.IsValid)
                return View("EditSlide", model);

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                HomeSlide s;
                if (model.Id == null)
                {
                    s = new HomeSlide { IsActive = model.IsActive, Order = model.Order };
                    _context.HomeSlides.Add(s);
                    await _context.SaveChangesAsync(ct);
                }
                else
                {
                    s = await _context.HomeSlides.FirstAsync(x => x.Id == model.Id.Value, ct);
                    s.IsActive = model.IsActive;
                    s.Order = model.Order;
                    s.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(ct);
                }

                // Görseller
                var folder = Path.Combine(_env.WebRootPath, "uploads", "slides", s.Id.ToString());
                Directory.CreateDirectory(folder);

                if (model.Cover != null && model.Cover.Length > 0)
                {
                    TryDeleteFileByWebPath(s.Cover1920x900);

                    s.Cover1920x900 = await SaveWebpVariantAsync(model.Cover, folder, "slides", "cover-1920x900", 1920, 900);
                }
                if (model.CoverMobile != null && model.CoverMobile.Length > 0)
                {
                    TryDeleteFileByWebPath(s.CoverMobile768x1024);

                    s.CoverMobile768x1024 = await SaveWebpVariantAsync(model.CoverMobile, folder, "slides", "cover-mobile-768x1024", 768, 1024);
                }
                await _context.SaveChangesAsync(ct);

                // TR kaynak
                var byLang = model.Langs.ToDictionary(x => x.LangCode, x => x, StringComparer.OrdinalIgnoreCase);
                byLang.TryGetValue("tr", out var trVm);

                string pick(string? v, string trv, string to) =>
                    (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", to);

                var trSlogan = trVm?.Slogan ?? model.SloganTr ?? "";
                var trTitle = trVm?.Title ?? model.TitleTr ?? "";
                var trCont = trVm?.Content ?? model.ContentTr ?? "";

                await UpsertSlideTrAsync(s.Id, trLang.Id, trSlogan, trTitle, trCont,
                    trVm?.Cta1Text ?? model.Cta1TextTr,
                    trVm?.Cta1Url ?? model.Cta1UrlTr,
                    trVm?.Cta2Text ?? model.Cta2TextTr,
                    trVm?.Cta2Url ?? model.Cta2UrlTr, ct);

                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    byLang.TryGetValue(l.LangCode, out var vm);

                    var sl = pick(vm?.Slogan, trSlogan, l.LangCode);
                    var ti = pick(vm?.Title, trTitle, l.LangCode);
                    var co = pick(vm?.Content, trCont, l.LangCode);

                    var c1t = string.IsNullOrWhiteSpace(vm?.Cta1Text) && model.AutoTranslate && !string.IsNullOrWhiteSpace(model.Cta1TextTr)
                                ? TryTranslate(model.Cta1TextTr!, "tr", l.LangCode) : (vm?.Cta1Text ?? "");
                    var c2t = string.IsNullOrWhiteSpace(vm?.Cta2Text) && model.AutoTranslate && !string.IsNullOrWhiteSpace(model.Cta2TextTr)
                                ? TryTranslate(model.Cta2TextTr!, "tr", l.LangCode) : (vm?.Cta2Text ?? "");

                    await UpsertSlideTrAsync(s.Id, l.Id, sl, ti, co,
                        c1t, vm?.Cta1Url, c2t, vm?.Cta2Url, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["SlideMsg"] = model.Id == null ? "Slayt oluşturuldu." : "Slayt güncellendi.";
                return RedirectToAction(nameof(Slides));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Kaydetme hatası: {ex.Message}");
                return View("EditSlide", model);
            }
        }

        private async Task UpsertSlideTrAsync(
            int slideId, int langId, string? slogan, string? title, string? content,
            string? cta1Text, string? cta1Url, string? cta2Text, string? cta2Url, CancellationToken ct)
        {
            var row = await _context.HomeSlideTranslations
                .FirstOrDefaultAsync(x => x.HomeSlideId == slideId && x.LangCodeId == langId, ct);

            if (row == null)
            {
                _context.HomeSlideTranslations.Add(new HomeSlideTranslation
                {
                    HomeSlideId = slideId,
                    LangCodeId = langId,
                    Slogan = slogan,
                    Title = title,
                    Content = content,
                    Cta1Text = cta1Text,
                    Cta1Url = cta1Url,
                    Cta2Text = cta2Text,
                    Cta2Url = cta2Url
                });
            }
            else
            {
                row.Slogan = slogan; row.Title = title; row.Content = content;
                row.Cta1Text = cta1Text; row.Cta1Url = cta1Url; row.Cta2Text = cta2Text; row.Cta2Url = cta2Url;
            }
        }

        [HttpPost("slides/delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlide(int id, CancellationToken ct)
        {
            var s = await _context.HomeSlides.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s == null) return NotFound();

            _context.HomeSlides.Remove(s);
            await _context.SaveChangesAsync(ct);

            // klasörü de temizlemek istersen (opsiyonel)
            var folder = Path.Combine(_env.WebRootPath, "uploads", "slides", id.ToString());
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);

            TempData["SlideMsg"] = "Slayt silindi.";
            return RedirectToAction(nameof(Slides));
        }

        [HttpPost("slides/reorder")]
        public async Task<IActionResult> ReorderSlides([FromBody] ReorderIdsReq req, CancellationToken ct)
        {
            var items = await _context.HomeSlides.Where(p => req.Ids.Contains(p.Id)).ToListAsync(ct);
            int order = 0;
            foreach (var id in req.Ids)
            {
                var it = items.FirstOrDefault(x => x.Id == id);
                if (it != null) it.Order = order++;
            }
            await _context.SaveChangesAsync(ct);
            return Ok(new { ok = true });
        }

        #endregion

        // AdminController.cs (ilgili kısma ekle)
        #region Advantages

        [HttpGet("advantages")]
        public async Task<IActionResult> Advantages()
        {
            var list = await _context.Advantages
                .OrderBy(a => a.Order)
                .Select(a => new
                {
                    a.Id,
                    a.IsActive,
                    a.Order,
                    a.Image313Url,
                    Title = a.Translations.FirstOrDefault(t => t.Lang.LangCode == "tr")!.Title
                })
                .ToListAsync();

            return View("AdvantagesIndex", list);
        }

        [HttpGet("create-advantage")]
        public async Task<IActionResult> CreateAdvantage()
        {
            var langs = await _context.Langs.OrderBy(x => x.Id).ToListAsync();
            var vm = new CreateAdvantageVM
            {
                Langs = langs.Select(l => new AdvantageLangVM { LangCode = l.LangCode }).ToList()
            };
            return View(vm);
        }

        [HttpPost("create-advantage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdvantage(CreateAdvantageVM model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(model);

            var entity = new Advantage { Order = model.Order, IsActive = model.IsActive };
            _context.Advantages.Add(entity);
            await _context.SaveChangesAsync(ct);

            // görsel
            if (model.Image != null)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "advantages", entity.Id.ToString());
                Directory.CreateDirectory(folder);
                entity.Image313Url = await SaveWebpVariantAsync(model.Image, folder, "advantages/" , "img", 313, 313);

            }

            var langs = await _context.Langs.OrderBy(x => x.Id).ToListAsync(ct);
            foreach (var l in langs)
            {
                var vmLang = model.Langs.FirstOrDefault(x => x.LangCode == l.LangCode);
                var trText = model.Langs.FirstOrDefault(x => x.LangCode == "tr")?.Title ?? "";

                string pick(string? v, string trv) =>
                    (!model.AutoTranslate || !string.IsNullOrWhiteSpace(v)) ? (v ?? "") : TryTranslate(trv, "tr", l.LangCode);

                _context.AdvantageTranslations.Add(new AdvantageTranslation
                {
                    AdvantageId = entity.Id,
                    LangCodeId = l.Id,
                    Title = pick(vmLang?.Title, trText),
                    Content = pick(vmLang?.Content, model.Langs.FirstOrDefault(x => x.LangCode == "tr")?.Content ?? "")
                });
            }

            await _context.SaveChangesAsync(ct);
            TempData["Msg"] = "Avantaj eklendi.";
            return RedirectToAction(nameof(Advantages));
        }

        [HttpGet("update-advantage/{id:int}")]
        public async Task<IActionResult> UpdateAdvantage(int id)
        {
            var adv = await _context.Advantages.Include(a => a.Translations).FirstOrDefaultAsync(a => a.Id == id);
            if (adv == null) return NotFound();

            var langs = await _context.Langs.OrderBy(x => x.Id).ToListAsync();
            var vm = new UpdateAdvantageVM
            {
                Id = adv.Id,
                ExistingImage = adv.Image313Url,
                Order = adv.Order,
                IsActive = adv.IsActive,
                Langs = langs.Select(l =>
                {
                    var t = adv.Translations.FirstOrDefault(x => x.LangCodeId == l.Id);
                    return new AdvantageLangVM { LangCode = l.LangCode, Title = t?.Title, Content = t?.Content };
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost("update-advantage/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAdvantage(int id, UpdateAdvantageVM model, CancellationToken ct)
        {
            if (id != model.Id) return BadRequest();
            var adv = await _context.Advantages.Include(a => a.Translations).FirstOrDefaultAsync(a => a.Id == id);
            if (adv == null) return NotFound();

            adv.Order = model.Order;
            adv.IsActive = model.IsActive;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "advantages", adv.Id.ToString());
            Directory.CreateDirectory(folder);

            if (model.Image != null)
            {
                TryDeleteFileByWebPath(adv.Image313Url);
                adv.Image313Url = await SaveWebpVariantAsync(model.Image, folder, "advantages/" + adv.Id, "img", 313, 313);
                adv.Image313Url = adv.Image313Url.Replace($"/uploads/advantages/{adv.Id}/{adv.Id}", $"/uploads/products/{adv.Id}");

            }

            foreach (var vmLang in model.Langs)
            {
                var lang = await _context.Langs.FirstAsync(x => x.LangCode == vmLang.LangCode, ct);
                var tr = adv.Translations.FirstOrDefault(x => x.LangCodeId == lang.Id);
                if (tr == null)
                {
                    tr = new AdvantageTranslation { AdvantageId = adv.Id, LangCodeId = lang.Id };
                    adv.Translations.Add(tr);
                }
                tr.Title = vmLang.Title ?? "";
                tr.Content = vmLang.Content ?? "";
            }

            await _context.SaveChangesAsync(ct);
            TempData["Msg"] = "Avantaj güncellendi.";
            return RedirectToAction(nameof(Advantages));
        }

        [HttpPost("delete-advantage/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdvantage(int id, CancellationToken ct)
        {
            var adv = await _context.Advantages.FindAsync(new object[] { id }, ct);
            if (adv == null) return NotFound();

            TryDeleteFileByWebPath(adv.Image313Url);
            _context.Advantages.Remove(adv);
            await _context.SaveChangesAsync(ct);

            TempData["Msg"] = "Avantaj silindi.";
            return RedirectToAction(nameof(Advantages));
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
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".jfif", ".svg" };

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
        private async Task<string> SaveWebpVariantAsync(IFormFile file, string folder, string? relFolder, string fileNameNoExt, int width, int height)
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

        private void TryDeleteFileByWebPath(string? webPath)
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
        private static List<string> BuildAutoImageAltsList(int count, string productName)
        {
            var list = new List<string>(Math.Max(1, count));
            for (int i = 0; i < Math.Max(1, count); i++)
                list.Add(i == 0 ? productName : $"{productName} - image {i + 1}");
            return list;
        }


        private static string MakeSafeFileBaseName(string input)
        {
            var s = input.Trim();
            s = Regex.Replace(s, @"\s+", "-");
            s = Regex.Replace(s, @"[^a-zA-Z0-9\-_]", "");
            return string.IsNullOrWhiteSpace(s) ? "image" : s.ToLowerInvariant();
        }

        private async Task UpsertBlockHtmlAsync(int blockId, int langId, string html, CancellationToken ct)
        {
            var t = await _context.ProductContentBlockTranslations
                .FirstOrDefaultAsync(x => x.BlockId == blockId && x.LangCodeId == langId, ct);

            if (t == null)
                _context.ProductContentBlockTranslations.Add(new ProductContentBlockTranslation { BlockId = blockId, LangCodeId = langId, Html = html ?? "" });
            else
                t.Html = html ?? "";

            await _context.SaveChangesAsync(ct);
        }

        private async Task UpsertProductTranslationAsync(int productId, int langId, UpdateProductLangVM vm, int imageCount, bool isSource, CancellationToken ct)
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

        private string NormalizeAltsCsv(string? csv, string productName, int count, string langCode)
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


        private string BuildOrTranslateAlts(string? targetCsv, string? trCsv, string trName, int imageCount, bool auto, string src, string tgt)
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
        private string ToLocalizedSlugSafe(string lang, string text)
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
        private async Task<string> EnsureUniqueProductSlugAsync(
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
        private string TryTranslate(string text, string from, string toLower)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var tgt = (toLower ?? "en").ToUpperInvariant();   // "ar" -> "AR"
            var src = (from ?? "tr").ToUpperInvariant();
            try { return _translator.TranslateAsync(text, src, tgt).GetAwaiter().GetResult(); }
            catch { return text; }
        }

        private static List<string> BalanceAltCount(List<string> list, int count, string name)
        {
            var res = new List<string>(list);
            if (res.Count > count) res = res.Take(count).ToList();
            while (res.Count < count) res.Add($"{name} - image {res.Count + 1}");
            return res;
        }

        private static string NormalizeOrBuildAlts(string? csv, string name, int imageCount)
        {
            var list = SplitCsv(csv);
            if (list.Count == 0) list = BuildAutoImageAltsList(imageCount, name);
            return string.Join(", ", BalanceAltCount(list, imageCount, name));
        }

        // TextCaseHelper sende var; tek parametreli sürüm kullanılıyor :contentReference[oaicite:5]{index=5}
        private string ToLocalizedSlugSafe(string text)
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
        private async Task UpsertProductTranslationAsync(
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
        private async Task<bool> IsProductSlugTakenAsync(
            int langId, string slug, int? excludeProductId = null, CancellationToken ct = default)
        {
            var q = _context.ProductsTranslations.AsNoTracking()
                .Where(t => t.LangCodeId == langId && t.Slug == slug);
            if (excludeProductId.HasValue)
                q = q.Where(t => t.ProductId != excludeProductId.Value);
            return await q.AnyAsync(ct);
        }
        private static List<int> ParseIds(string? csv)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (var s in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(s.Trim(), out var n) && n > 0) list.Add(n);
            return list;
        }

        private static List<int> NormalizeOrderPayload(string[]? order, string? idsCsv)
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
        private async Task<bool> IsPostSlugTakenAsync(int langId, string slug, int? excludePostId = null, CancellationToken ct = default)
        {
            var q = _context.BlogPostsTranslations.AsNoTracking()
                .Where(t => t.LangCodeId == langId && t.Slug == slug);
            if (excludePostId.HasValue) q = q.Where(t => t.PostId != excludePostId.Value);
            return await q.AnyAsync(ct);
        }

        private async Task<string> EnsureUniquePostSlugAsync(int langId, string slugCandidate, int? excludePostId = null, CancellationToken ct = default)
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

        private async Task UpsertPostTranslationAsync(
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

        private async Task UpsertPostBlockHtmlAsync(int blockId, int langId, string html, CancellationToken ct = default)
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
        private async Task<string> BuildCanonicalKeyNameAsync(string trTitle, int idForFallback, CancellationToken ct)
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


        #endregion
    }
}

