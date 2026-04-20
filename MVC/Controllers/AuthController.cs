using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers;

public class AuthController : Controller
{
    // GET: /Auth/ForgotPassword
    public IActionResult ForgotPassword()
    {
        return View();
    }
}
