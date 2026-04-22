using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MVC.Controllers
{
    public class StaffAuthController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public StaffAuthController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
        }

        // Login Page
        [HttpGet]
        public IActionResult Login() => View();

        // Login Action
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] vm_StaffLogin model)
        {
            try
            {
                if (model == null)
                    return Json(new { success = false, message = "Invalid request data." });

                if (string.IsNullOrWhiteSpace(model.Email))
                    return Json(new { success = false, message = "Email is required." });

                if (string.IsNullOrWhiteSpace(model.Password))
                    return Json(new { success = false, message = "Password is required." });

                var apiBaseUrl = _configuration["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5020";
                var endpoint = $"{apiBaseUrl}/api/StaffAuth/login";

                var payload = new
                {
                    Email = model.Email.Trim().ToLower(),
                    Password = model.Password
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                    {
                        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver()
                    }),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(endpoint, content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic? api = JsonConvert.DeserializeObject(body);
                    string token = api?.token ?? "";
                    string role = api?.role ?? "";
                    string fullName = api?.fullName ?? "";

                    return Json(new
                    {
                        success = true,
                        message = $"Welcome, {fullName}!",
                        token,
                        role,
                        fullName
                    });
                }
                else
                {
                    string errorMsg = "Invalid credentials or insufficient permissions.";
                    try
                    {
                        dynamic? api = JsonConvert.DeserializeObject(body);
                        errorMsg = (string?)api?.message ?? errorMsg;
                    }
                    catch { /* ignore parse errors */ }

                    return Json(new { success = false, message = errorMsg });
                }
            }
            catch (Exception ex)
            {
                // Log exception in production
                return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
            }
        }

        // Forgot Password
        [HttpGet]
        public IActionResult ForgotPassword(string? email)
        {
            ViewBag.Email = email;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }

    public class vm_StaffLogin
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}