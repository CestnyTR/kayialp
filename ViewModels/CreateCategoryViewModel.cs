using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public class CreateCategoryViewModel
    {
        [Required(ErrorMessage = "Kategori adı zorunludur")]
        [StringLength(200, ErrorMessage = "Kategori adı 200 karakteri geçmemelidir.")]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "Sıra 0 veya daha büyük olmalıdır")]
        public int Order { get; set; }

        [Required]
        public int LangCodeId { get; set; } = 2;
    }
}
