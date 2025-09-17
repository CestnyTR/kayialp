namespace kayialp.ViewModels.Layout
{
    public class HeaderCompanyVM
    {
        // görseller
        public string? LogoUrl { get; set; }          // /uploads/company/logo.webp
        public string? MobileLogoUrl { get; set; }    // /uploads/company/mobil_logo.webp
        public string? FaviconUrl { get; set; }       // /uploads/company/icon.webp (istersen)

        // iletişim
        public string? Phone { get; set; }            // +90 ...
        public string? Email { get; set; }            // info@...
        public string? AddressLine { get; set; }      // kısa adres satırı
        public string? WorkingHours { get; set; }     // "Pzt-Cum 09:00-18:00"

        // sosyal
        public string? FacebookUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YoutubeUrl { get; set; }
    }
}
