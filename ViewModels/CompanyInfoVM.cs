using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public sealed class CompanyInfoVM
    {
        // tekil alanlar
        public string? Name { get; set; }
        public string? LegalName { get; set; }
        public string? TaxNumber { get; set; }
        public string? MersisNo { get; set; }
        public string? Email { get; set; }
        public string? Email2 { get; set; }
        public string? Phone { get; set; }
        public string? Phone2 { get; set; }
        public string? Whatsapp { get; set; }
        public string? Fax { get; set; }
        public string? Website { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? AddressLine { get; set; }
        public string? PostalCode { get; set; }
        public string? MapEmbedUrl { get; set; }
        public string? WorkingHours { get; set; }

        public string? FacebookUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? HeroUrl { get; set; }

        public IFormFile? Logo { get; set; }
        public IFormFile? Hero { get; set; }

        public List<CompanyInfoLangVM> Langs { get; set; } = new();
    }

    public sealed class CompanyInfoLangVM
    {
        public string LangCode { get; set; } = "";
        public string? AboutHtml { get; set; }
        public string? MissionHtml { get; set; }
        public string? VisionHtml { get; set; }
    }

}
