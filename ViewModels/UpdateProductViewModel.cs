// using Microsoft.AspNetCore.Http;
// using System.Collections.Generic;

namespace kayialp.ViewModels
{
    public sealed class LangAttributeEditVM
    {
        public int AttributeId { get; set; }  // ProductAttributes.Id
        public int Order { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    public sealed class UpdateProductLangVM
    {
        public string LangCode { get; set; } = "";   // "tr","en","ru","ar"
        public string? Name { get; set; }
        public string? Slug { get; set; }            // boşsa addan türet
        public string? ImageAltsCsv { get; set; }    // boşsa otomatik üret/çevir
        public string? ShortHtml { get; set; }
        public string? DescHtml { get; set; }
        public string? AboutHtml { get; set; }

        // Bu dil için özellik çevirileri (edit edilebilir)
        public List<LangAttributeEditVM> Attrs { get; set; } = new();
    }

    // TR için satır ekleme/silme/güncelleme
    public sealed class UpdateProductAttributeRow
    {
        public int? Id { get; set; }         // mevcut satır için
        public int Order { get; set; }
        public string? NameTr { get; set; }  // TR kaynak ad
        public string? ValueTr { get; set; } // TR kaynak değer
        public bool? Delete { get; set; }    // sert sil
    }

    public sealed class UpdateProductViewModel
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int Stock { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }

        // Görseller
        public List<IFormFile>? NewImages { get; set; }
        public List<string>? RemoveImages { get; set; }   // "cover.webp","gallery-1.webp" vb.
        public string? ImageOrder { get; set; }           // dosya adları CSV
        public int? CoverIndex { get; set; }              // 0-based

        // Çeviri davranışı
        public bool AutoTranslate { get; set; } = true;

        // TR quick edit (istenirse formda kullanmak için)
        public string? NameTr { get; set; }
        public string? ShortDescriptionTr { get; set; }
        public string? DescriptionTr { get; set; }
        public string? AboutTr { get; set; }
        public string? ImageAltsTr { get; set; }

        // Sekmeler
        public List<UpdateProductLangVM> Langs { get; set; } = new();

        // TR’de satır ekleme/silme için
        public List<UpdateProductAttributeRow>? Attributes { get; set; }

        // Özellik global sırası (AttributeId CSV)
        public string? AttributeOrder { get; set; }
    }
}
