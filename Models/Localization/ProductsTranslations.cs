using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class ProductsTranslations
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int LangCodeId { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;


        [ForeignKey("ProductId")]
        public Products product { get; set; } = new Products();
        [ForeignKey("LangCodeId")]
        public Langs LangCode { get; set; } = new Langs();
    }
}
