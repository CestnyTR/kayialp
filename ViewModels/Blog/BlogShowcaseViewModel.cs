namespace kayialp.ViewModels.Blog
{
    public class BlogCardVM
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = "";
        public string ImageAlt { get; set; } = "blog";
        public string Title { get; set; } = "";
        public string Excerpt { get; set; } = ""; // summary
        public string LinkUrl { get; set; } = "#";
    }

    public class BlogShowcaseViewModel
    {
        public string SubTitle { get; set; } = "Neler sunuyoruz.";
        public string Title { get; set; } = "Tahıl ve Bakliyat Sektörüne Profesyonel Çözümler";
        public string Paragraph { get; set; } =
            "Kayıalp Makine, tahıl işleme tesislerinin tüm ihtiyaçlarına yönelik, operasyonları kolaylaştıran ve verimliliği artıran geniş bir ürün yelpazesi sunar.";
        public string ReadMore { get; set; } = "Daha Fazla Oku";
        public List<BlogCardVM> Items { get; set; } = new();
    }
}
