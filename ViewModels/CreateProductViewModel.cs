using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace kayialp.ViewModels
{
    public class CreateProductViewModel
    {
        [Required]
        public int CategoryId { get; set; }

        [Required, MinLength(2), MaxLength(160)]
        public string NameTr { get; set; } = string.Empty;

        // Virgül ile çoklu görsel (max 5)
        [MaxLength(2000)]
        public string? ImageUrlsCsv { get; set; }

        [Range(0, int.MaxValue)]
        public int Stock { get; set; } = 0;

        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;

        // Dropdown için
        public IEnumerable<SelectListItem>? Categories { get; set; }
    }
}
