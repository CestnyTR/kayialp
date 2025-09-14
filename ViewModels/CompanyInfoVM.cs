using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public sealed class CompanyInfoVM
    {
        public int Id { get; set; }

        // Genel
        public string? Name { get; set; }
        public string? LegalName { get; set; }
        public string? TaxNumber { get; set; }
        public string? MersisNo { get; set; }

        // İletişim
        [EmailAddress] public string? Email { get; set; }
        [EmailAddress] public string? Email2 { get; set; }
        public string? Phone { get; set; }
        public string? Phone2 { get; set; }
        public string? Whatsapp { get; set; }
        public string? Fax { get; set; }
        public string? Website { get; set; }

        // Adres
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? AddressLine { get; set; }
        public string? PostalCode { get; set; }
        public string? MapEmbedUrl { get; set; }

        // İçerik
        public string? AboutHtml { get; set; }
        public string? MissionHtml { get; set; }
        public string? VisionHtml { get; set; }
        public string? WorkingHours { get; set; }

        // Medya
        public string? ExistingLogo { get; set; }
        public string? ExistingHero { get; set; }
        public IFormFile? Logo { get; set; } // 400x400 öneri
        public IFormFile? Hero { get; set; } // 1920x600 öneri
    }
}
