using kayialp.Context;
using kayialp.ViewModels;
using kayialp.ViewModels.Faq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace kayialp.ViewComponents
{
    public class FaqListViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;

        public FaqListViewComponent(kayialpDbContext ctx)
        {
            _context = ctx;
        }

        public async Task<IViewComponentResult> InvokeAsync(CancellationToken ct = default)
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var langs = await _context.Langs.ToListAsync(ct);

            var lang = langs.FirstOrDefault(l => l.LangCode == culture) ?? langs.First(l => l.LangCode == "tr");
            var trLang = langs.First(l => l.LangCode == "tr");

            var faqs = await _context.Faqs
                .Include(f => f.Translations)
                .OrderBy(f => f.Order)
                .ToListAsync(ct);

            var faqVm = new List<FaqViewModel>();
            foreach (var faq in faqs)
            {
                var tr = faq.Translations.FirstOrDefault(x => x.LangCodeId == lang.Id);
                if (tr != null)
                {
                    faqVm.Add(new FaqViewModel { Id = faq.Id, Question = tr.Question, Answer = tr.Answer });
                }
                else
                {
                    var trTr = faq.Translations.FirstOrDefault(x => x.LangCodeId == trLang.Id);
                    faqVm.Add(new FaqViewModel
                    {
                        Id = faq.Id,
                        Question = trTr?.Question ?? "",
                        Answer = trTr?.Answer ?? ""
                    });
                }
            }

            return View(faqVm);
        }
    }
}
