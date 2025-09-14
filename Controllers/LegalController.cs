// Controllers/LegalController.cs  (public site)
using kayialp.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class LegalController : Controller
{
    private readonly kayialpDbContext _context;
    public LegalController(kayialpDbContext ctx) { _context = ctx; }

    [HttpGet("{culture}/privacy")]
    public Task<IActionResult> Privacy(CancellationToken ct) => Show("privacy", ct);

    [HttpGet("{culture}/kvkk")]
    public Task<IActionResult> Kvkk(CancellationToken ct) => Show("kvkk", ct);

    private async Task<IActionResult> Show(string key, CancellationToken ct)
    {
        var culture = (Request.Query["culture"].ToString() ?? "tr").ToLowerInvariant();
        var langId = await _context.Langs.Where(l => l.LangCode == culture).Select(l => l.Id).FirstOrDefaultAsync(ct);
        if (langId == 0)
            langId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync(ct);

        var page = await _context.LegalPages.AsNoTracking().FirstOrDefaultAsync(p => p.Key == key && p.IsActive, ct);
        if (page == null) return NotFound();

        var tr = await _context.LegalPageTranslations.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.LegalPageId == page.Id && t.LangCodeId == langId, ct);

        ViewData["Title"] = tr?.Title ?? key.ToUpperInvariant();
        ViewBag.Html = tr?.Html ?? "";
        return View("Legal"); // tek view
    }
}
