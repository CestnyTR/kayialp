// Helpers/LocalizationHelper.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using kayialp.Services; 

namespace kayialp.Helpers
{
    public static class LocalizationHelper
    {
        public static string Localize(this IHtmlHelper htmlHelper, string key)
        {
            var httpContext = htmlHelper.ViewContext.HttpContext;
            var contentService = httpContext.RequestServices.GetService<ContentService>();
            return contentService?.GetText(key) ?? $"[[{key}]]";
        }
    }

}
