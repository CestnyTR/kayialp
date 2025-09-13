using System.ComponentModel.DataAnnotations;

namespace kayialp.Models
{
    public class BlogPosts
    {
        public int Id { get; set; }
        public int? CategoryId { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }

        // Dosya yolları (webp)
        public string? Cover312x240 { get; set; }
        public string? Inner856x460 { get; set; }
    }

    public class BlogPostsTranslations
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int LangCodeId { get; set; }
        public string KeyName { get; set; } = "";
        public string ValueTitle { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? ImageAltCover { get; set; }
        public string? ImageAltInner { get; set; }
    }

    public class BlogPostContent
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }

    public class BlogPostContentBlock
    {
        public int Id { get; set; }
        public int PostContentId { get; set; }
        public string BlockType { get; set; } = ""; // "summary","body"
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }

    public class BlogPostContentBlockTranslation
    {
        public int Id { get; set; }
        public int BlockId { get; set; }
        public int LangCodeId { get; set; }
        public string Html { get; set; } = "";
    }

    public class BlogCategories
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }

    public class BlogCategoriesTranslations
    {
        public int Id { get; set; }
        public int BlogCategoryId { get; set; }
        public int LangCodeId { get; set; }
        public string KeyName { get; set; } = "";
        public string ValueText { get; set; } = "";
        public string Slug { get; set; } = "";
    }
}
