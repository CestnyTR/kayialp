// ViewModels/BlogCategoryVMs.cs
using System.Collections.Generic;

namespace kayialp.ViewModels
{
    public class BlogListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Slug { get; set; }
    public string Summary { get; set; }

    // Eksikler:
    public string ImageUrl { get; set; }
    public string ImageAlt { get; set; }
    public DateTime CreatedAt { get; set; } // PublishedAt yoksa CreatedAt kullan
}


}
