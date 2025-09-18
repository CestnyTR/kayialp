using kayialp.Context;
using kayialp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace kayialp.ViewComponents
{
    public class ProductGridViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _db;
        public ProductGridViewComponent(kayialpDbContext db) => _db = db;

        // categoryId veya slug gönderebilirsin; page/pageSize ile sayfalama
        public IViewComponentResult Invoke(int? categoryId = null, string? slug = null, int page = 1, int pageSize = 9)
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var langId = _db.Langs.AsNoTracking().FirstOrDefault(l => l.LangCode == culture)?.Id;

            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 48) pageSize = 9;

            // slug geldiyse kategoriyi bul (CategoriesTranslations.Slug + LangCodeId)
            if (!string.IsNullOrWhiteSpace(slug) && categoryId is null && langId is not null)
            {
                categoryId = _db.CategoriesTranslations
                                .AsNoTracking()
                                .Where(t => t.LangCodeId == langId && t.Slug == slug)
                                .Select(t => (int?)t.CategoriesId)
                                .FirstOrDefault();
            }

            // Ürün temel sorgu
            var baseQuery = _db.Products
                               .AsNoTracking()
                               .Where(p => categoryId == null || p.CategoryId == categoryId);

            var totalCount = baseQuery.Count();

            var items = baseQuery
                        .OrderBy(p => p.Order).ThenBy(p => p.Id)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(p => new ProductCardVM
                        {
                            Id = p.Id,
                            CategoryId = p.CategoryId,
                            ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? "/img/product/product_1_1.png" : p.ImageUrl
                        })
                        .ToList();

            if (langId is not null && items.Count > 0)
            {
                var productIds = items.Select(i => i.Id).ToList();
                var catIds = items.Select(i => i.CategoryId).Distinct().ToList();

                // Ürün çevirileri
                var ptAll = _db.ProductsTranslations
                               .AsNoTracking()
                               .Where(t => productIds.Contains(t.ProductId) && t.LangCodeId == langId)
                               .ToList();

                // Kategori çevirileri
                var ctAll = _db.CategoriesTranslations
                               .AsNoTracking()
                               .Where(t => catIds.Contains(t.CategoriesId) && t.LangCodeId == langId)
                               .ToList();

                foreach (var it in items)
                {
                    // Ürün adı & slug (pref: "title" ya da "name")
                    var pref = ptAll.FirstOrDefault(x => x.ProductId == it.Id && (x.KeyName == "title" || x.KeyName == "name"));
                    var any  = ptAll.FirstOrDefault(x => x.ProductId == it.Id);

                    it.Name = pref?.ValueText ?? any?.ValueText ?? "Unnamed Product";
                    it.Slug = pref?.Slug      ?? any?.Slug      ?? it.Id.ToString();

                    // Kategori adı etiketi
                    it.CategoryName = ctAll.FirstOrDefault(x => x.CategoriesId == it.CategoryId)?.ValueText ?? "Kategori";
                }
            }

            var vm = new ProductGridListVM
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Culture = culture,
                CategoryId = categoryId,
                CategorySlug = slug
            };

            return View(vm);
        }
    }

    // === VM'ler ===
    public class ProductCardVM
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string ImageUrl { get; set; } = "/img/product/product_1_1.png";
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string CategoryName { get; set; } = "";
    }

    public class ProductGridListVM
    {
        public List<ProductCardVM> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string Culture { get; set; } = "tr";
        public int? CategoryId { get; set; }
        public string? CategorySlug { get; set; }

        public int TotalPages => (int)System.Math.Ceiling((double)TotalCount / System.Math.Max(1, PageSize));
    }
}
