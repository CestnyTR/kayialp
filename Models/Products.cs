using System.ComponentModel.DataAnnotations.Schema;

namespace kayialp.Models
{
    public class Products
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int Stock { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int Order { get; set; }

        [ForeignKey("CategoryId")]
        public Categories Category { get; set; } //= new Categories();
    }
}
