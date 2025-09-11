using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class ProductDetails
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Order { get; set; }

        [ForeignKey("ProductId")]
        public Products Product { get; set; } = new Products();
    }
}
