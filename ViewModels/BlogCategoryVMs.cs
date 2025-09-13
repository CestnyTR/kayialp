// ViewModels/BlogCategoryVMs.cs
using System.Collections.Generic;

namespace kayialp.ViewModels
{
    public sealed class BlogCategoryListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = "-"; // TR adı
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class UpdateBlogCategoryLangVM
    {
        public string LangCode { get; set; } = ""; // "tr","en","ru","ar"
        public string? Name { get; set; }
        public string? Slug { get; set; }
    }

    public sealed class UpdateBlogCategoryVM
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
        public bool AutoTranslate { get; set; } = true; // boş alanlara TR’den

        // TR kısa yol (opsiyonel)
        public string? NameTr { get; set; }

        public List<UpdateBlogCategoryLangVM> Langs { get; set; } = new();
    }
}
