namespace kayialp.ViewModels
{
    public class CategoryNavItemViewModel
    {
        public int Id { get; set; }
        public string NameKey { get; set; }  // Localize ederken kullanacağız: $"Categories.{NameKey}"
        public string Slug { get; set; }     // Aktif dilin category slug'ı
    }
}
