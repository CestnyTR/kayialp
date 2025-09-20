using kayialp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace kayialp.Controllers
{
    public class ContactController : BaseController
    {
        public ContactController(ContentService contentService) : base(contentService) { }
        // [HttpGet("{culture}/Contact")]
        
        [HttpGet("{culture}/c/{slug}")]
        public IActionResult Index()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            ViewData["Culture"] = culture;
            return View();
        }
    }
}