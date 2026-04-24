using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using MVC.Models;
namespace MVC.Controllers
{
    // [Route("[controller]")]
    public class SearchController : Controller
    {
        private readonly HttpClient _httpClient;

        public SearchController(IHttpClientFactory factory)
        {
            _httpClient = factory.CreateClient();
        }

        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrEmpty(query))
                query = "Indian Agriculture";

            var apiUrl = $"http://localhost:5020/api/GoogleSearch?query={query}";

            var response = await _httpClient.GetStringAsync(apiUrl);

            var json = JObject.Parse(response);

            // Get thumbnails from AI Overview
            var thumbnails = json["ai_overview"]?["references"]
                ?.Where(x => x["thumbnail"] != null)
                ?.Select(x => x["thumbnail"]?.ToString())
                ?.ToList();

            int index = 0;

            var results = json["organic_results"]
                .Select(x => new GoogleSearchViewModel
                {
                    Title = x["title"]?.ToString(),
                    Link = x["link"]?.ToString(),
                    Snippet = x["snippet"]?.ToString(),
                    Source = x["source"]?.ToString(),

                    Thumbnail =
                        x["thumbnail"]?.ToString() ??
                        (thumbnails != null && index < thumbnails.Count
                            ? thumbnails[index++]
                            : x["favicon"]?.ToString())
                }).ToList();

            return View(results);
        }
        
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}