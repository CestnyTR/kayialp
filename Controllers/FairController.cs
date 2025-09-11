using kayialp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace kayialp.Controllers
{    public class FairController : BaseController
    {
        public FairController(ContentService contentService) : base(contentService) { }

        [HttpGet("{culture}/Fuar")]
        public IActionResult Index()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            ViewData["Culture"] = culture;
            return View();
        }
    }
}