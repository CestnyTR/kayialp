// ViewModels/SlideVMs.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public sealed class EditSlideLangVM
    {
        public string LangCode { get; set; } = ""; // tr,en,ru,ar
        public string? Slogan { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Cta1Text { get; set; }
        public string? Cta1Url  { get; set; }
        public string? Cta2Text { get; set; }
        public string? Cta2Url  { get; set; }
    }

    public sealed class EditSlideVM
    {
        public int? Id { get; set; } // null -> create, not null -> update
        public bool IsActive { get; set; } = true;
        public int Order { get; set; } = 0;
        public bool AutoTranslate { get; set; } = true;

        // Görsel yükleme
        [Display(Name = "Kapak (1920x900)")]
        public IFormFile? Cover { get; set; } // create'de zorunlu
        [Display(Name = "Mobil Kapak (768x1024) — opsiyonel")]
        public IFormFile? CoverMobile { get; set; }

        // TR kısayol alanları (istersen kullan)
        public string? SloganTr { get; set; }
        public string? TitleTr  { get; set; }
        public string? ContentTr{ get; set; }
        public string? Cta1TextTr { get; set; }
        public string? Cta1UrlTr  { get; set; }
        public string? Cta2TextTr { get; set; }
        public string? Cta2UrlTr  { get; set; }

        // Sekmeler
        public List<EditSlideLangVM> Langs { get; set; } = new();
        // Mevcut görsel önizleme
        public string? ExistingCover { get; set; }
        public string? ExistingCoverMobile { get; set; }
    }

    public sealed class SlideListItemVM
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
        public int Order { get; set; }
        public string? Cover { get; set; }
        public string TitleTr { get; set; } = "";
    }

    public sealed class ReorderIdsReq
    {
        public List<int> Ids { get; set; } = new();
    }
}
