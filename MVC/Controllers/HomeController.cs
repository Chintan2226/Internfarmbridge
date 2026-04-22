using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MVC.Models;

namespace MVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MVC.Services.IUnsplashService _unsplashService;
 
    public HomeController(ILogger<HomeController> logger, MVC.Services.IUnsplashService unsplashService)
    {
        _logger = logger;
        _unsplashService = unsplashService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.HeroImageUrl = await _unsplashService.GetPhotoUrlAsync("indian farmer agriculture");
        ViewBag.CtaImageUrl = await _unsplashService.GetPhotoUrlAsync("agritech india");
        ViewBag.CtaBgImageUrl = await _unsplashService.GetPhotoUrlAsync("indian field agriculture landscape");
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
