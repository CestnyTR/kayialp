using System.Collections.Generic;

namespace kayialp.ViewModels.Product
{
    public sealed class ProductDetailsVM
    {
        // temel
        public int ProductId { get; set; }
        public string Title { get; set; } = "";
        public string? Sku { get; set; }
        public string? CategoryName { get; set; }
        public bool InStock { get; set; }

        // görseller
        public string CoverImage { get; set; } = "";           // Kapak
        public List<string> GalleryImages { get; set; } = new(); // Kapaktan sonraki tüm görseller
        public List<string> GalleryAlts { get; set; } = new();   // Tüm görseller için alt yazılar (kapak dahil)

        // sekmeler
        public string? ShortDescHtml { get; set; }   // üst kısım kısa metin – istersen
        public string? DescriptionHtml { get; set; } // "Açıklama" tab
        public string? AboutHtml { get; set; }       // "Ürün Hakkında" tab

        // özellikler
        public List<(string Name, string Value)> Attributes { get; set; } = new();
    }
}
