using kayialp.Context;
using kayialp.Helpers;
using kayialp.Models;
using kayialp.Services;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.Controllers.Admin
{
    public class FaqController : Controller
    {
        private readonly ITranslationService _translator;

        private readonly kayialpDbContext _context;
        private readonly AdminHelper _helper;
        public FaqController(kayialpDbContext ctx, ITranslationService translator, AdminHelper helper)
        {
            _context = ctx;
            _translator = translator;

            _helper = helper;
        }

        // Listeleme
        [HttpGet("/admin/faq")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var trId = await _context.Langs
                .Where(l => l.LangCode == "tr")
                .Select(l => l.Id)
                .FirstAsync(ct);

            ViewBag.TrId = trId;

            var faqs = await _context.Faqs
                .Include(f => f.Translations)
                .OrderBy(f => f.Order)
                .ToListAsync(ct);

            return View("~/Views/Admin/Faq/Index.cshtml", faqs);
        }
        // Ekleme formu
        [HttpGet("/admin/faq/create")]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var langs = await _context.Langs.ToListAsync(ct);

            var vm = new FaqEditViewModel
            {
                Translations = langs.Select(l => new FaqTranslationVM
                {
                    LangCodeId = l.Id,
                    LangCode = l.LangCode
                }).ToList()
            };
            return View("~/Views/Admin/Faq/Create.cshtml", vm);
        }
        [HttpPost("/admin/faq/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FaqEditViewModel vm, CancellationToken ct)
        {
            var langs = await _context.Langs.AsNoTracking().ToListAsync(ct);
            var trLang = langs.First(l => l.LangCode == "tr");

            // ✅ Türkçe boşsa hata ver
            var trVm = vm.Translations.FirstOrDefault(x => x.LangCode == "tr");
            if (trVm == null || string.IsNullOrWhiteSpace(trVm.Question) || string.IsNullOrWhiteSpace(trVm.Answer))
            {
                ModelState.AddModelError("", "Türkçe soru ve cevap zorunludur.");
                return View("~/Views/Admin/Faq/Create.cshtml", vm);
            }

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var faq = new Faqs { Order = vm.Order };
                _context.Faqs.Add(faq);
                await _context.SaveChangesAsync(ct);

                // TR kaydı
                await _helper.UpsertFaqTranslationAsync(faq.Id, trLang.Id, trVm.Question, trVm.Answer, ct);

                // Diğer diller
                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    var langVm = vm.Translations.FirstOrDefault(x => x.LangCodeId == l.Id);

                    string pick(string? v, string trv, string langCode) =>
                        (!vm.AutoTranslate || !string.IsNullOrWhiteSpace(v))
                            ? (v ?? "")
                            : _translator.TranslateAsync(trv, "tr", langCode).Result;

                    var q = pick(langVm?.Question, trVm.Question, l.LangCode);
                    var a = pick(langVm?.Answer, trVm.Answer, l.LangCode);

                    await _helper.UpsertFaqTranslationAsync(faq.Id, l.Id, q, a, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["FaqMsg"] = "SSS başarıyla eklendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Oluşturma hatası: {ex.Message}");
                return View("~/Views/Admin/Faq/Create.cshtml", vm);
            }
        }
        // Düzenleme
        [HttpGet("/admin/faq/edit/{id}")]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var faq = await _context.Faqs
                .Include(f => f.Translations)
                .FirstOrDefaultAsync(f => f.Id == id, ct);

            if (faq == null) return NotFound();

            var langs = await _context.Langs.ToListAsync(ct);
            var vm = new FaqEditViewModel
            {
                Id = faq.Id,
                Order = faq.Order,
                Translations = langs.Select(l =>
                {
                    var tr = faq.Translations.FirstOrDefault(x => x.LangCodeId == l.Id);
                    return new FaqTranslationVM
                    {
                        LangCodeId = l.Id,
                        LangCode = l.LangCode,
                        Question = tr?.Question ?? "",
                        Answer = tr?.Answer ?? ""
                    };
                }).ToList()
            };

            return View("~/Views/Admin/Faq/Edit.cshtml", vm);
        }

        [HttpPost("/admin/faq/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FaqEditViewModel vm, CancellationToken ct)
        {
            var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == vm.Id, ct);
            if (faq == null) return NotFound();

            faq.Order = vm.Order;

            var langs = await _context.Langs.AsNoTracking().ToListAsync(ct);
            var trLang = langs.First(l => l.LangCode == "tr");

            // ✅ Türkçe boşsa hata ver
            var trVm = vm.Translations.FirstOrDefault(x => x.LangCode == "tr");
            if (trVm == null || string.IsNullOrWhiteSpace(trVm.Question) || string.IsNullOrWhiteSpace(trVm.Answer))
            {
                ModelState.AddModelError("", "Türkçe soru ve cevap zorunludur.");
                return View("~/Views/Admin/Faq/Edit.cshtml", vm);
            }

            using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // TR kaydı
                await _helper.UpsertFaqTranslationAsync(faq.Id, trLang.Id, trVm.Question, trVm.Answer, ct);

                // Diğer diller
                foreach (var l in langs.Where(x => x.Id != trLang.Id))
                {
                    var langVm = vm.Translations.FirstOrDefault(x => x.LangCodeId == l.Id);

                    string pick(string? v, string trv, string langCode) =>
                        (!vm.AutoTranslate || !string.IsNullOrWhiteSpace(v))
                            ? (v ?? "")
                            : _translator.TranslateAsync(trv, "tr", langCode).Result;

                    var q = pick(langVm?.Question, trVm.Question, l.LangCode);
                    var a = pick(langVm?.Answer, trVm.Answer, l.LangCode);

                    await _helper.UpsertFaqTranslationAsync(faq.Id, l.Id, q, a, ct);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["FaqMsg"] = "SSS başarıyla güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError("", $"Güncelleme hatası: {ex.Message}");
                return View("~/Views/Admin/Faq/Edit.cshtml", vm);
            }
        }
        // Silme
        [HttpPost("/admin/faq/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var faq = await _context.Faqs
                .Include(f => f.Translations)
                .FirstOrDefaultAsync(f => f.Id == id, ct);

            if (faq == null) return NotFound();

            _context.Faqs.Remove(faq);
            await _context.SaveChangesAsync(ct);

            TempData["FaqMsg"] = "SSS başarıyla silindi.";
            return RedirectToAction("Index");
        }


        [HttpPost("/admin/faq/update-order")]
        public async Task<IActionResult> UpdateOrder([FromBody] List<FaqOrderVM> model, CancellationToken ct)
        {
            foreach (var item in model)
            {
                var faq = await _context.Faqs.FindAsync(new object[] { item.Id }, ct);
                if (faq != null)
                {
                    faq.Order = item.Order;
                }
            }

            await _context.SaveChangesAsync(ct);
            return Ok();
        }



    }
}
