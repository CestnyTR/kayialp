namespace kayialp.ViewModels.Layout
{
    public class FooterViewModel
    {
        // company
        public string? LogoUrl { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }  // tek satır

        // translatable (About)
        public string AboutHtml { get; set; } = "";

        // socials
        public string? FacebookUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? Whatsapp { get; set; }
    }
}
