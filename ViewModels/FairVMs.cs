// ViewModels/FairVMs.cs
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace kayialp.ViewModels
{
    public sealed class FairLangVM
    {
        public string LangCode { get; set; } = ""; // "tr","en","ru","ar"
        public string? Title { get; set; }
        public string? Slug { get; set; }
    }

    // NOT: sealed KALDIRILDI -> UpdateFairVM miras alabiliyor
    public class CreateFairVM
    {
        // temel
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Venue { get; set; }
        public string? WebsiteUrl { get; set; }
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // kapak
        public IFormFile? Cover { get; set; }

        // AutoTranslate: boşlara TR’den
        public bool AutoTranslate { get; set; } = true;

        // TR kısa yol
        public string? TitleTr { get; set; }

        public List<FairLangVM> Langs { get; set; } = new();
    }

    public sealed class UpdateFairVM : CreateFairVM
    {
        public int Id { get; set; }
        public string? ExistingCover { get; set; } // gösterim için
    }

    public sealed class FairCardVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? CoverUrl { get; set; }
        public string DateRange { get; set; } = "";
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Venue { get; set; }
    }

    public sealed class FairsFilterVM
    {
        public int? Year { get; set; }
        public string? Country { get; set; }
        public List<int> Years { get; set; } = new();
        public List<string> Countries { get; set; } = new();
        public List<FairCardVM> Cards { get; set; } = new();
    }

    // quick add (admin modal)
    public sealed class QuickFairReq
    {
        public string? TitleTr { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Venue { get; set; }
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}
