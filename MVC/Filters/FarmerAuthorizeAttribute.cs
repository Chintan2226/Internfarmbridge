using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
 
namespace MVC.Filters
{
    /// <summary>
    /// Action filter that protects Farmer MVC routes.
    /// Reads the "authToken" cookie, validates it client-side (expiry + role),
    /// and redirects to Farmer/Login on any failure.
    /// </summary>
    public class FarmerAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var token = context.HttpContext.Request.Cookies["authToken"];
 
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new RedirectToActionResult("Login", "Farmer", null);
                return;
            }
 
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
 
                // Support both short claim type and full URI form
                var role = jwt.Claims.FirstOrDefault(x =>
                        x.Type == "role" ||
                        x.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
                    )?.Value;
 
                // Reject expired tokens or tokens that don't belong to the farmer role
                if (jwt.ValidTo < DateTime.UtcNow || role?.ToLower() != "farmer")
                {
                    context.Result = new RedirectToActionResult("Login", "Farmer", null);
                    return;
                }
            }
            catch
            {
                context.Result = new RedirectToActionResult("Login", "Farmer", null);
                return;
            }
 
            base.OnActionExecuting(context);
        }
    }
}
 