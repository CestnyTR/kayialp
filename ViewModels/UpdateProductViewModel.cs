using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public class UpdateProductViewModel
    {
        public int Id { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required, MinLength(2), MaxLength(160)]
        public string NameTr { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Stock { get; set; } = 0;

        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;

        // Mevcut resimler (CSV’den çözülen yollar)
        public List<string> ExistingImages { get; set; } = new();

        // İşaretlenen mevcut resimler (tam yol) silinecek
        public List<string> RemoveImagePaths { get; set; } = new();

        // Kapak olarak işaretlenen mevcut yol (opsiyonel)
        public string? CoverImagePath { get; set; }

        // Yeni resimler (multiple) – toplam 5 sınırı korunur
        public List<IFormFile> NewImages { get; set; } = new();

        // Detaylar (TR)
        [MaxLength(20000)]
        public string? DescriptionTrHtml { get; set; }

        [MaxLength(20000)]
        public string? AboutTrHtml { get; set; }

        // Seçenekler
        public bool RegenerateOtherLangs { get; set; } = false;
        public bool RegenerateEmptySlugs { get; set; } = true;

        // Özellikler
        public List<FeatureRowVM> Features { get; set; } = new();

        // Seçim listeleri
        public IEnumerable<SelectListItem>? Categories { get; set; }
    }

    public class FeatureRowVM
    {
        public int Id { get; set; } // 0 => yeni
        [Required, MinLength(1), MaxLength(1000)]
        public string TextTr { get; set; } = string.Empty;
        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;
        public bool Delete { get; set; } = false;
    }

    public class ProductTransEditVM
    {
        public int LangId { get; set; }           // DB'deki dil Id
        public string LangCode { get; set; } = ""; // "tr","en","ru","ar"
        public string LangDisplay { get; set; } = ""; // Sekmede göstereceğiz (TR, EN, RU, AR)

        [Required, MinLength(1), MaxLength(160)]
        public string Name { get; set; } = "";    // ProductsTranslations.ValueText

        [MaxLength(300)]
        public string? Slug { get; set; }         // Boş bırakılırsa otomatik üretilecek
    }
}

