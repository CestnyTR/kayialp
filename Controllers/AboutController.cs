using kayialp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace kayialp.Controllers
{
    public class AboutController : BaseController
    {
        public AboutController(ContentService contentService) : base(contentService) { }

        [HttpGet("{culture}/a/{slug}")]
        // [HttpGet("{culture}/about")]
        public IActionResult Index()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            ViewData["Culture"] = culture;
            return View();
        }
    }
}