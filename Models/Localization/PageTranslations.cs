using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class PageTranslations
    {
        public int Id { get; set; }
        public int PageId { get; set; }
        public int LangCodeId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        [ForeignKey("PageId")]
        public Pages page { get; set; } //= new Pages();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } //= new Langs();
    }
}
