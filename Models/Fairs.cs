namespace kayialp.Models
{
// Models/Fairs.cs
public class Fairs
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }   // zorunlu
    public DateTime EndDate { get; set; }     // zorunlu
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Venue { get; set; }
    public string? WebsiteUrl { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
    public string? Cover424x460 { get; set; } // /uploads/fairs/{id}/cover-424x460.webp
}

// Models/FairTranslations.cs
public class FairTranslations
{
    public int Id { get; set; }
    public int FairId { get; set; }
    public int LangCodeId { get; set; }

    public string? KeyName { get; set; }   // EN başlıktan camelCase, tek ve sabit
    public string? Title { get; set; }     // Kart başlığı
    public string? Slug { get; set; }      // Detay yok ama ileride lazım olabilir
    public string? FairName { get; set; }  // (opsiyonel) “Fair Name: …” metni
    public string? PlaceText { get; set; } // (opsiyonel) “Place: …” metni
}

}
