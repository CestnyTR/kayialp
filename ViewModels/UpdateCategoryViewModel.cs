using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public class UpdateCategoryViewModel
    {
        [Required]
        public int Id { get; set; }

        [Range(0, int.MaxValue)]
        public int Order { get; set; }

        // EN camelCase (gösterelim; istersen readonly yaparız)
        public string KeyName { get; set; } = string.Empty;

        // Kaydettiğimde EN’den otomatik yeniden üret
        public bool RegenerateKeyNameFromEnglish { get; set; } = true;

        // Slug üretim kuralı: Boş bıraktıklarımı otomatik üret
        public bool RegenerateEmptySlugs { get; set; } = true;

        // --- NEW: existing paths for preview ---
        public string? ExistingCardImage { get; set; }
        public string? ExistingShowcaseImage { get; set; }

        // --- NEW: replace or remove ---
        public IFormFile? NewCardImage { get; set; }
        public IFormFile? NewShowcaseImage { get; set; }
        public bool RemoveCardImage { get; set; }
        public bool RemoveShowcaseImage { get; set; }
        public bool GenerateFromTurkish { get; set; }

        // Sekmeler
        public List<CategoryTranslationEdit> Translations { get; set; } = new();
    }

    public class CategoryTranslationEdit
    {
        public int LangId { get; set; }
        public string LangCode { get; set; } = "";    // "tr", "en", "ru", "ar" (veya en-GB...)
        public string LangDisplay { get; set; } = ""; // etikette göstermek için (örn. "TR", "EN")

        [Display(Name = "Ad")]
        [Required, MinLength(1), MaxLength(160)]
        public string ValueText { get; set; } = "";

        [Display(Name = "Slug")]
        [MaxLength(200)]
        public string? Slug { get; set; } = "";



    }
}
