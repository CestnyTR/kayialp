// Models/AdvantageTranslation.cs
using System.ComponentModel.DataAnnotations;
using kayialp.Models;
namespace kayialp.Models
{
    public class AdvantageTranslation
    {
        public int Id { get; set; }
        public int AdvantageId { get; set; }
        public Advantage Advantage { get; set; } = null!;

        public int LangCodeId { get; set; }
        public Langs Lang { get; set; } = null!;

        [Required]
        public string Title { get; set; } = "";

        [Required]
        public string Content { get; set; } = "";
    }
}
