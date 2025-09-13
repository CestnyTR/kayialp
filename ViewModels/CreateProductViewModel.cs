using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace kayialp.ViewModels
{
    public class CreateProductViewModel
    {
        [Required]
        public int CategoryId { get; set; }
        [Range(0, int.MaxValue)]
        public int Stock { get; set; }
        public int Order { get; set; }

        // Görseller (min 1, max 5)
        [Required]
        public List<IFormFile> Images { get; set; } = new();

        // Görsel yönetimi için hidden alanlar (JS doldurur)
        public string? ImageOrder { get; set; }   // örn: "0,2,1" (FileList index'leri)
        public int? CoverIndex { get; set; }      // kapak olan FileList index'i

        // Yalnızca TR alanları
        [Required, MinLength(3), MaxLength(120)]
        public string NameTr { get; set; } = "";
        public string? ImageAltsTr { get; set; } = "";

        // Detaylar (TR)
        public string? ShortDescriptionTr { get; set; } = "";
        public string? DescriptionTr { get; set; } = "";
        public string? AboutTr { get; set; } = "";

        // Özellikler (TR)
        public List<ProductAttributeRow> Attributes { get; set; } = new();
    }

    public class ProductAttributeRow
    {
        [MaxLength(120)]
        public string? NameTr { get; set; }   // Etiket
        [MaxLength(400)]
        public string? ValueTr { get; set; }  // Değer
        public int Order { get; set; } = 0;
    }

}

