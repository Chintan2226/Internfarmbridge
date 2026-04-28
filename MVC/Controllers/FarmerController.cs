using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVC.Filters;
using MVC.Services;
using MVC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace MVC.Controllers
{
    public class FarmerController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AuthApiService _authApi;
        private readonly string _apiBase;
        private readonly ILogger<FarmerController> _logger;


        public FarmerController(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            AuthApiService authApi,
            ILogger<FarmerController> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _authApi = authApi;
            _logger = logger;
            _apiBase = (configuration["ApiBaseUrl"] ?? "http://localhost:5020").TrimEnd('/');
        }

        public async Task<IActionResult> News(string query)
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

        // ========== ELASTICSEARCH SEARCH METHODS (ADD AT THE END OF CLASS) ==========

        [HttpPost]
        public async Task<IActionResult> SearchMyCrops([FromBody] SearchRequestModel request)
        {
            try
            {
                var token = Request.Cookies["authToken"];
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, results = new List<CropSearchResult>(), totalCount = 0 });
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add token to request
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var response = await _httpClient.PostAsync($"{_apiBase}/api/FarmerApp/search/my-crops", content);
                var result = await response.Content.ReadAsStringAsync();

                // Return the response even if empty
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchMyCrops failed");
                return Json(new { success = false, error = ex.Message, results = new List<CropSearchResult>(), totalCount = 0 });
            }
        }

        // GET /Farmer/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST /Farmer/Register
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] vm_FarmerRegister model)
        {
            try
            {
                if (model == null)
                    return Json(new { success = false, message = "Invalid request data." });

                if (string.IsNullOrWhiteSpace(model.FullName))
                    return Json(new { success = false, message = "Full name is required." });

                if (string.IsNullOrWhiteSpace(model.Email) || !model.Email.Contains("@"))
                    return Json(new { success = false, message = "Valid email is required." });

                if (string.IsNullOrWhiteSpace(model.Phone))
                    return Json(new { success = false, message = "Mobile number is required." });

                if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
                    return Json(new { success = false, message = "Password must be at least 8 characters." });

                if (model.Password != model.ConfirmPassword)
                    return Json(new { success = false, message = "Passwords do not match." });

                var apiUrl = _configuration["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5020";
                var endpoint = $"{apiUrl}/api/auth/register";

                var json = JsonConvert.SerializeObject(new
                {
                    FullName = model.FullName?.Trim(),
                    Email = model.Email?.Trim(),
                    Phone = model.Phone?.Trim(),
                    Password = model.Password,
                    ConfirmPassword = model.ConfirmPassword,
                    Address = model.Address?.Trim() ?? "",
                    State = model.State?.Trim() ?? "",
                    District = model.District?.Trim() ?? ""
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return Json(new { success = true, message = "Registration successful! Please log in." });

                try
                {
                    dynamic? apiResponse = JsonConvert.DeserializeObject(responseContent);
                    string errorMessage = apiResponse?.message ?? "Registration failed. Please try again.";
                    return Json(new { success = false, message = errorMessage });
                }
                catch
                {
                    return Json(new { success = false, message = "Registration failed. Please try again later." });
                }
            }
            catch
            {
                return Json(new { success = false, message = "An error occurred. Please try again later." });
            }
        }

        // GET /Farmer/Login
        [HttpGet]
        public IActionResult Login()
        {
            // Already logged in? Skip to dashboard.
            // if (!string.IsNullOrEmpty(Request.Cookies["authToken"]))
            //     return RedirectToAction("Dashboard");

            // Build Google OAuth URL and pass to view
            ViewBag.GoogleUrl = BuildGoogleOAuthUrl();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string? code, string? error)
        {
            // 1️⃣ Handle user cancellation or OAuth error
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                return RedirectToAction("Login");

            try
            {
                // 2️⃣ Exchange authorization code for Google access token
                var tokenResponse = await _httpClient.PostAsync(
                    "https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "code",          code },
                        { "client_id",     _configuration["GoogleOAuth:ClientId"]     ?? "" },
                        { "client_secret", _configuration["GoogleOAuth:ClientSecret"] ?? "" },
                        { "redirect_uri",  _configuration["GoogleOAuth:RedirectUri"]  ?? "" },
                        { "grant_type",    "authorization_code" }
                    }));

                if (!tokenResponse.IsSuccessStatusCode)
                    return RedirectToAction("Login");

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var accessToken = JObject.Parse(tokenJson)["access_token"]?.ToString();

                if (string.IsNullOrEmpty(accessToken))
                    return RedirectToAction("Login");

                // 3️⃣ Fetch Google user profile
                var profileRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://www.googleapis.com/oauth2/v2/userinfo");
                profileRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var profileResponse = await _httpClient.SendAsync(profileRequest);
                if (!profileResponse.IsSuccessStatusCode)
                    return RedirectToAction("Login");

                var userInfo = await profileResponse.Content.ReadAsStringAsync();
                var data = JObject.Parse(userInfo);

                var dto = new GoogleUserDto

                {
                    Email = data["email"]?.ToString(),
                    Name = data["name"]?.ToString(),
                    ProviderId = data["id"]?.ToString(),
                    Role = "farmer"
                };

                if (string.IsNullOrEmpty(dto.Email))
                    return RedirectToAction("Login");

                // 4️⃣ Call our API → get JWT (same shape as normal login)
                var resultJson = await _authApi.GoogleLogin(dto);
                var apiResult = JObject.Parse(resultJson);

                bool success = apiResult["success"]?.Value<bool>() ?? false;
                string token = apiResult["token"]?.ToString() ?? "";
                string role = apiResult["role"]?.ToString() ?? "farmer";
                string fullName = apiResult["fullName"]?.ToString() ?? dto.Name ?? "";
                int userId = apiResult["userId"]?.Value<int>() ?? 0;
                string email = apiResult["email"]?.ToString() ?? dto.Email;

                if (!success || string.IsNullOrEmpty(token))
                    return RedirectToAction("Login");

                // 5️⃣ Store JWT in cookie — identical to normal login flow
                int expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30");

                Response.Cookies.Append("authToken", token, new CookieOptions
                {
                    HttpOnly = false,           // JS needs to read it (matches existing login.cshtml)
                    Secure = false,           // set true in production (HTTPS)
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(expiryMinutes),
                    Path = "/"
                });

                // Optionally mirror what the normal login does with additional cookies/session
                // so Dashboard works the same regardless of login method
                Response.Cookies.Append("userRole", role, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromMinutes(expiryMinutes) });
                Response.Cookies.Append("userFullName", fullName, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromMinutes(expiryMinutes) });
                Response.Cookies.Append("userId", userId.ToString(), new CookieOptions { Path = "/", MaxAge = TimeSpan.FromMinutes(expiryMinutes) });

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                // Log ex here if you have a logger
                TempData["LoginError"] = "Google login failed. Please try again.";
                return RedirectToAction("Login");
            }
        }

        private string BuildGoogleOAuthUrl()
        {
            var clientId = _configuration["GoogleOAuth:ClientId"] ?? "";
            var redirectUri = _configuration["GoogleOAuth:RedirectUri"] ?? "";

            return "https://accounts.google.com/o/oauth2/v2/auth?"
                 + $"client_id={Uri.EscapeDataString(clientId)}"
                 + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                 + "&response_type=code"
                 + "&scope=email%20profile"
                 + "&prompt=consent%20select_account"
                 + "&access_type=offline"
                 + "&include_granted_scopes=false";
        }


        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Dashboard()
        {
            return View();
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult PricePrediction()
        {
            return View();
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Crops()
        {
            return View();
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult QCSlots()
        {
            return View();
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Payments()
        {
            return View();
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Notifications()
        {
            return View();
        }


        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Inquiries()
        {
            return View();
        }

        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Orders()
        {
            return View();
        }


        [FarmerAuthorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Profile()
        {
            return View();
        }

        // POST /Farmer/Login
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] vm_FarmerLogin model)
        {
            try
            {
                if (model == null)
                    return Json(new { success = false, message = "Invalid request data." });

                if (string.IsNullOrWhiteSpace(model.EmailOrPhone))
                    return Json(new { success = false, message = "Email or phone is required." });

                if (string.IsNullOrWhiteSpace(model.Password))
                    return Json(new { success = false, message = "Password is required." });

                var apiUrl = _configuration["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5020";
                var endpoint = $"{apiUrl}/api/auth/login";

                var json = JsonConvert.SerializeObject(new
                {
                    EmailOrPhone = model.EmailOrPhone?.Trim(),
                    Password = model.Password
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic? apiResponse = JsonConvert.DeserializeObject(responseContent);
                    string token = apiResponse?.token ?? "";
                    string role = apiResponse?.role ?? "farmer";
                    string fullName = apiResponse?.fullName ?? "";

                    return Json(new
                    {
                        success = true,
                        message = $"Welcome back, {fullName}! Login successful.",
                        token = token,
                        role = role,
                        fullName = fullName
                    });
                }
                else
                {
                    try
                    {
                        dynamic? apiResponse = JsonConvert.DeserializeObject(responseContent);
                        string errorMessage = apiResponse?.message ?? "Invalid email/phone or password.";
                        return Json(new { success = false, message = errorMessage });
                    }
                    catch
                    {
                        return Json(new { success = false, message = "Login failed. Please try again." });
                    }
                }
            }
            catch
            {
                return Json(new { success = false, message = "An error occurred. Please try again later." });
            }
        }

        // GET /Farmer/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }

    public class vm_FarmerLogin
    {
        public string EmailOrPhone { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class vm_FarmerRegister
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? District { get; set; }
    }

    public class GoogleUserDto
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? ProviderId { get; set; }
        public string Role { get; set; } = "farmer";
    }
}