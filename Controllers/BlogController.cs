using kayialp.Context;
using kayialp.Models;
using kayialp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class BlogController : Controller
{
    private readonly kayialpDbContext _context;

    public BlogController(kayialpDbContext ctx)
    {
        _context = ctx;
    }

    [HttpGet("{culture}/blog")]
    public async Task<IActionResult> Index(string culture, CancellationToken ct)
    {
        // aktif dil Id’si
        var langId = await _context.Langs
            .Where(l => l.LangCode == culture)
            .Select(l => l.Id)
            .FirstOrDefaultAsync(ct);

        if (langId == 0)
        {
            // fallback TR
            langId = await _context.Langs
                .Where(l => l.LangCode == "tr")
                .Select(l => l.Id)
                .FirstAsync(ct);
        }

        var blogs = await _context.BlogPosts
            .Where(b => b.IsActive)
            .Join(_context.BlogPostsTranslations,
                  b => b.Id,
                  t => t.PostId,
                  (b, t) => new { b, t })
            .Where(x => x.t.LangCodeId == langId)
            .OrderByDescending(x => x.b.Id) // Id’ye göre sıralama (yayın tarihi alanı yok)
            .Select(x => new BlogListItemViewModel
            {
                Id = x.b.Id,
                Title = x.t.ValueTitle,
                Slug = x.t.Slug,
                ImageUrl = x.b.Cover312x240,
                ImageAlt = x.t.ImageAltCover, // alt text
                                              // summary bloklarını al
                Summary = (from c in _context.BlogPostContents
                           join cb in _context.BlogPostContentBlocks on c.Id equals cb.PostContentId
                           join cbt in _context.BlogPostContentBlockTranslations on cb.Id equals cbt.BlockId
                           where c.PostId == x.b.Id
                                 && cb.BlockType == "summary"
                                 && cb.IsActive
                                 && cbt.LangCodeId == langId
                           select cbt.Html).FirstOrDefault()
            })
        .ToListAsync(ct);
        return View(blogs);
    }
    [HttpGet("{culture}/blog/{id:int}/{slug}")]
    public async Task<IActionResult> Detail(string culture, int id, string slug, CancellationToken ct)
    {
        var langId = await _context.Langs
            .Where(l => l.LangCode == culture)
            .Select(l => l.Id)
            .FirstOrDefaultAsync(ct);

        if (langId == 0)
        {
            langId = await _context.Langs
                .Where(l => l.LangCode == "tr")
                .Select(l => l.Id)
                .FirstAsync(ct);
        }

        var blog = await _context.BlogPosts
            .Where(b => b.Id == id && b.IsActive)
            .Join(_context.BlogPostsTranslations,
                  b => b.Id,
                  t => t.PostId,
                  (b, t) => new { b, t })
            .Where(x => x.t.LangCodeId == langId)
            .Select(x => new BlogDetailViewModel
            {
                Id = x.b.Id,
                Title = x.t.ValueTitle,
                Slug = x.t.Slug,
                ImageUrl = x.b.Inner856x460,        // asıl resim yolu
                ImageAlt = x.t.ImageAltCover, // alt text

                Summary = (from c in _context.BlogPostContents
                           join cb in _context.BlogPostContentBlocks on c.Id equals cb.PostContentId
                           join cbt in _context.BlogPostContentBlockTranslations on cb.Id equals cbt.BlockId
                           where c.PostId == x.b.Id
                                 && cb.BlockType == "summary"
                                 && cb.IsActive
                                 && cbt.LangCodeId == langId
                           select cbt.Html).FirstOrDefault(),

                Body = (from c in _context.BlogPostContents
                        join cb in _context.BlogPostContentBlocks on c.Id equals cb.PostContentId
                        join cbt in _context.BlogPostContentBlockTranslations on cb.Id equals cbt.BlockId
                        where c.PostId == x.b.Id
                              && cb.BlockType == "body"
                              && cb.IsActive
                              && cbt.LangCodeId == langId
                        select cbt.Html).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (blog == null)
            return NotFound();

        return View(blog);
    }

}
