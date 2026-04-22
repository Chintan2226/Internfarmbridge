using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace MVC.Controllers
{
    public class LanguageController : Controller
    {
        private readonly IStringLocalizer<LanguageController> _localizer;

        public LanguageController(IStringLocalizer<LanguageController> localizer)
        {
            _localizer = localizer;
        }

        [HttpGet("Language/{lang}")]
        public IActionResult GetLanguage(string lang)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });

            // Set the culture for the current request to get the correct resources
            var cultureInfo = new CultureInfo(lang);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            // Get all translations from the localizer
            var translations = _localizer.GetAllStrings(includeParentCultures: true)
                .ToDictionary(s => s.Name, s => s.Value);

            return Json(translations);
        }
    }
}