using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace MVC.Filters
{
    public class FieldOfficerAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var token = context.HttpContext.Request.Cookies["authToken"];

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new RedirectToActionResult("Login", "StaffAuth", null);
                return;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var role = jwt.Claims.FirstOrDefault(x =>
                        x.Type == "role" ||
                        x.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
                    )?.Value;

                if (jwt.ValidTo < DateTime.UtcNow || role?.ToLower() != "field_officer")
                {
                    context.Result = new RedirectToActionResult("Login", "StaffAuth", null);
                }
            }
            catch
            {
                context.Result = new RedirectToActionResult("Login", "StaffAuth", null);
            }

            base.OnActionExecuting(context);
        }
    }
}