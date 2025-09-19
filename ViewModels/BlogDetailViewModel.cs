// ViewModels/BlogCategoryVMs.cs
using System.Collections.Generic;

namespace kayialp.ViewModels
{


public class BlogDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Slug { get; set; }
    public string ImageUrl { get; set; }
    public string ImageAlt { get; set; }
    public string Summary { get; set; }
    public string Body { get; set; }
    public DateTime CreatedAt { get; set; }
}

}
