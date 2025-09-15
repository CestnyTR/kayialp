// Models/Advantage.cs
using System.ComponentModel.DataAnnotations;
using kayialp.Models;
namespace kayialp.Models
{
    public class Advantage
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }

        [MaxLength(300)]
        public string? Image313Url { get; set; }

        public ICollection<AdvantageTranslation> Translations { get; set; } = new List<AdvantageTranslation>();
    }
}