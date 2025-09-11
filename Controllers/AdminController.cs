using kayialp.Context;
using kayialp.Models;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
namespace kayialp.Controllers
{
    public class AdminController : Controller
    {
        private readonly kayialpDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(kayialpDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;

        }

        public async Task<IActionResult> Index()
        {
            var langId = 2; // Türkçe dili sabit
            var categoriesWithTranslations = await _context.Categories
                .OrderBy(c => c.Order)
                .Join(
                    _context.CategoriesTranslations,
                    category => category.Id,
                    translation => translation.CategoriesId,
                    (category, translation) => new { Category = category, Translation = translation }
                )
                .Where(joined => joined.Translation.LangCodeId == langId)
                .Select(joined => new CategoryViewModel
                {
                    Id = joined.Category.Id,
                    Name = joined.Translation.ValueText
                })
                .ToListAsync();
            return View(categoriesWithTranslations);
        }


        public async Task<IActionResult> Categories()
        {
            var langId = 2; // Türkçe dili sabit

            var categoriesWithTranslations = await _context.Categories
                .OrderBy(c => c.Order)
                .Join(
                    _context.CategoriesTranslations,
                    category => category.Id,
                    translation => translation.CategoriesId,
                    (category, translation) => new { Category = category, Translation = translation }
                )
                .Where(joined => joined.Translation.LangCodeId == langId)
                .Select(joined => new CategoryViewModel
                {
                    Id = joined.Category.Id,
                    Name = joined.Translation.KeyName
                })
                .ToListAsync(); // ← ASENKRON ÇAĞRI

            Console.WriteLine("Kategori sayısı: " + categoriesWithTranslations.Count);

            return View("Index", categoriesWithTranslations);
        }

        [HttpGet]
        public IActionResult CreateCategory()
        {
            var model = new CreateCategoryViewModel
            {
                LangCodeId = 2,
                Order = 0
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CreateCategoryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var category = new Categories
            {
                Order = model.Order
            };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var translation = new CategoriesTranslations
            {
                CategoriesId = category.Id,
                LangCodeId = model.LangCodeId,
                KeyName = model.Name,
                ValueText = model.Name
            };
            _context.CategoriesTranslations.Add(translation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

    }
}
