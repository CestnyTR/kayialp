// ViewModels/CreateBlogCategoryVM.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public sealed class BlogCategoryLangVM
    {
        public string LangCode { get; set; } = ""; // "tr","en","ru","ar"
        public string? Name { get; set; }
        public string? Slug { get; set; }
    }

    public sealed class CreateBlogCategoryVM
    {
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // TR kısayollar (opsiyonel): doldurursan TR sekmesine yansır
        public string? NameTr { get; set; }

        // AutoTranslate: yalnızca BOŞ alanlar TR’den doldurulur
        public bool AutoTranslate { get; set; } = true;

        public List<BlogCategoryLangVM> Langs { get; set; } = new();
    }
}
