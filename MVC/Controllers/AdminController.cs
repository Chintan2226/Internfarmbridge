using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVC.Filters;

namespace MVC.Controllers
{
    [AdminAuthorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly HttpClient _http;
        private readonly string _apiBase;

        public AdminController(
            ILogger<AdminController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration config
        )
        {
            _logger = logger;
            _http = httpClientFactory.CreateClient();

            // It is better to pull this from appsettings.json
            // Defaulting to your provided port 5020
            _apiBase = (config["ApiBaseUrl"] ?? "http://localhost:5020").TrimEnd('/');
        }

        // 1. Render the Dashboard Page
        // GET: /Admin/Dashboard
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Catalog()
        {
            return View();
        }

        public IActionResult FarmerManagment()
        {
            return View();
        }

        // ─── API Proxy Methods (Called by JavaScript) ───

        // GET: /Admin/GetDashboardKpi
        [HttpGet]
        public async Task<IActionResult> GetDashboardKpi()
        {
            return await ProxyGetRequest($"{_apiBase}/api/Admin/GetDashboardKpi");
        }

        // GET: /Admin/GetRevenueChart?period=monthly
        [HttpGet]
        public async Task<IActionResult> GetRevenueChart(string period = "monthly")
        {
            return await ProxyGetRequest($"{_apiBase}/api/Admin/GetRevenueChart?period={period}");
        }

        // GET: /Admin/GetOrderVolumeChart?period=monthly
        [HttpGet]
        public async Task<IActionResult> GetOrderVolumeChart(string period = "monthly")
        {
            return await ProxyGetRequest(
                $"{_apiBase}/api/Admin/GetOrderVolumeChart?period={period}"
            );
        }

        // GET: /Admin/GetTodayCropListings
        [HttpGet]
        public async Task<IActionResult> GetTodayCropListings()
        {
            return await ProxyGetRequest($"{_apiBase}/api/Admin/GetTodayCropListings");
        }

        // GET: /Admin/GetPendingApprovals
        [HttpGet]
        public async Task<IActionResult> GetPendingApprovals()
        {
            return await ProxyGetRequest($"{_apiBase}/api/Admin/GetPendingApprovals");
        }

        // POST: /Admin/ApproveUser
        [HttpPost]
        public async Task<IActionResult> ApproveUser(int userId)
        {
            try
            {
                var content = new FormUrlEncodedContent(
                    new[] { new KeyValuePair<string, string>("userId", userId.ToString()) }
                );

                var response = await _http.PostAsync($"{_apiBase}/api/Admin/ApproveUser", content);

                var json = await response.Content.ReadAsStringAsync();

                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving user {UserId}", userId);
                return Json(new { success = false });
            }
        }

        // ─── Private Helper ───
        private async Task<IActionResult> ProxyGetRequest(string url)
        {
            try
            {
                var response = await _http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Content(json, "application/json");
                }

                _logger.LogWarning(
                    "API returned error {StatusCode} for {Url}",
                    response.StatusCode,
                    url
                );
                return Json(new { success = false, message = "API Error" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Backend API at {Url}", url);
                return Json(
                    new { success = false, message = "Connection to data service failed." }
                );
            }
        }

        // Get All
        public async Task<IActionResult> GetAll(string category, string unit)
        {
            var response = await _http.GetAsync(
                $"{_apiBase}/api/Admin/GetAll?category={category}&unit={unit}"
            );
            var data = await response.Content.ReadAsStringAsync();
            return Content(data, "application/json");
        }

        // Get By Id
        public async Task<IActionResult> GetById(long id)
        {
            var response = await _http.GetAsync($"{_apiBase}/api/Admin/GetById/{id}");
            var data = await response.Content.ReadAsStringAsync();
            return Content(data, "application/json");
        }

        // Add
        [HttpPost]
        public async Task<IActionResult> Add()
        {
            var form = Request.Form;

            using var content = new MultipartFormDataContent();

            foreach (var key in form.Keys)
                content.Add(new StringContent(form[key]), key);

            var file = Request.Form.Files.FirstOrDefault();
            if (file != null)
            {
                var stream = file.OpenReadStream();
                content.Add(new StreamContent(stream), "ImageFile", file.FileName);
            }

            var response = await _http.PostAsync($"{_apiBase}/api/Admin/Add", content);
            var data = await response.Content.ReadAsStringAsync();

            return Content(data, "application/json");
        }

        // Edit
        [HttpPost]
        public async Task<IActionResult> Edit()
        {
            var form = Request.Form;

            using var content = new MultipartFormDataContent();

            foreach (var key in form.Keys)
                content.Add(new StringContent(form[key]), key);

            var file = Request.Form.Files.FirstOrDefault();
            if (file != null)
            {
                var stream = file.OpenReadStream();
                content.Add(new StreamContent(stream), "ImageFile", file.FileName);
            }

            var response = await _http.PutAsync($"{_apiBase}/api/Admin/Edit", content);
            var data = await response.Content.ReadAsStringAsync();

            return Content(data, "application/json");
        }

        // Delete
        [HttpPost]
        public async Task<IActionResult> Delete(long id)
        {
            var response = await _http.DeleteAsync($"{_apiBase}/api/Admin/Delete/{id}");
            var data = await response.Content.ReadAsStringAsync();

            return Content(data, "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCatalogStatus([FromBody] vm_ToggleStatus model)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(model),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.PutAsync($"{_apiBase}/api/Admin/ToggleStatus", content);
            var data = await response.Content.ReadAsStringAsync();

            return Content(data, "application/json");
        }

        public async Task<IActionResult> GetUnits()
        {
            var response = await _http.GetAsync($"{_apiBase}/api/Admin/GetUnits");
            return Content(await response.Content.ReadAsStringAsync(), "application/json");
        }

        public IActionResult FOCreate()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFieldOfficerViewModel model)
        {
            var json = JsonSerializer.Serialize(model);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_apiBase}/api/Admin/create", content);

            var result = await response.Content.ReadAsStringAsync();

            return Content(result, "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> GetFOList(string search = "", int page = 1)
        {
            var response = await _http.GetAsync(
                $"{_apiBase}/api/Admin/fo/list?search={search}&page={page}"
            );

            var result = await response.Content.ReadAsStringAsync();

            return Content(result, "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> GetWarehouses()
        {
            var response = await _http.GetAsync($"{_apiBase}/api/Admin/warehouses");

            var result = await response.Content.ReadAsStringAsync();

            return Content(result, "application/json");
        }

        public IActionResult FieldOfficers()
        {
            return View();
        }

        public IActionResult Vendor()
        {
            return View();
        }

        public IActionResult Warehouse()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }

    public class vm_ToggleStatus
    {
        public long Id { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateFieldOfficerViewModel
    {
        public string FullName { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }

        public int WarehouseId { get; set; }

        public string? AssignedRegion { get; set; }

        // Populated by controller from BAL DataTable
        public List<WarehouseDropdownItem> Warehouses { get; set; } = new();
    }

    public class WarehouseDropdownItem
    {
        public int WarehouseId { get; set; }
        public string Name { get; set; }
        public string District { get; set; }
        public string State { get; set; }
    }

    // ─── Result ViewModel returned from BAL ───────────────────────────────

    public class CreateFOResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? UserId { get; set; }
        public string? TempPassword { get; set; }
    }

    public class vm_FOStatusRequest
    {
        public int UserId { get; set; }
        public int AdminId { get; set; }
        public bool IsActive { get; set; }
        public string Reason { get; set; } = "";
    }
}
