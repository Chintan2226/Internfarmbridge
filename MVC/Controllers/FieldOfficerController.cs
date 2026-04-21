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

        // ========== ELASTICSEARCH SEARCH METHODS (ADD AT THE END OF CLASS) ==========

        [HttpPost]
        public async Task<IActionResult> SearchQCRecords([FromBody] SearchRequestModel request)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBase}/api/FieldOfficer/search/qc-records", content);
                var result = await response.Content.ReadAsStringAsync();

                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchQCRecords failed");
                return Json(new { success = false, results = new List<QCSearchResult>() });
            }
        }


        public IActionResult Dashboard()
        {
            return View();
        }

        // ✅ QC Request Management Binod
        public IActionResult QCRequest()
        {
            return View(); // will return Views/FieldOfficer/RequestManagement.cshtml
        }
        // ✅ Profile Ruman
        public IActionResult Profile()
        {
            return View(); // will return Views/FieldOfficer/Profile.cshtml
        }
        public IActionResult QualityParams()
        {
            return View(); // will return Views/FieldOfficer/Profile.cshtml
        }
        public IActionResult QualityForm()
        {
            return View(); // will return Views/FieldOfficer/QualityForm.cshtml
        }

        public IActionResult InspectionDetail()
        {
            return View(); // will return Views/FieldOfficer/InspectionDetail.cshtml
        }
        public IActionResult InspectionHistory()
        {
            return View(); // will return Views/FieldOfficer/InspectionDetail.cshtml
        }

        public IActionResult PaymentHistory()
        {
            return View(); // will return Views/FieldOfficer/PaymentHistory.cshtml
        }

        public IActionResult Catalog()
        {
            return View(); // will return Views/FieldOfficer/Profile.cshtml
        }
       
         public IActionResult  UploadImage()
        {
            return View(); 
        }
    

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}