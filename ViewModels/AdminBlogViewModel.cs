using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace kayialp.ViewModels
{
    // --- Liste ---
    public class AdminBlogCategoryVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class AdminBlogListItemVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string CategoryName { get; set; } = "-";
        public int Order { get; set; }
        public bool IsActive { get; set; }
        public string? CoverUrl { get; set; }
        public string Slug { get; set; } = "";
    }

    public class AdminBlogIndexVM
    {
        public List<AdminBlogCategoryVM> Categories { get; set; } = new();
        public List<AdminBlogListItemVM> Posts { get; set; } = new();
    }

    // --- Create/Update ortak dil alanı ---
    public class BlogLangVM
    {
        public string LangCode { get; set; } = "";  // "tr","en","ru","ar"
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? ImageAltCover { get; set; }
        public string? ImageAltInner { get; set; }
        public string? SummaryHtml { get; set; }
        public string? BodyHtml { get; set; }
    }

    // *** DIKKAT: sealed KALDIRILDI ***
    public class CreatePostVM
    {
        public int? CategoryId { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;

        // Görseller
        public IFormFile? CoverImage { get; set; } // 312x240
        public IFormFile? InnerImage { get; set; } // 856x460

        // Çeviri davranışı
        public bool AutoTranslate { get; set; } = true;

        // TR kısayollar (isteğe bağlı)
        public string? TitleTr { get; set; }
        public string? SummaryTr { get; set; }
        public string? BodyTr { get; set; }
        public string? AltCoverTr { get; set; }
        public string? AltInnerTr { get; set; }

        // Sekmeler
        public List<BlogLangVM> Langs { get; set; } = new();
    }

    public class UpdatePostVM : CreatePostVM
    {
        public int Id { get; set; }

        // Mevcut görsel yolları
        public string? ExistingCover { get; set; }
        public string? ExistingInner { get; set; }

        // Sil bayrakları
        public bool RemoveCover { get; set; }
        public bool RemoveInner { get; set; }
    }
}
