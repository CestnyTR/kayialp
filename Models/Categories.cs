using System.ComponentModel.DataAnnotations;

namespace kayialp.Models
{
    public class Categories
    {
        public int Id { get; set; }
        public int Order { get; set; }
        // --- NEW: Category images ---
        [MaxLength(500)]
        public string? ImageCard312x240 { get; set; }        // /uploads/categories/{id}/card-312x240.webp

        [MaxLength(500)]
        public string? ImageShowcase423x636 { get; set; }    // /uploads/categories/{id}/showcase-423x636.webp

    }
}
