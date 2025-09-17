namespace kayialp.ViewModels.Advantages
{
    public class AdvantageItemVM
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = "";   // 313x313 görsel
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string LinkUrl { get; set; } = "#";   // service.html gibi
        public string LinkText { get; set; } = "";   // çeviriyle gelebilir (opsiyonel)
    }

    public class AdvantagesViewModel
    {
        public string SectionBg { get; set; } = "/img/bg/category_bg_1.png";  // temadaki default
        public string SubTitle { get; set; } = "Avantajlarımız";
        public string Title { get; set; } = "Neden Bizi Seçmelisiniz ?";
        public List<AdvantageItemVM> Items { get; set; } = new();
    }
}
