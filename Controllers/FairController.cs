// Controllers/FairController.cs  (public site)
using kayialp.Context;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography.Xml;

public class FairController : Controller
{
    private readonly kayialpDbContext _context;

    public FairController(kayialpDbContext context)
    {
        _context = context;
    }

    [HttpGet("{culture}/fairs")]
    public async Task<IActionResult> Index(int? year, string? country, CancellationToken ct)
    {
        // 1) Dil id’si (querystring culture varsa onu kullan; yoksa tr)
        var culture = (Request.Query["culture"].ToString() ?? "tr").ToLowerInvariant();
        var langId = await _context.Langs
            .Where(l => l.LangCode == culture)
            .Select(l => l.Id)
            .FirstOrDefaultAsync(ct);

        if (langId == 0)
            langId = await _context.Langs.Where(l => l.LangCode == "tr").Select(l => l.Id).FirstAsync(ct);

        // 2) Yıllar — EF için çevirilebilir UNION
        var years = await _context.Fairs.Select(f => f.StartDate.Year)
                        .Union(_context.Fairs.Select(f => f.EndDate.Year))
                        .Distinct()
                        .OrderByDescending(y => y)
                        .ToListAsync(ct);

        // 3) Ülke listesi (boş olmayanlar)
        var countries = await _context.Fairs
            .Where(f => f.Country != null && f.Country != "")
            .Select(f => f.Country!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        // 4) Kart sorgusu (filtreler)
        var q = from f in _context.Fairs.AsNoTracking()
                join t in _context.FairTranslations.AsNoTracking().Where(x => x.LangCodeId == langId)
                    on f.Id equals t.FairId into tj
                from tt in tj.DefaultIfEmpty()
                where f.IsActive
                select new
                {
                    f.Id,
                
                    f.StartDate,
                    f.EndDate,
                    f.Country,
                    f.City,
                    f.Venue,
                    f.Cover424x460,
                    Title = tt != null ? tt.KeyName : null
                };

        if (year.HasValue)
        {
            // DateTime.Year -> EF Core translate edebilir (DATEPART)
            q = q.Where(x => x.StartDate.Year == year.Value || x.EndDate.Year == year.Value);
        }
        if (!string.IsNullOrWhiteSpace(country))
        {
            q = q.Where(x => x.Country == country);
        }

        // 5) Tarih aralığı string’i ve diğer metinler SQL’e çevrilmediği için client-side’da projekte et
        var rows = await q
            .OrderBy(x => x.StartDate)
            .ToListAsync(ct);

        var cards = rows.Select(x => new FairCardVM
        {
            Id = x.Id,
            Title = string.IsNullOrWhiteSpace(x.Title) ? "-" : x.Title!,
            CoverUrl = string.IsNullOrWhiteSpace(x.Cover424x460) ? "/img/placeholder-424x460.png" : x.Cover424x460,
            DateRange = $"{x.StartDate:dd MMM yyyy} – {x.EndDate:dd MMM yyyy}",
            Country = x.Country,
            City = x.City,
            Venue = x.Venue
        }).ToList();

        var vm = new FairsFilterVM
        {
            Year = year,
            Country = country,
            Years = years,
            Countries = countries,
            Cards = cards
        };

        return View(vm); // Views/Fair/Index.cshtml
    }
}
