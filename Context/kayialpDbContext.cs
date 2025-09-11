using kayialp.Models;
using kayialp.Models.Localization;
using Microsoft.EntityFrameworkCore;

namespace kayialp.Context
{
    public class kayialpDbContext : DbContext
    {
        public kayialpDbContext(DbContextOptions<kayialpDbContext> options) : base(options)
        {
        }

        // Localization start
        public DbSet<LayoutPage> LayoutPage { get; set; }
        public DbSet<CategoriesTranslations> CategoriesTranslations { get; set; }
        public DbSet<FaqTranslations> FaqTranslations { get; set; }
        public DbSet<PageTranslations> PageTranslations { get; set; }
        public DbSet<ProductDetailsFeatureTranslations> ProductDetailsFeatureTranslations { get; set; }
        public DbSet<ProductDetailsTranslations> ProductDetailsTranslations { get; set; }
        public DbSet<ProductsTranslations> ProductsTranslations { get; set; }
        public DbSet<SliderTranslations> SliderTranslations { get; set; }

        // Localization end
        public DbSet<Categories> Categories { get; set; }
        public DbSet<Faqs> Faqs { get; set; }
        public DbSet<Langs> Langs { get; set; }
        public DbSet<Pages> Pages { get; set; }
        public DbSet<ProductDetails> ProductDetails { get; set; }
        public DbSet<ProductDetailsFeatures> ProductDetailsFeatures { get; set; }
        public DbSet<Products> Products { get; set; }
        public DbSet<Sliders> Sliders { get; set; }
        public DbSet<Users> Users { get; set; }
    }
}
