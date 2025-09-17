using System.Globalization;
using System.Text.RegularExpressions;
using kayialp.Context;
using kayialp.Services;
using kayialp.ViewModels.Blog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kayialp.ViewComponents
{
    public class BlogShowcaseViewComponent : ViewComponent
    {
        private readonly kayialpDbContext _context;
        private readonly ContentService _content;

        public BlogShowcaseViewComponent(kayialpDbContext context, ContentService content)
        {
            _context = context;
            _content = content;
        }

        private int? GetLangId(string code) =>
            _context.Langs.FirstOrDefault(l => l.LangCode == code)?.Id;

        private static string StripHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            var text = Regex.Replace(html, "<.*?>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }

        private static string Truncate(string text, int max = 220) =>
            string.IsNullOrWhiteSpace(text) ? string.Empty :
            (text.Length <= max ? text : text.Substring(0, max).TrimEnd() + "…");

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var langId  = GetLangId(culture) ?? GetLangId("en");

            var vm = new BlogShowcaseViewModel
            {
                SubTitle  = _content.GetText("pages.home.blog_subtitle")  ?? "Neler sunuyoruz.",
                Title     = _content.GetText("pages.home.blog_title")     ?? "Tahıl ve Bakliyat Sektörüne Profesyonel Çözümler",
                Paragraph = _content.GetText("pages.home.blog_paragraph") ??
                            "Kayıalp Makine, tahıl işleme tesislerinin tüm ihtiyaçlarına yönelik, operasyonları kolaylaştıran ve verimliliği artıran geniş bir ürün yelpazesi sunar.",
                ReadMore  = _content.GetText("pages.home.blog_read_more") ?? "Daha Fazla Oku"
            };

            // 1) Son 6 aktif yazı
            var posts = await _context.BlogPosts
                .Where(p => p.IsActive)
                .OrderBy(p => p.Order).ThenByDescending(p => p.Id)
                .Take(6).ToListAsync();

            if (!posts.Any())
                return View("/Views/Shared/Components/BlogShowcase/Default.cshtml", vm);

            var postIds = posts.Select(p => p.Id).ToList();

            // 2) Çeviriler
            var trs = await _context.BlogPostsTranslations
                .Where(t => postIds.Contains(t.PostId) && t.LangCodeId == langId)
                .ToListAsync();

            // 3) Contents & Blocks
            var contents = await _context.BlogPostContents
                .Where(c => postIds.Contains(c.PostId) && c.IsActive)
                .ToListAsync();

            var contentIds = contents.Select(c => c.Id).ToList();

            var blocks = await _context.BlogPostContentBlocks
                .Where(b => contentIds.Contains(b.PostContentId) && b.BlockType == "summary" && b.IsActive)
                .ToListAsync();

            var blockIds = blocks.Select(b => b.Id).ToList();

            var blockTrs = await _context.BlogPostContentBlockTranslations
                .Where(t => blockIds.Contains(t.BlockId) && t.LangCodeId == langId)
                .ToListAsync();

            foreach (var p in posts)
            {
                var tr = trs.FirstOrDefault(t => t.PostId == p.Id);
                var content = contents.FirstOrDefault(c => c.PostId == p.Id);
                var block = blocks.FirstOrDefault(b => b.PostContentId == content?.Id);
                var summary = block != null
                    ? blockTrs.FirstOrDefault(bt => bt.BlockId == block.Id)?.Html
                    : "";

                vm.Items.Add(new BlogCardVM
                {
                    Id       = p.Id,
                    ImageUrl = string.IsNullOrWhiteSpace(p.Cover312x240) ? "/img/service/service_img_1.jpg" : p.Cover312x240!,
                    ImageAlt = tr?.ImageAltCover ?? "blog",
                    Title    = tr?.ValueTitle ?? "",
                    Excerpt  = Truncate(StripHtml(summary), 220),
                    LinkUrl  = !string.IsNullOrWhiteSpace(tr?.Slug)
                        ? $"/{culture}/blog/{tr.Slug}"
                        : $"/{culture}/blog/{p.Id}"
                });
            }

            return View("/Views/Shared/Components/BlogShowcase/Default.cshtml", vm);
        }
    }
}
