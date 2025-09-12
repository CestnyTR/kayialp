using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public class CreateProductFullViewModel
    {
        [Required]
        public int CategoryId { get; set; }

        [Required, MinLength(2), MaxLength(160)]
        public string NameTr { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Stock { get; set; } = 0;

        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;

        // Çoklu dosya (max 5)
        [Required]
        public List<IFormFile> Images { get; set; } = new();

        // YENİ: iki sabit CKEditor alanı
        [MaxLength(20000)]
        public string? DescriptionTrHtml { get; set; }  // Açıklama

        [MaxLength(20000)]
        public string? AboutTrHtml { get; set; }        // Ürün Hakkında

        // YENİ: detaya bağlı olmayan feature’lar
        public List<ProductFeatureInput> Features { get; set; } = new();

        public IEnumerable<SelectListItem>? Categories { get; set; }
    }

    public class ProductFeatureInput
    {
        [Required, MinLength(1), MaxLength(1000)]
        public string TextTr { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;
    }

}
