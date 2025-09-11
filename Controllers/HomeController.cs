using kayialp.Models;
using kayialp.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;

namespace kayialp.Controllers
{
    public class HomeController : BaseController
    {

        public HomeController(ContentService contentService) : base(contentService) { }

        public IActionResult Index()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName; // Geçerli dil
            ViewData["Culture"] = culture;
            return View();
        }


    }
}
