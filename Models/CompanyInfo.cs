using System.ComponentModel.DataAnnotations;

namespace kayialp.Models
{
    public sealed class CompanyInfo
    {
        public int Id { get; set; } // tek satır: 1

        // Genel
        [MaxLength(200)] public string? Name { get; set; }
        [MaxLength(200)] public string? LegalName { get; set; }
        [MaxLength(50)]  public string? TaxNumber { get; set; }
        [MaxLength(50)]  public string? MersisNo { get; set; }

        // İletişim
        [MaxLength(200)] public string? Email { get; set; }
        [MaxLength(200)] public string? Email2 { get; set; }
        [MaxLength(50)]  public string? Phone { get; set; }
        [MaxLength(50)]  public string? Phone2 { get; set; }
        [MaxLength(50)]  public string? Whatsapp { get; set; }
        [MaxLength(50)]  public string? Fax { get; set; }
        [MaxLength(200)] public string? Website { get; set; }

        // Adres
        [MaxLength(100)] public string? Country { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(100)] public string? District { get; set; }
        [MaxLength(300)] public string? AddressLine { get; set; }
        [MaxLength(20)]  public string? PostalCode { get; set; }
        public string? MapEmbedUrl { get; set; }

        // İçerik
        public string? AboutHtml { get; set; }
        public string? MissionHtml { get; set; }
        public string? VisionHtml { get; set; }
        public string? WorkingHours { get; set; }

        // Medya
        public string? LogoUrl { get; set; }        // /uploads/company/logo.webp
        public string? HeroUrl { get; set; }        // /uploads/company/hero.webp
    }
}
