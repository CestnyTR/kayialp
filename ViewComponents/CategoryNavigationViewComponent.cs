using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using kayialp.Context;
using kayialp.Models;
using kayialp.ViewModels;
using System.Linq;
using System.Threading.Tasks;
namespace kayialp.ViewModels
{

    public class CategoryNavigationViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;

        public CategoryNavigationViewComponent(kayialpDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var trLangId = await _context.Langs
                .Where(l => l.LangCode == "tr")
                .Select(l => l.Id)
                .FirstOrDefaultAsync();

            var categories = await _context.Categories
                .OrderBy(c => c.Order)
                .Join(
                    _context.CategoriesTranslations,
                    category => category.Id,
                    translation => translation.CategoriesId,
                    (category, translation) => new { Category = category, Translation = translation }
                )
                .Where(joined => joined.Translation.LangCodeId == trLangId)
                .Select(joined => new CategoryViewModel
                {
                    Id = joined.Category.Id,
                    Name = joined.Translation.KeyName
                })
                .ToListAsync();

            return View(categories);
        }
    }
}
