using System.ComponentModel.DataAnnotations;

namespace kayialp.Models
{
    public sealed class CompanyInfo
    {
        public int Id { get; set; }

        [MaxLength(200)] public string? Name { get; set; }
        [MaxLength(200)] public string? LegalName { get; set; }
        [MaxLength(50)] public string? TaxNumber { get; set; }
        [MaxLength(50)] public string? MersisNo { get; set; }
        [MaxLength(200)] public string? Email { get; set; }
        [MaxLength(200)] public string? Email2 { get; set; }
        [MaxLength(50)] public string? Phone { get; set; }
        [MaxLength(50)] public string? Phone2 { get; set; }
        [MaxLength(50)] public string? Whatsapp { get; set; }
        [MaxLength(50)] public string? Fax { get; set; }
        [MaxLength(200)] public string? Website { get; set; }

        [MaxLength(100)] public string? Country { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(100)] public string? District { get; set; }
        [MaxLength(300)] public string? AddressLine { get; set; }
        [MaxLength(20)] public string? PostalCode { get; set; }
        public string? MapEmbedUrl { get; set; }

        [MaxLength(300)] public string? LogoUrl { get; set; }
        [MaxLength(300)] public string? MobilLogoUrl { get; set; }
        [MaxLength(300)] public string? IconUrl { get; set; }

        [MaxLength(300)] public string? FacebookUrl { get; set; }
        [MaxLength(300)] public string? TwitterUrl { get; set; }
        [MaxLength(300)] public string? InstagramUrl { get; set; }
        [MaxLength(300)] public string? LinkedInUrl { get; set; }
        [MaxLength(300)] public string? YoutubeUrl { get; set; }

        public string? WorkingHours { get; set; }

        public ICollection<CompanyInfoTranslation> Translations { get; set; } = new List<CompanyInfoTranslation>();
    }

}
