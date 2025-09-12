using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class ProductDetailsFeatureTranslations
    {
        public int Id { get; set; }
        public int ProductDetailsFeatureId { get; set; }
        public int LangCodeId { get; set; }
        public string ValueText { get; set; } = string.Empty;

        [ForeignKey("ProductDetailsFeatureId")]
        public ProductDetailsFeatures productDetailsFeature { get; set; } //= new ProductDetailsFeatures();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } //= new Langs();
    }
}
