// Models/LegalPage.cs
namespace kayialp.Models
{
    public sealed class LegalPage
    {
        public int Id { get; set; }
        // "privacy" veya "kvkk" sabit anahtar
        public string Key { get; set; } = ""; // unique index önerilir
        public bool IsActive { get; set; } = true;
        public int Order { get; set; } = 0; // ilerde sıralama gerekirse
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<LegalPageTranslation> Translations { get; set; } = new List<LegalPageTranslation>();
    }

    public sealed class LegalPageTranslation
    {
        public int Id { get; set; }
        public int LegalPageId { get; set; }
        public int LangCodeId { get; set; }      // Langs tablosuna FK
        public string Title { get; set; } = "";  // sayfa başlığı
        public string Html { get; set; } = "";   // sayfa içeriği (HTML)
        // İstersen slug da ekleyebilirsin:
        public string? Slug { get; set; }
    }
}
