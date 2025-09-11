using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public class CreateCategoryViewModel
    {
        [Required, MinLength(3), MaxLength(80)]
        public string NameTr { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Order { get; set; } = 0;
    }
}
