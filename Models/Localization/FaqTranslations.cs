using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class FaqTranslations
    {
        public int Id { get; set; }
        public int FaqId { get; set; }
        public int LangCodeId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        [ForeignKey("FaqId")]
        public Faqs faq { get; set; } //= new Faqs();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } //= new Langs();
    }
}
