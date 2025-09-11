using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class CategoriesTranslations
    {
        public int Id { get; set; }
        public int CategoriesId { get; set; }
        public int LangCodeId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        [ForeignKey("CategoriesId")]
        public Categories category { get; set; } //= new Categories();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } //= new Langs();
    }
}
