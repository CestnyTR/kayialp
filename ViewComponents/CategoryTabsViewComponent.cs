using System.Globalization;
using kayialp.Context;
using kayialp.Services;
using kayialp.ViewModels.Categories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.ViewComponents
{
    public class CategoryTabsViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;
        private readonly ContentService _content;

        public CategoryTabsViewComponent(kayialpDbContext context, ContentService content)
        {
            _context = context;
            _content = content;
        }

        private int? GetLangId(string two) =>
            _context.Langs.FirstOrDefault(l => l.LangCode == two)?.Id;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var langId = GetLangId(culture) ?? GetLangId("en")
                        ?? _context.Langs.Select(x => (int?)x.Id).FirstOrDefault();

            var defaultLangId = GetLangId("en") ?? _context.Langs.Select(x => (int?)x.Id).FirstOrDefault();

            var vm = new CategoryTabsViewModel
            {
                SectionSubTitle = _content.GetText("pages.home.case_section_subtitle") ?? "Kayıalp Makineleri",
                SectionTitle    = _content.GetText("pages.home.case_section_title")    ?? "İleri Teknoloji ve Mühendislik: Kayıalp Makineleri"
            };

            // 1) Kategoriler (vitrin görseli + sıralama)
            var categories = await _context.Categories
                .OrderBy(c => c.Order)
                .Select(c => new { c.Id, c.ImageShowcase423x636 })
                .ToListAsync();

            if (categories.Count == 0)
                return View("/Views/Shared/Components/CategoryTabs/Default.cshtml", vm);

            var ids = categories.Select(c => c.Id).ToList();

            // 2) Çevirileri tek seferde çek: aktif dil + fallback dil
            var curTrs = await _context.CategoriesTranslations
                .Where(t => ids.Contains(t.CategoriesId) && t.LangCodeId == langId)
                .ToListAsync();

            var defTrs = (defaultLangId != null && defaultLangId != langId)
                ? await _context.CategoriesTranslations
                    .Where(t => ids.Contains(t.CategoriesId) && t.LangCodeId == defaultLangId)
                    .ToListAsync()
                : new List<Models.CategoriesTranslations>();

            // Yardımcılar (öncelik sırası ile)
            string? BestName(int id)
            {
                string? pick(IEnumerable<Models.CategoriesTranslations> set, params string[] keys)
                    => set.FirstOrDefault(t => t.CategoriesId == id && keys.Contains(t.KeyName))?.ValueText
                       ?? set.FirstOrDefault(t => t.CategoriesId == id && !string.IsNullOrWhiteSpace(t.ValueText))?.ValueText;

                return pick(curTrs, "name", "title")
                    ?? pick(defTrs, "name", "title");
            }

            string? BestSlug(int id)
            {
                string? pick(IEnumerable<Models.CategoriesTranslations> set, params string[] keys)
                    => set.FirstOrDefault(t => t.CategoriesId == id && keys.Contains(t.KeyName) && !string.IsNullOrWhiteSpace(t.Slug))?.Slug
                       ?? set.FirstOrDefault(t => t.CategoriesId == id && !string.IsNullOrWhiteSpace(t.Slug))?.Slug;

                return pick(curTrs, "name", "title")
                    ?? pick(defTrs, "name", "title");
            }

            string? BestSub(int id)
            {
                string? pick(IEnumerable<Models.CategoriesTranslations> set, params string[] keys)
                    => set.FirstOrDefault(t => t.CategoriesId == id && keys.Contains(t.KeyName))?.ValueText;

                return pick(curTrs, "subtitle") ?? pick(curTrs, "desc")
                    ?? pick(defTrs, "subtitle")   ?? pick(defTrs, "desc");
            }

            // 3) Kart listesi (doğru ad/alt yazı/link)
            var cards = new List<CategoryTabsItemVM>();
            foreach (var c in categories)
            {
                var title = BestName(c.Id) ?? "";           // <-- ID fallback KALDIRILDI
                var slug  = BestSlug(c.Id);
                var link  = !string.IsNullOrWhiteSpace(slug)
                    ? $"/{culture}/products/category/{slug}"
                    : $"/{culture}/products?c={c.Id}";

                cards.Add(new CategoryTabsItemVM
                {
                    Id = c.Id,
                    Title = title,
                    ImageUrl = string.IsNullOrWhiteSpace(c.ImageShowcase423x636)
                               ? "/img/case/case_1_1.jpg"
                               : c.ImageShowcase423x636!,
                    LinkUrl = link,
                    SubTitle = BestSub(c.Id) ?? ""
                });
            }

            // 4) Sekmeler – sekme başlığı doğrudan kategori adı
            for (int i = 0; i < cards.Count; i++)
            {
                var tabId = $"nav-step{i + 1}";
                var rotated = cards.Skip(i).Concat(cards.Take(i)).ToList();

                vm.Tabs.Add(new CategoryTabVM
                {
                    TabId = tabId,
                    TabTitle = string.IsNullOrWhiteSpace(cards[i].Title) ? $"Category" : cards[i].Title,
                    Items = rotated
                });
            }

            return View("/Views/Shared/Components/CategoryTabs/Default.cshtml", vm);
        }
    }
}
