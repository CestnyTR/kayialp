using kayialp.Context;
using kayialp.Services;
using kayialp.ViewModels.Slider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.ViewComponents
{
    public class SliderViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;
        private readonly ContentService _content;

        public SliderViewComponent(kayialpDbContext context, ContentService content)
        {
            _context = context;
            _content = content;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Aktif, sıralı slide'ları çek
            var slides = await _context.HomeSlides
                .Where(s => s.IsActive)
                .OrderBy(s => s.Order)
                .Select(s => new
                {
                    s.Id,
                    s.Cover1920x900,
                    s.CoverMobile768x1024
                })
                .ToListAsync();

            var vm = new SliderViewModel();

            foreach (var s in slides)
            {
                // ContentService ile çeviriler
                string baseKey = $"homeslide.{s.Id}";

                var item = new SliderItemVM
                {
                    Id = s.Id,
                    DesktopImageUrl = s.Cover1920x900 ?? "",
                    MobileImageUrl = s.CoverMobile768x1024 ?? "",

                    Slogan  = _content.GetText($"{baseKey}.slogan")  ?? "",
                    Title   = _content.GetText($"{baseKey}.title")   ?? "",
                    Content = _content.GetText($"{baseKey}.content") ?? "",

                    Cta1Text = _content.GetText($"{baseKey}.cta1text") ?? "",
                    Cta1Url  = _content.GetText($"{baseKey}.cta1url")  ?? "#",

                    Cta2Text = _content.GetText($"{baseKey}.cta2text") ?? "",
                    Cta2Url  = _content.GetText($"{baseKey}.cta2url")  ?? "#",
                };

                vm.Items.Add(item);
            }

            return View("/Views/Shared/Components/Slider/Default.cshtml", vm);
        }
    }
}
