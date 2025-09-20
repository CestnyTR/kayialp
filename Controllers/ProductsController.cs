using kayialp.Services;
using Microsoft.AspNetCore.Mvc;

namespace kayialp.Controllers
{
    public class ProductsController : BaseController
    {
        public ProductsController(ContentService contentService) : base(contentService) { }

        // /{culture}/p/{slug}
        [HttpGet("{culture}/p/{slug}")]
        public IActionResult Index(string culture, string slug)
        {
            ViewData["Culture"] = culture;
            ViewData["ProductsSlug"] = slug; // "pages.layout.productCategory.slug" değeri
            return View(); // Views/Products/Index.cshtml
        }

        // /{culture}/{pCSlug}/{cSlug}
        [HttpGet("{culture}/{pCSlug}/{cSlug}")]
        public IActionResult CategoryProducts(string culture, string pCSlug, string cSlug, int page = 1, int pageSize = 9)
        {
            ViewData["Culture"] = culture;
            ViewData["ProductsCategorySlug"] = pCSlug; // üst segment (örn: product-categories / urun-kategorileri)
            ViewData["CategorySlug"] = cSlug;          // alt segment (örn: monster / paketleme)
            ViewData["Page"] = page;
            ViewData["PageSize"] = pageSize;
            return View(); // Views/Products/CategoryProducts.cshtml
        }


        // /{culture}/Products/ProductDetails/?id=...
        // [HttpGet("{culture}/Products/ProductDetails/")]
        [HttpGet("{culture}/{cSlug}/{productSlug}-{id:int}")]
        public IActionResult ProductDetails(string culture, string pCSlug, string cSlug, string productSlug, int id)
        {
            ViewData["Culture"] = culture;
            ViewData["ProductsCategorySlug"] = pCSlug;
            ViewData["CategorySlug"] = cSlug;
            ViewData["ProductSlug"] = productSlug;
            ViewData["ProductId"] = id;

            return View("ProductDetails");
        }

    }
}
