using kayialp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
namespace kayialp.Controllers
{
    public class ProductsController : BaseController
    {
        public ProductsController(ContentService contentService) : base(contentService) { }
        [HttpGet("{culture}/Category")]
        public IActionResult Index()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            ViewData["Culture"] = culture; return View();
        }
        [HttpGet("{culture}/Category/Products")]
        public IActionResult CategoryProducts()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            ViewData["Culture"] = culture; return View();
        }
        [HttpGet("{culture}/Products/ProductDetails/")]
        public IActionResult ProductDetails()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            ViewData["Culture"] = culture; return View();
        }
    }
}
