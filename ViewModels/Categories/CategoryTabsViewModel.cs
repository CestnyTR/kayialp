namespace kayialp.ViewModels.Categories
{
    public class CategoryTabsItemVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string LinkUrl { get; set; } = "#";
        public string SubTitle { get; set; } = "Kayıalp Makineleri"; // kart alt başlık
    }

    public class CategoryTabVM
    {
        public string TabId { get; set; } = "";   // nav-step1, nav-step2...
        public string TabTitle { get; set; } = ""; // sekme başlığı
        public List<CategoryTabsItemVM> Items { get; set; } = new();
    }

    public class CategoryTabsViewModel
    {
        // Bölüm üst başlıkları
        public string SectionSubTitle { get; set; } = "Kayıalp Makineleri";
        public string SectionTitle { get; set; } = "İleri Teknoloji ve Mühendislik: Kayıalp Makineleri";
        // Sekmeler
        public List<CategoryTabVM> Tabs { get; set; } = new();
    }
}
