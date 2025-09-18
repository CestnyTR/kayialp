using kayialp.Context;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.IO.Compression;
using System.Linq;

namespace kayialp.ViewComponents
{
    public class CategoryListViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;

        public CategoryListViewComponent(kayialpDbContext context)
        {
            _context = context;
        }

        public IViewComponentResult Invoke()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var langId = _context.Langs.FirstOrDefault(l => l.LangCode == culture)?.Id;
            var categories = _context.Categories
                   .OrderBy(c => c.Order)
                   .Select(c => new CategoryVM
                   {
                       Id = c.Id,
                       // Category’de tanımlı ImageCard312x240 alanı var
                       ImageUrl = !string.IsNullOrEmpty(c.ImageCard312x240)
                                   ? c.ImageCard312x240
                                   : "/img/service/sv-1.jpg",

                       Name = _context.CategoriesTranslations
                           .Where(t => t.CategoriesId == c.Id && t.LangCodeId == langId)
                           .Select(t => t.ValueText)
                           .FirstOrDefault() ?? "NoName",

                       Slug = _context.CategoriesTranslations
                           .Where(t => t.CategoriesId == c.Id && t.LangCodeId == langId)
                           .Select(t => t.Slug)
                           .FirstOrDefault() ?? ""
                   })
                   .ToList();

            return View(categories);
        }
    }

    public class CategoryVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string ImageUrl { get; set; } = "";
    }
}
