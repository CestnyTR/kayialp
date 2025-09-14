// ViewModels/LegalVMs.cs
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace kayialp.ViewModels
{
    public sealed class EditLegalLangVM
    {
        public string LangCode { get; set; } = ""; // "tr","en","ru","ar"
        public string? Title { get; set; }
        public string? Html { get; set; }
        public string? Slug { get; set; } // opsiyonel
    }

    public sealed class EditLegalVM
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";    // "privacy" veya "kvkk"
        public bool IsActive { get; set; } = true;
        public bool AutoTranslate { get; set; } = true;

        // TR kısayol alanları (istersen kullanırsın)
        public string? TitleTr { get; set; }
        public string? HtmlTr { get; set; }

        // Sekmeler:
        public List<EditLegalLangVM> Langs { get; set; } = new();
    }

    public sealed class LegalListItemVM
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
        public bool IsActive { get; set; }
        public int Order { get; set; }
        public string TitleTr { get; set; } = "";
    }
}
