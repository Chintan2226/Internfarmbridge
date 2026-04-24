using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using MVC.Models;
using System.Text.Json;
using System.Text;

namespace MVC.Controllers
{
    public class VendorController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VendorController> _logger;
        private readonly string _apiBase;

        public VendorController(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<VendorController> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiBase = (configuration["ApiBaseUrl"] ?? "http://localhost:5020").TrimEnd('/');
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }

        // ✅ UPDATED Login() — builds Google OAuth URL safely
        public IActionResult Login()
        {
            var token = Request.Cookies["authToken"];
            if (!string.IsNullOrEmpty(token))
                return RedirectToAction("Catalog");

            var clientId = _configuration["GoogleOAuth:ClientId"];
            var redirectUri = _configuration["GoogleOAuth:VendorRedirectUri"];

            // Prevent 'Value cannot be null' error
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
            {
                ViewBag.ConfigError = "OAuth Configuration is missing in appsettings.json";
                return View();
            }

            Response.Cookies.Append("isGoogleUser", "false", new CookieOptions
            {
                MaxAge = TimeSpan.FromMinutes(30),
                Path = "/"
            });

            var googleUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                            $"client_id={clientId}" +
                            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                            $"&response_type=code" +
                            $"&scope=email%20profile" +
                            $"&prompt=consent%20select_account" +
                            $"&access_type=offline";

            ViewBag.GoogleUrl = googleUrl;
            return View();
        }

        // ✅ Google OAuth Callback for Vendor
        public async Task<IActionResult> GoogleCallback(string code, string error)
        {
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                return RedirectToAction("Login");

            using var client = new HttpClient();

            // 1. Exchange code for Access Token
            var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", _configuration["GoogleOAuth:ClientId"] },
                    { "client_secret", _configuration["GoogleOAuth:ClientSecret"] },
                    { "redirect_uri", _configuration["GoogleOAuth:VendorRedirectUri"] },
                    { "grant_type", "authorization_code" }
                }));

            if (!tokenResponse.IsSuccessStatusCode) return RedirectToAction("Login");

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var accessToken = JObject.Parse(tokenJson)["access_token"]?.ToString();

            // 2. Fetch User Info
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userInfoJson = await client.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            var userInfo = JObject.Parse(userInfoJson);

            // 3. Call API
            var apiBase = _configuration["ApiBaseUrl"]?.TrimEnd('/');
            var apiPayload = new
            {
                email = userInfo["email"]?.ToString(),
                name = userInfo["name"]?.ToString(),
                providerId = userInfo["id"]?.ToString()
            };

            var apiResponse = await _httpClient.PostAsJsonAsync($"{apiBase}/api/vendor/google-login", apiPayload);

            if (apiResponse.IsSuccessStatusCode)
            {
                var apiResult = JObject.Parse(await apiResponse.Content.ReadAsStringAsync());
                var jwtToken = apiResult["token"]?.ToString();
                var isGoogleUserToken = apiResult["isGoogleUser"]?.Value<bool>() ?? false;

                Response.Cookies.Append("authToken", jwtToken, new CookieOptions
                {
                    MaxAge = TimeSpan.FromMinutes(30),
                    Path = "/"
                });

                Response.Cookies.Append("isGoogleUser", isGoogleUserToken.ToString().ToLower(), new CookieOptions
                {
                    MaxAge = TimeSpan.FromMinutes(30),
                    Path = "/"
                });

                if (apiResult["isNewUser"]?.Value<bool>() == true)
                {
                    return RedirectToAction("Profile");
                }
                return RedirectToAction("Catalog");
            }

            TempData["GoogleError"] = "Authentication failed. Please try again.";
            return RedirectToAction("Login");
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Orders()
        {
            return View();
        }

        public IActionResult Catalog()
        {
            return View();
        }

        public IActionResult Wishlist()
        {
            return View();
        }

        public IActionResult Checkout()
        {
            return View();
        }

        public IActionResult OrderTracking(string id)
        {
            ViewBag.OrderId = id ?? "ORD001";
            return View();
        }

        public IActionResult Payments()
        {
            return View();
        }

        public IActionResult Profile()
        {
            var isGoogleUser = Request.Cookies["isGoogleUser"];
            ViewBag.IsGoogleUser = isGoogleUser == "True";
            return View();
        }

        public IActionResult ForgetPassword()
        {
            return View();
        }

        public IActionResult ResetPassword(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; }
        }

        // ========== ELASTICSEARCH SEARCH METHODS ==========


        [HttpPost]
        [Route("Vendor/SearchCatalog")]
        public async Task<IActionResult> SearchCatalog([FromBody] SearchRequestModel request)
        {
            try
            {
                var token = Request.Cookies["authToken"]; // ✅ Cookie se token lo
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsync(
                    $"{_apiBase}/api/Vendor/search/vendor-catalog", content);
                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchCatalog failed");
                return Json(new { success = false, results = new List<CatalogSearchResult>() });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SearchMyOrders([FromBody] SearchRequestModel request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBase}/api/Vendor/search/my-orders", content);
                var result = await response.Content.ReadAsStringAsync();

                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchMyOrders failed");
                return Json(new { success = false, results = new List<OrderSearchResult>() });
            }
        }


    }
}