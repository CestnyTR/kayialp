namespace kayialp.ViewModels
{
    public class AdminIndexViewModel
    {
        public List<CategoryViewModel> Categories { get; set; } = new();
        public List<ProductListItemViewModel> Products { get; set; } = new();
    }

    public class ProductListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "-";           // TR ürün adı
        public string CategoryName { get; set; } = "-";   // TR kategori adı
        public int Stock { get; set; }
        public int Order { get; set; }
        public string FirstImageUrl { get; set; } = "";   // küçük önizleme
        public int ImageCount { get; set; }               // +N rozeti
        public string Slug { get; set; } = "";            // TR slug
    }
    public class CategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
