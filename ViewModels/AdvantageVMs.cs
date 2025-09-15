// ViewModels/AdvantageVMs.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace kayialp.ViewModels
{
    public sealed class AdvantageLangVM
    {
        public string LangCode { get; set; } = "";

        [MaxLength(30)]
        public string? Title { get; set; }

        [MaxLength(200)]
        public string? Content { get; set; }
    }

    // Ortak alanlar buraya
    public abstract class AdvantageBaseVM
    {
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;

        // TR → boş alanları doldur (sadece boşları çevir)
        public bool AutoTranslate { get; set; } = true;

        [Required]
        public List<AdvantageLangVM> Langs { get; set; } = new();
    }

    // CREATE için: görsel zorunlu
    public class CreateAdvantageVM : AdvantageBaseVM
    {
        [Required]
        public IFormFile? Image { get; set; }
    }

    // UPDATE için: Id ve mevcut görsel yolu + görsel opsiyonel
    public class UpdateAdvantageVM : AdvantageBaseVM
    {
        public int Id { get; set; }
        public IFormFile? Image { get; set; }         // opsiyonel; yüklenirse değiştir
        public string? ExistingImage { get; set; }     // sadece gösterim için
    }
}
