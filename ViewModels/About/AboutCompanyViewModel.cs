namespace kayialp.ViewModels.About
{
    public class AboutCompanyViewModel
    {
        // Üst başlık alanı
        public string SubTitle { get; set; } = "Şirketimiz Hakkında";
        public string Title { get; set; } = "İşletmenizle Birlikte Gelişen Çözümler";

        // Ana paragraf (HTML destekli)
        public string AboutHtml { get; set; } = "";

        // İki adet “about-item” bloğu: misyon & vizyon (HTML destekli)
        public string MissionTitle { get; set; } = "Misyonumuz";
        public string MissionHtml { get; set; } = "";

        public string VisionTitle { get; set; } = "Vizyonumuz";
        public string VisionHtml { get; set; } = "";

        // Buton
        public string ButtonText { get; set; } = "Daha Fazla Öğren";
        public string ButtonUrl { get; set; } = "about.html";
    }
}
