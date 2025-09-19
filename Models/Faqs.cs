namespace kayialp.Models
{
    public class Faqs
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public ICollection<FaqTranslations> Translations { get; set; }
    }
}
