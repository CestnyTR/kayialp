using System.Globalization;
using kayialp.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var langId = _context.Langs.AsNoTracking()
                          .Where(l => l.LangCode == culture)
                          .Select(l => (int?)l.Id)
                          .FirstOrDefault()
                       ?? _context.Langs.AsNoTracking().Select(l => (int?)l.Id).FirstOrDefault()
                       ?? 1;

            // Tek join ile Name + Slug çek
            var categories = _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Order)
                .GroupJoin(
                    _context.CategoriesTranslations.AsNoTracking()
                        .Where(t => t.LangCodeId == langId),
                    c => c.Id,
                    t => t.CategoriesId,
                    (c, trs) => new { c, tr = trs.FirstOrDefault() }
                )
                .Select(x => new CategoryVM
                {
                    Id = x.c.Id,
                    ImageUrl = string.IsNullOrWhiteSpace(x.c.ImageCard312x240)
                               ? "/img/service/sv-1.jpg"
                               : x.c.ImageCard312x240!,
                    Name = x.tr != null && !string.IsNullOrWhiteSpace(x.tr.ValueText)
                               ? x.tr.ValueText
                               : "NoName",
                    Slug = x.tr != null ? (x.tr.Slug ?? "") : ""
                })
                .ToList();

            return View(categories); // Views/Shared/Components/CategoryList/Default.cshtml
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
