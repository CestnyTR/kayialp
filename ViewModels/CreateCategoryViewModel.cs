using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public class CreateCategoryViewModel
    {
        [Required, MinLength(3), MaxLength(80)]
        public string NameTr { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;


        [Display(Name = "Kart Görseli (312x240)")]
        public IFormFile? CardImage { get; set; }

        [Display(Name = "Vitrin Görseli (423x636)")]
        public IFormFile? ShowcaseImage { get; set; }
    }
}
