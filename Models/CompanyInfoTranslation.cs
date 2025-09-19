using kayialp.Models;

namespace kayialp.Models
{
    public sealed class CompanyInfoTranslation
    {
        public int Id { get; set; }
        public int CompanyInfoId { get; set; }
        public int LangCodeId { get; set; }
        public string WorkingDays { get; set; } = "";
        public string AboutHtml { get; set; } = "";
        public string MissionHtml { get; set; } = "";
        public string VisionHtml { get; set; } = "";

        public CompanyInfo CompanyInfo { get; set; } = null!;
    }
}
