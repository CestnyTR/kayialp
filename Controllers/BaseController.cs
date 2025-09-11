using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using kayialp.Services;
using System.Threading;

namespace kayialp.Controllers
{
    public class BaseController : Controller
    {
        protected readonly ContentService _contentService;

        public BaseController(ContentService contentService)
        {
            _contentService = contentService;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;

            // Örn: "lang-en", "lang-tr", "arabic"
            ViewBag.BodyClass = $"lang-{culture}";
            ViewData["Culture"] = culture;

            base.OnActionExecuting(context);
        }
    }
}
