using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using kayialp.Context;
using kayialp.ViewModels.Product;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
// Eğer ProductContent / ProductAttribute tabloları ayrı namespacede ise:
using ProductModels;

namespace kayialp.ViewComponents
{
    public class ProductDetailsViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _db;
        private readonly IHostEnvironment _env;

        public ProductDetailsViewComponent(kayialpDbContext db, IHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private static List<string> SplitCsv(string? csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? new List<string>()
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim())
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .ToList();

        public async Task<IViewComponentResult> InvokeAsync(int id)
        {
            // aktif dil id
            var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var langId = await _db.Langs
                .Where(l => l.LangCode == two)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync() ?? await _db.Langs.Select(x => x.Id).FirstAsync();

            // ürün
            var product = await _db.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return View("NotFound");

            // başlık (ProductsTranslations)
            var title = await _db.ProductsTranslations
                .Where(t => t.ProductId == id && t.LangCodeId == langId &&
                            (t.KeyName == "title" || t.KeyName == "name"))
                .Select(t => t.ValueText)
                .FirstOrDefaultAsync() ?? "";

            // kart üzerinde kısa açıklama / açıklama / ürün hakkında (ProductContent*)
            var blocks = await _db.Set<ProductContent>()
                .Where(c => c.ProductId == id && c.IsActive)
                .OrderBy(c => c.Order)
                .SelectMany(c => _db.Set<ProductContentBlock>()
                                    .Where(b => b.ProductContentId == c.Id && b.IsActive))
                .Include(b => b.Translations)
                .ToListAsync();

            string pick(string type) =>
                blocks.Where(b => b.BlockType == type)
                      .SelectMany(b => b.Translations)
                      .Where(tr => tr.LangCodeId == langId)
                      .OrderBy(tr => tr.Id)
                      .Select(tr => tr.Html)
                      .FirstOrDefault();

            var shortDesc = pick("short_desc");
            var desc      = pick("description");
            var about     = pick("about");

            // özellikler (ProductAttribute* tablolarından)
            var attrPairs = await _db.Set<ProductAttributeGroup>()
                .Where(g => g.ProductId == id && g.IsActive)
                .OrderBy(g => g.Order)
                .SelectMany(g => _db.Set<ProductAttribute>()
                                    .Where(a => a.GroupId == g.Id && a.IsActive)
                                    .OrderBy(a => a.Order))
                .SelectMany(a => _db.Set<ProductAttributeTranslation>()
                                    .Where(t => t.AttributeId == a.Id && t.LangCodeId == langId)
                                    .Select(t => new { t.Name, t.Value }))
                .ToListAsync();

            // GÖRSELLER — dosya sisteminden (admin’deki mantığın aynısı)
            var webRoot = _env.ContentRootPath; // IHostEnvironment: ContentRoot -> app root
            var wwwroot = Path.Combine(webRoot, "wwwroot");
            var folder  = Path.Combine(wwwroot, "uploads", "products", product.Id.ToString());

            var fileNames = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.webp").Select(Path.GetFileName)!.ToList()
                : new List<string>();

            // Kapak ilk sırada
            var coverFile = Path.GetFileName(product.ImageUrl ?? "");
            if (!string.IsNullOrWhiteSpace(coverFile) && fileNames.Contains(coverFile))
                fileNames = fileNames.Where(f => f != coverFile).Prepend(coverFile).ToList();

            // URL listesi
            var urls = fileNames.Select(name => $"/uploads/products/{product.Id}/{name}").ToList();

            // Alt yazılar: ProductsTranslations.ImageAlts (CSV)
            var trRow = await _db.ProductsTranslations
                .Where(t => t.ProductId == id && t.LangCodeId == langId)
                .OrderBy(t => t.Id)
                .Select(t => new { t.ValueText, t.ImageAlts })
                .FirstOrDefaultAsync();

            var alts = SplitCsv(trRow?.ImageAlts);
            // eksikse başlıktan doldur
            while (alts.Count < urls.Count) alts.Add(trRow?.ValueText ?? title);
            if (alts.Count > urls.Count)    alts = alts.Take(urls.Count).ToList();

            var vm = new ProductDetailsVM
            {
                ProductId       = id,
                Title           = title,
                CategoryName    = product.CategoryId.ToString() , // istersen CategoryTranslations ile zenginleştir
                InStock         = product.Stock > 0,

                CoverImage      = urls.FirstOrDefault() ?? "",
                GalleryImages   = urls.Skip(1).ToList(),
                GalleryAlts     = alts,

                ShortDescHtml   = shortDesc,
                DescriptionHtml = desc,
                AboutHtml       = about,

                Attributes      = attrPairs.Select(x => (x.Name ?? "", x.Value ?? "")).ToList()
            };

            return View(vm);
        }
    }
}
