namespace kayialp.Models.Localization
{

    public class LayoutPage
    {
        public int Id { get; set; }

        // Key -> hangi alan olduğunu belirtiyor (örn. Header.Home, Footer.Contact)
        public string Key { get; set; } = string.Empty;

        // Dil kodu (tr, en, ru, ar)
        public string Culture { get; set; } = string.Empty;

        // Metin içeriği
        public string Value { get; set; } = string.Empty;

    }

}
