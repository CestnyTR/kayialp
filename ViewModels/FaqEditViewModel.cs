namespace kayialp.ViewModels
{
    public class FaqEditViewModel
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public bool AutoTranslate { get; set; } = true; // Varsayılan: işaretli

        public List<FaqTranslationVM> Translations { get; set; } = new();
    }

    public class FaqTranslationVM
    {
        public int LangCodeId { get; set; }
        public string LangCode { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
    }
    public class FaqOrderVM
    {
        public int Id { get; set; }
        public int Order { get; set; }
    }


}
