using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class ProductDetailsTranslations
    {
        public int Id { get; set; }
        public int ProductDetailsId { get; set; }
        public int LangCodeId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;


        [ForeignKey("ProductDetailsId")]
        public ProductDetails productDetail { get; set; } //= new ProductDetails();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } //= new Langs();
    }
}
