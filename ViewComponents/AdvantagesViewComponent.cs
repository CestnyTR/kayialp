using kayialp.Context;
using kayialp.Services;
using kayialp.ViewModels.Advantages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.ViewComponents
{
    public class AdvantagesViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;
        private readonly ContentService _content;

        public AdvantagesViewComponent(kayialpDbContext context, ContentService content)
        {
            _context = context;
            _content = content;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Varsayım: Advantages tablosu: Id, IsActive, OrderNo, ImageUrl, LinkUrl (vb.)
            var rows = await _context.Advantages
                .Where(x => x.IsActive)
                .OrderBy(x => x.Order)
                .Select(x => new { x.Id, x.Image313Url })
                .ToListAsync();

            var vm = new AdvantagesViewModel
            {
                // İstersen başlıkları da ContentService'ten verelim (bulunamazsa default'lar kalır)
                SubTitle = _content.GetText("pages.home.advantages_subtitle") ?? "Avantajlarımız",
                Title    = _content.GetText("pages.home.advantages_title")    ?? "Neden Bizi Seçmelisiniz ?",
                SectionBg = "/img/bg/category_bg_1.png"
            };

            foreach (var r in rows)
            {
                var key = $"advantages.{r.Id}";
                vm.Items.Add(new AdvantageItemVM
                {
                    Id = r.Id,
                    ImageUrl = string.IsNullOrWhiteSpace(r.Image313Url) ? "/img/category/category_1_1.jpg" : r.Image313Url,
                    Title = _content.GetText($"{key}.title") ?? "",
                    Description = _content.GetText($"{key}.desc") ?? "",
                });
            }

            return View("/Views/Shared/Components/Advantages/Default.cshtml", vm);
        }
    }
}
