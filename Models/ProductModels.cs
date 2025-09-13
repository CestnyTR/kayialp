using System.ComponentModel.DataAnnotations;
using kayialp.Models;

namespace ProductModels
{
    // --- Content (blok tabanlı) ---
    public class ProductContent
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Products Product { get; set; } = default!;
        public int Order { get; set; } = 0; // gelecekte çoklu “içerik seti” için
        public bool IsActive { get; set; } = true;

        public ICollection<ProductContentBlock> Blocks { get; set; } = new List<ProductContentBlock>();
    }

    public class ProductContentBlock
    {
        public int Id { get; set; }
        public int ProductContentId { get; set; }
        public ProductContent ProductContent { get; set; } = default!;

        // short_desc, description, about, faq, gallery, video, custom ...
        [MaxLength(50)]
        public string BlockType { get; set; } = "custom";

        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public ICollection<ProductContentBlockTranslation> Translations { get; set; } = new List<ProductContentBlockTranslation>();
    }

    public class ProductContentBlockTranslation
    {
        public int Id { get; set; }
        public int BlockId { get; set; }
        public ProductContentBlock Block { get; set; } = default!;

        public int LangCodeId { get; set; }     // Langs.Id
        public Langs LangCode { get; set; } = default!;

        [MaxLength(200)]
        public string? Title { get; set; }      // opsiyonel bölüm başlığı
        public string Html { get; set; } = "";  // WYSIWYG içerik
    }

    // --- Attributes (özellikler) ---
    public class ProductAttributeGroup
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Products Product { get; set; } = default!;
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
    }

    public class ProductAttribute
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public ProductAttributeGroup Group { get; set; } = default!;

        [MaxLength(80)]
        public string KeyName { get; set; } = "";   // canonical key (örn: resolution, lensFocal)

        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public ICollection<ProductAttributeTranslation> Translations { get; set; } = new List<ProductAttributeTranslation>();
    }

    public class ProductAttributeTranslation
    {
        public int Id { get; set; }
        public int AttributeId { get; set; }
        public ProductAttribute Attribute { get; set; } = default!;

        public int LangCodeId { get; set; }
        public Langs LangCode { get; set; } = default!;

        [MaxLength(120)]
        public string Name { get; set; } = "";      // Etiket (örn: “Çözünürlük”)
        [MaxLength(400)]
        public string Value { get; set; } = "";     // Değer (örn: “1080p”)
    }
}