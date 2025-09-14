// Models/HomeSlide.cs
namespace kayialp.Models
{
    public sealed class HomeSlide
    {
        public int Id { get; set; }
        public bool IsActive { get; set; } = true;
        public int Order { get; set; } = 0;

        // Görseller (web-relative path)
        public string? Cover1920x900 { get; set; }          // zorunlu
        public string? CoverMobile768x1024 { get; set; }     // opsiyonel

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<HomeSlideTranslation> Translations { get; set; } = new List<HomeSlideTranslation>();
    }

    public sealed class HomeSlideTranslation
    {
        public int Id { get; set; }
        public int HomeSlideId { get; set; }
        public int LangCodeId { get; set; }      // Langs tablosuna FK

        public string? Slogan { get; set; }      // üst kısa metin
        public string? Title { get; set; }       // büyük başlık
        public string? Content { get; set; }     // alt açıklama (düz/HTML)
        // Dil bazlı butonlar
        public string? Cta1Text { get; set; }
        public string? Cta1Url  { get; set; }
        public string? Cta2Text { get; set; }
        public string? Cta2Url  { get; set; }
    }
}
