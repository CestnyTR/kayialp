namespace kayialp.ViewModels.Slider
{
    public class SliderItemVM
    {
        public int Id { get; set; }
        public string DesktopImageUrl { get; set; } = "";
        public string MobileImageUrl { get; set; } = "";

        public string Slogan { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";

        public string Cta1Text { get; set; } = "";
        public string Cta1Url { get; set; } = "#";

        public string Cta2Text { get; set; } = "";
        public string Cta2Url { get; set; } = "#";
    }

    public class SliderViewModel
    {
        public List<SliderItemVM> Items { get; set; } = new();
    }
}
