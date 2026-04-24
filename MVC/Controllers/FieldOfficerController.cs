using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVC.Filters;
using MVC.Models;
using System.Text.Json;
using System.Text;

namespace MVC.Controllers
{

    [FieldOfficerAuthorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class FieldOfficerController : Controller
    {
        private readonly ILogger<FieldOfficerController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiBase;

        public FieldOfficerController(ILogger<FieldOfficerController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _apiBase = (configuration["ApiBaseUrl"] ?? "http://localhost:5020").TrimEnd('/');
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        // ✅ QC Request Management Binod
        public IActionResult QCRequest()
        {
            return View();
        }
        // ✅ Profile Ruman
        public IActionResult Profile()
        {
            return View();
        }
        public IActionResult QualityParams()
        {
            return View();
        }
        public IActionResult QualityForm()
        {
            return View();
        }

        public IActionResult InspectionDetail()
        {
            return View();
        }
        public IActionResult InspectionHistory()
        {
            return View();
        }

        public IActionResult PaymentHistory()
        {
            return View();
        }

        public IActionResult Catalog()
        {
            return View();
        }

        public IActionResult UploadImage()
        {
            return View();
        }

        // ========== ONLY THIS METHOD ADDED (SAME AS ADMIN SIDE) ==========

        [HttpPost]
        public async Task<IActionResult> SearchQCRecords([FromBody] SearchRequestModel request)
        {
            try
            {
                Console.WriteLine("=== DEBUG: MVC Controller Hit ===");
                Console.WriteLine($"Query: {request?.Query}");

                var token = Request.Cookies["authToken"];
                Console.WriteLine($"Token found: {(string.IsNullOrEmpty(token) ? "NO" : "YES")}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var apiUrl = $"{_apiBase}/api/FieldOfficer/search/qc-records";
                Console.WriteLine($"API URL: {apiUrl}");

                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                }

                var response = await _httpClient.PostAsync(apiUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"API Status Code: {response.StatusCode}");
                Console.WriteLine($"API Response Length: {result?.Length ?? 0}");
                Console.WriteLine($"API Response: {result}");

                if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(result))
                {
                    return Content(result, "application/json");
                }
                else
                {
                    // Return fallback response
                    return Json(new
                    {
                        success = false,
                        message = "No data found",
                        results = new List<object>(),
                        totalCount = 0
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return Json(new { success = false, error = ex.Message, results = new List<object>() });
            }
        }

        

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}