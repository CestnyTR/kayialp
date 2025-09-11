using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class SliderTranslations
    {
        public int Id { get; set; }
        public int SliderId { get; set; }
        public int LangCodeId { get; set; }
        public string Slogan { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ShortContent { get; set; } = string.Empty;


        [ForeignKey("SliderId")]
        public Sliders slider { get; set; } = new Sliders();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } = new Langs();
    }
}
