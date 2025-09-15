using kayialp.Models;
using Microsoft.EntityFrameworkCore;
using ProductModels;

namespace kayialp.Context
{
  public class kayialpDbContext : DbContext
  {
    public kayialpDbContext(DbContextOptions<kayialpDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
      base.OnModelCreating(mb);

      mb.Entity<ProductContentBlockTranslation>()
        .HasIndex(x => new { x.BlockId, x.LangCodeId })
        .IsUnique(); // her blok için dil başına 1 çeviri

      mb.Entity<ProductAttributeTranslation>()
        .HasIndex(x => new { x.AttributeId, x.LangCodeId })
        .IsUnique(); // her özellik için dil başına 1 çeviri


      mb.Entity<BlogPosts>().ToTable("BlogPosts");
      mb.Entity<BlogPostsTranslations>().ToTable("BlogPostsTranslations");
      mb.Entity<BlogPostContent>().ToTable("BlogPostContents");
      mb.Entity<BlogPostContentBlock>().ToTable("BlogPostContentBlocks");
      mb.Entity<BlogPostContentBlockTranslation>().ToTable("BlogPostContentBlockTranslations");
      mb.Entity<BlogCategories>().ToTable("BlogCategories");
      mb.Entity<BlogCategoriesTranslations>().ToTable("BlogCategoriesTranslations");

      // Dil bazında slug tekilliği (önerilir)
      mb.Entity<BlogPostsTranslations>()
        .HasIndex(x => new { x.LangCodeId, x.Slug })
        .IsUnique();

      mb.Entity<BlogCategoriesTranslations>()
        .HasIndex(x => new { x.LangCodeId, x.Slug })
        .IsUnique();

      mb.Entity<AdvantageTranslation>()
    .HasIndex(x => new { x.AdvantageId, x.LangCodeId })
    .IsUnique();

      mb.Entity<AdvantageTranslation>()
        .HasOne(t => t.Lang)
        .WithMany()                 // Langs tarafında koleksiyon yoksa
        .HasForeignKey(t => t.LangCodeId)
        .OnDelete(DeleteBehavior.Restrict);
    }

    // Localization start
    public DbSet<CategoriesTranslations> CategoriesTranslations { get; set; }
    public DbSet<FaqTranslations> FaqTranslations { get; set; }
    public DbSet<PageTranslations> PageTranslations { get; set; }
    public DbSet<ProductsTranslations> ProductsTranslations { get; set; }

    // Localization end
    public DbSet<Categories> Categories { get; set; }
    public DbSet<Faqs> Faqs { get; set; }
    public DbSet<Langs> Langs { get; set; }
    public DbSet<Pages> Pages { get; set; }
    public DbSet<Products> Products { get; set; }
    public DbSet<Users> Users { get; set; }

    public DbSet<ProductContent> ProductContents { get; set; } = default!;
    public DbSet<ProductContentBlock> ProductContentBlocks { get; set; } = default!;
    public DbSet<ProductContentBlockTranslation> ProductContentBlockTranslations { get; set; } = default!;

    public DbSet<ProductAttributeGroup> ProductAttributeGroups { get; set; } = default!;
    public DbSet<ProductAttribute> ProductAttributes { get; set; } = default!;
    public DbSet<ProductAttributeTranslation> ProductAttributeTranslations { get; set; } = default!;

    // Blogs
    public DbSet<BlogPosts> BlogPosts { get; set; }
    public DbSet<BlogPostsTranslations> BlogPostsTranslations { get; set; }
    public DbSet<BlogPostContent> BlogPostContents { get; set; }
    public DbSet<BlogPostContentBlock> BlogPostContentBlocks { get; set; }
    public DbSet<BlogPostContentBlockTranslation> BlogPostContentBlockTranslations { get; set; }
    public DbSet<BlogCategories> BlogCategories { get; set; }
    public DbSet<BlogCategoriesTranslations> BlogCategoriesTranslations { get; set; }

    // Fairs
    public DbSet<Fairs> Fairs { get; set; }
    public DbSet<FairTranslations> FairTranslations { get; set; }
    public DbSet<CompanyInfo> CompanyInfos { get; set; }
    public DbSet<CompanyInfoTranslation> CompanyInfoTranslations { get; set; }


    //legal page
    public DbSet<LegalPage> LegalPages { get; set; }
    public DbSet<LegalPageTranslation> LegalPageTranslations { get; set; }

    //HomeSlides 
    public DbSet<HomeSlide> HomeSlides { get; set; }
    public DbSet<HomeSlideTranslation> HomeSlideTranslations { get; set; }
    //Advantage 

    public DbSet<Advantage> Advantages { get; set; }
    public DbSet<AdvantageTranslation> AdvantageTranslations { get; set; }

  }
}
