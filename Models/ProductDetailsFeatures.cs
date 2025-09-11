using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class ProductDetailsFeatures
    {
        public int Id { get; set; }
        public int ProductDetailsId { get; set; }
        public int Order { get; set; }
        
        [ForeignKey("ProductDetailsId")]
        public ProductDetails ProductDetails { get; set; } = new ProductDetails();
    }
}
