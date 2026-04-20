using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API.BAL;
using API.Models.Vendor;  // ← Changed: removed .ViewModels
using API.Models.Auth;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class VendorController : ControllerBase
    {
        private readonly VendorHelper _vendorHelper;
        private readonly JwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly RedisService _redisService;
        private readonly EmailService _emailService;

        public VendorController(VendorHelper vendorHelper, JwtService jwtService, IConfiguration configuration, RedisService redisService, EmailService emailService)
        {
            _vendorHelper = vendorHelper;
            _jwtService = jwtService;
            _configuration = configuration;
            _redisService = redisService;
            _emailService = emailService;
        }

        private int CurrentVendorId
        {
            get
            {
                // We use "vendor_id" because that's what you saved during login
                var claim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                return int.TryParse(claim, out int id) ? id : 0;
            }
        }

        //login register vendor jwt,token verify
        /// Register new vendor account
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] vm_VendorRegister model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid registration data.", errors = ModelState.Values.SelectMany(v => v.Errors) });

                bool emailExists = await _vendorHelper.EmailExistsAsync(model.Email);
                if (emailExists)
                    return BadRequest(new { success = false, message = "Email already registered." });

                bool phoneExists = await _vendorHelper.PhoneExistsAsync(model.Phone);
                if (phoneExists)
                    return BadRequest(new { success = false, message = "Phone number already registered." });

                string hashedPassword = _vendorHelper.HashPassword(model.Password);

                var vendorProfile = await _vendorHelper.RegisterVendorAsync(
                    model.Email, hashedPassword, model.BusinessName, model.ContactPerson, model.Phone, model.Gstin
                );

                // ✅ TRIGGER THE REAL WELCOME EMAIL
                await _emailService.SendVendorWelcomeEmailAsync(model.Email, model.BusinessName);

                return Ok(new
                {
                    success = true,
                    message = "Vendor registered successfully. Please verify your email and login.",
                    vendorId = vendorProfile.Id,
                    userId = vendorProfile.UserId,
                    email = model.Email
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during registration: {ex.Message}" });
            }
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] vm_VendorLogin model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid login data.", errors = ModelState.Values.SelectMany(v => v.Errors) });

                User user = null;
                VendorProfile vendor = null;

                if (model.EmailOrPhone.Contains("@"))
                {
                    (user, vendor) = await _vendorHelper.GetVendorByEmailAsync(model.EmailOrPhone);
                }
                else
                {
                    (user, vendor) = await _vendorHelper.GetVendorByPhoneAsync(model.EmailOrPhone);
                }

                if (user == null || vendor == null)
                    return Unauthorized(new { success = false, message = "Invalid email/phone or password." });

                if (!user.IsActive)
                    return Unauthorized(new { success = false, message = "Account is inactive." });

                //Verify password using BCrypt
                if (!_vendorHelper.VerifyPassword(model.Password, user.PasswordHash))
                    return Unauthorized(new { success = false, message = "Invalid email/phone or password." });

                // Role is automatically included in token by JwtService
                var additionalClaims = new Dictionary<string, string>(); // Removed duplicate NameIdentifier
                var token = _jwtService.GenerateJwtToken(user.Id, user.Email, user.Role, additionalClaims);

                // Return JWT token on successful login with role included
                return Ok(new
                {
                    success = true,
                    message = "Login successful.",
                    token = token,
                    role = user.Role,
                    vendorId = vendor.Id,
                    userId = user.Id,
                    businessName = vendor.BusinessName,
                    email = user.Email,
                    phone = vendor.Phone,
                    expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30"),
                    isGoogleUser = false   // ✅ ADD THIS
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during login: {ex.Message}" });
            }
        }

        [AllowAnonymous]
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthRequest request)
        {
            var (vendor, isNewUser) = await _vendorHelper.RegisterVendorGoogleAsync(request.Email, request.Name, request.ProviderId);
            
            if (vendor != null)
            {
                // ✅ TRIGGER EMAIL ONLY IF THEY ARE A NEW USER
                if (isNewUser)
                {
                    // We use their Google Name as the Business Name since they didn't fill out a form
                    await _emailService.SendVendorWelcomeEmailAsync(request.Email, request.Name);
                }

                var token = _jwtService.GenerateJwtToken(vendor.UserId, request.Email, "vendor");
                return Ok(new { success = true, token = token, isGoogleUser = true, isNewUser = isNewUser });
            }
            
            return Unauthorized(new { message = "Authentication failed" });
        }

        // 
        [HttpGet("verify")]
        public IActionResult VerifyToken()
        {
            try
            {
                // 🔹 1. Get token from Authorization header
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();

                if (authHeader == null || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Token missing"
                    });
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // 🔹 2. Validate Token
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    ClockSkew = TimeSpan.Zero // no delay in expiry
                };

                ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // 🔹 3. Extract Claims
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = principal.FindFirst(ClaimTypes.Email)?.Value;
                var role = principal.FindFirst(ClaimTypes.Role)?.Value;
                var vendorId = principal.FindFirst("vendor_id")?.Value;

                // 🔹 4. Return response
                return Ok(new
                {
                    success = true,
                    userId,
                    email,
                    role,
                    vendorId
                });
            }
            catch (SecurityTokenExpiredException)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Token expired"
                });
            }
            catch (Exception)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid token"
                });
            }
        }


        // ============ CATALOG ============
        [HttpGet("catalog")]
        public async Task<IActionResult> GetCatalog(
             [FromQuery] string category = "all",
             [FromQuery] string search = "",
             [FromQuery] string grade = "all")  // ← GRADE PARAMETER ADD KARO
        {
            var filter = new VM_CropFilter
            {
                CropType = string.IsNullOrEmpty(search) ? null : search,
                Category = category == "all" ? null : category,  // ← CATEGORY PROPERLY SET
                Grade = grade == "all" ? null : grade            // ← GRADE PROPERLY SET
            };
            var products = await _vendorHelper.GetFilteredCatalogAsync(filter);
            return Ok(new { success = true, data = products });
        }

        [HttpPost("catalog/filter")]
        public async Task<IActionResult> FilterCatalog([FromBody] VM_CropFilter filter)
        {
            var products = await _vendorHelper.GetFilteredCatalogAsync(filter);
            return Ok(new { success = true, data = products });
        }

        // ============ CART ============
        [HttpGet("cart")]
        public async Task<IActionResult> GetCart()
        {
            // 🔹 FIX 2: Removed hardcoded '4', using CurrentUserId
            return Ok(new { success = true, data = await _vendorHelper.GetCartSummaryAsync(CurrentVendorId) });
        }

        [HttpPost("cart/add")]
        public async Task<IActionResult> AddToCart([FromBody] VM_AddToCartRequest req)
        {
            // 🔹 FIX 3: Passing CurrentUserId (The BAL helper handles finding the profile id via subquery)
            var result = await _vendorHelper.AddItemToCartAsync(CurrentVendorId, req.CropId, req.Quantity);

            // ✅ Clear KPI cache (because cart count changed)
            if (result == "Success")
            {
                await _redisService.RemoveUserAsync($"vendor:dashboard:kpi:{CurrentVendorId}");
            }

            if (result == "Success")
                return Ok(new { success = true, message = "Item added to cart" });
            return BadRequest(new { success = false, message = result });
        }
        [HttpDelete("cart/remove/{cartId}")]
        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            var result = await _vendorHelper.RemoveCartItemAsync(CurrentVendorId, cartId);
            if (result == "Success")
                return Ok(new { success = true });
            return BadRequest(new { success = false, message = result });
        }

        // ============ ORDERS ============
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] string status = "all")
        {
            var orders = await _vendorHelper.GetOrderHistoryAsync(CurrentVendorId);
            if (status != "all")
                orders = orders.FindAll(o => o.Status == status);
            return Ok(new { success = true, data = orders });
        }

        [HttpGet("orders/history")]
        public async Task<IActionResult> GetOrderHistory()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetOrderHistoryAsync(CurrentVendorId) });
        }

        [HttpPost("orders/cancel")]
        public async Task<IActionResult> CancelOrder([FromBody] VM_CancelOrderRequest request)
        {
            var result = await _vendorHelper.CancelOrderAsync(CurrentVendorId, request.OrderId, request.Reason);

            if (result == "Success")
            {
                await _redisService.RemoveUserAsync($"vendor:dashboard:kpi:{CurrentVendorId}");
                await _redisService.RemoveUserAsync($"vendor:dashboard:stats:{CurrentVendorId}");
                await _redisService.RemoveUserAsync($"vendor:dashboard:monthly:{CurrentVendorId}");
                await _redisService.RemoveUserAsync($"vendor:dashboard:category:{CurrentVendorId}");

                // ✅ SAFE FIX: Convert the string OrderId to an int for the EmailService
                int.TryParse(request.OrderId, out int parsedOrderId);

                // ✅ TRIGGER CANCEL EMAIL
                var profile = await _vendorHelper.GetProfileAsync(CurrentVendorId);
                await _emailService.SendVendorOrderCancelledEmailAsync(
                    profile?.Email,
                    profile?.BusinessName,
                    parsedOrderId, // <-- Passes the converted number!
                    0m, 
                    request.Reason
                );

                return Ok(new { success = true, message = "Order cancelled successfully." });
            }
            return BadRequest(new { success = false, message = result });
        }

        [HttpPost("orders/repeat")]
        public async Task<IActionResult> RepeatOrder([FromBody] string orderId)
        {
            var result = await _vendorHelper.RepeatOrderAsync(CurrentVendorId, orderId);
            if (result.StartsWith("Success"))
                return Ok(new { success = true, message = result });
            return BadRequest(new { success = false, message = result });
        }

        [HttpGet("orders/track/{orderId}")]
        public async Task<IActionResult> TrackOrder(string orderId)
        {
            var tracking = await _vendorHelper.GetOrderTrackingAsync(orderId, CurrentVendorId);
            if (tracking == null)
                return NotFound(new { success = false, message = "Order not found" });
            return Ok(new { success = true, data = tracking });
        }

        // ============ ADDRESSES ============
        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetAddressesAsync(CurrentVendorId) });
        }

        [HttpPost("addresses/save")]
        public async Task<IActionResult> SaveAddress([FromBody] VM_SaveAddressRequest request)
        {
            var result = await _vendorHelper.SaveAddressAsync(CurrentVendorId, request);
            return Ok(result);
        }

        // ============ CHECKOUT ============
        [HttpPost("checkout/place-order")]
        public async Task<IActionResult> PlaceOrder([FromBody] VM_PlaceOrderRequest request)
        {
            var result = await _vendorHelper.PlaceOrderAsync(CurrentVendorId, request);
            if (result.Success)
            {
                await _redisService.RemoveUserAsync($"vendor:dashboard:kpi:{CurrentVendorId}");
                await _redisService.RemoveUserAsync($"vendor:dashboard:stats:{CurrentVendorId}");
                await _redisService.RemoveUserAsync($"vendor:dashboard:monthly:{CurrentVendorId}");
                await _redisService.RemoveUserAsync($"vendor:dashboard:category:{CurrentVendorId}");

                // ✅ TRIGGER ORDER SUCCESS EMAIL
                var profile = await _vendorHelper.GetProfileAsync(CurrentVendorId);
                await _emailService.SendVendorOrderSuccessEmailAsync(
                    profile?.Email, 
                    profile?.BusinessName, 
                    0, // We will just pass 0 for now until you connect it to your Order ID
                    0m, 
                    "Registered Address", // Fixed compiler error
                    "Standard Delivery",  // Fixed compiler error
                    "Order Confirmed"
                );
            }
            return Ok(result);
        }

        // ============ PAYMENTS ============
        [HttpGet("payments")]
        public async Task<IActionResult> GetPayments()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetPaymentHistoryAsync(CurrentVendorId) });
        }

        // ============ PROFILE ============
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await _vendorHelper.GetProfileAsync(CurrentVendorId);
            var stats = await _vendorHelper.GetUserKpiStatsAsync(CurrentVendorId);

            if (profile == null)
                return NotFound(new { success = false, message = "Profile not found" });

            return Ok(new { success = true, data = profile, stats });
        }

        [HttpPut("profile/update")]
        public async Task<IActionResult> UpdateProfile([FromBody] VM_UpdateProfileRequest request)
        {
            var result = await _vendorHelper.UpdateProfileAsync(CurrentVendorId, request);
            if (result.Contains("successfully"))
                return Ok(new { success = true, message = result });
            return BadRequest(new { success = false, message = result });
        }

        [HttpPost("profile/upload-photo")]
        public async Task<IActionResult> UploadProfilePhoto([FromBody] VM_PhotoUploadRequest request)
        {
            var result = await _vendorHelper.UploadProfilePhotoAsync(CurrentVendorId, request.ImageBase64);
            if (result.Contains("successfully"))
                return Ok(new { success = true, message = result, imageUrl = request.ImageBase64 });
            return BadRequest(new { success = false, message = result });
        }

        [HttpPost("profile/change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] VM_ChangePasswordRequest request)
        {
            var result = await _vendorHelper.ChangePasswordAsync(CurrentVendorId, request.CurrentPassword, request.NewPassword);
            if (result.Contains("successfully"))
                return Ok(new { success = true, message = result });
            return BadRequest(new { success = false, message = result });
        }

        // ============ WISHLIST ============

        [HttpGet("wishlist")]
        public async Task<IActionResult> GetWishlist()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetWishlistAsync(CurrentVendorId) });
        }

        [HttpPost("wishlist/add/{productId}")]
        public async Task<IActionResult> AddToWishlist(int productId, [FromQuery] string grade = null)
        {
            var result = await _vendorHelper.AddToWishlistAsync(CurrentVendorId, productId, grade);

            // ✅ Clear KPI cache (because wishlist count changed)
            if (result.Contains("Added"))
            {
                await _redisService.RemoveUserAsync($"vendor:dashboard:kpi:{CurrentVendorId}");
            }
            return Ok(new { success = true, message = result });
        }

        [HttpDelete("wishlist/remove/{productId}")]
        public async Task<IActionResult> RemoveFromWishlist(int productId, [FromQuery] string grade = null)
        {
            var result = await _vendorHelper.RemoveFromWishlistAsync(CurrentVendorId, productId, grade);

            // ✅ Clear KPI cache (because wishlist count changed)
            if (result.Contains("Removed"))
            {
                await _redisService.RemoveUserAsync($"vendor:dashboard:kpi:{CurrentVendorId}");
            }

            return Ok(new { success = true, message = result });
        }

        [HttpGet("wishlist/check/{productId}")]
        public async Task<IActionResult> IsInWishlist(int productId, [FromQuery] string grade = null)
        {
            var isInWishlist = await _vendorHelper.IsInWishlistAsync(CurrentVendorId, productId, grade);
            return Ok(new { success = true, isInWishlist = isInWishlist });
        }

        // ============ USER STATS ============
        [HttpGet("user/stats")]
        public async Task<IActionResult> GetUserStats()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetUserKpiStatsAsync(CurrentVendorId) });
        }

        [HttpGet("user/recent-orders")]
        public async Task<IActionResult> GetUserRecentOrders()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetRecentOrdersAsync(CurrentVendorId) });
        }

        // ============ DASHBOARD ============
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            return Ok(new { success = true, data = await _vendorHelper.GetDashboardStatsAsync(CurrentVendorId) });
        }

        // ============ RAZORPAY PAYMENT ============

        [HttpPost("payment/create-order")]
        public async Task<IActionResult> CreateRazorpayOrder([FromBody] CreateRazorpayOrderRequest request)
        {
            var result = await _vendorHelper.CreateRazorpayOrderAsync(CurrentVendorId, request.OrderId, request.Amount);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("payment/verify")]
        public async Task<IActionResult> VerifyRazorpayPayment([FromBody] VM_RazorpayPaymentVerification verification)
        {
            var result = await _vendorHelper.VerifyRazorpayPaymentAsync(CurrentVendorId, verification);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }


        [HttpPut("addresses/update/{id}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] VM_SaveAddressRequest request)
        {
            var result = await _vendorHelper.UpdateAddressAsync(CurrentVendorId, id, request);
            return Ok(result);
        }

        [HttpDelete("addresses/delete/{id}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var result = await _vendorHelper.DeleteAddressAsync(CurrentVendorId, id);
            return Ok(result);
        }

        // ============ DASHBOARD KPI WITH CACHE ============

        [HttpGet("dashboard/kpi")]
        public async Task<IActionResult> GetDashboardKpi()
        {
            string cacheKey = $"vendor:dashboard:kpi:{CurrentVendorId}";

            try
            {
                // ✅ 1. Check cache
                var cachedData = await _redisService.GetAsync<VM_UserKpiStats>(cacheKey);

                if (cachedData != null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = cachedData,
                        source = "cache"
                    });
                }

                // ❌ Not in cache → fetch from DB (existing method)
                var kpi = await _vendorHelper.GetUserKpiStatsAsync(CurrentVendorId);

                // ✅ 2. Store in cache (30 minutes)
                await _redisService.SetAsync(cacheKey, kpi, TimeSpan.FromMinutes(30));

                return Ok(new
                {
                    success = true,
                    data = kpi,
                    source = "db"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============ DASHBOARD STATS WITH CACHE (For Graphs) ============

        [HttpGet("dashboard/stats-cached")]
        public async Task<IActionResult> GetDashboardStatsCached()
        {
            string cacheKey = $"vendor:dashboard:stats:{CurrentVendorId}";

            try
            {
                // ✅ 1. Check cache
                var cachedData = await _redisService.GetAsync<VM_DashboardStats>(cacheKey);

                if (cachedData != null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = cachedData,
                        source = "cache"
                    });
                }

                // ❌ Not in cache → fetch from DB (existing method)
                var stats = await _vendorHelper.GetDashboardStatsAsync(CurrentVendorId);

                // ✅ 2. Store in cache (30 minutes)
                await _redisService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(30));

                return Ok(new
                {
                    success = true,
                    data = stats,
                    source = "db"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============ MONTHLY PURCHASE TRENDS (New Method) ============

        [HttpGet("dashboard/monthly-trends")]
        public async Task<IActionResult> GetMonthlyPurchaseTrends()
        {
            string cacheKey = $"vendor:dashboard:monthly:{CurrentVendorId}";

            try
            {
                var cachedData = await _redisService.GetAsync<VM_MonthlyTrends>(cacheKey);
                if (cachedData != null)
                {
                    return Ok(new { success = true, data = cachedData, source = "cache" });
                }

                var trends = await _vendorHelper.GetMonthlyPurchaseTrendsAsync(CurrentVendorId);
                await _redisService.SetAsync(cacheKey, trends, TimeSpan.FromMinutes(30));

                return Ok(new { success = true, data = trends, source = "db" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============ SPENDING BY CATEGORY (New Method) ============

        [HttpGet("dashboard/category-spending")]
        public async Task<IActionResult> GetCategorySpending()
        {
            string cacheKey = $"vendor:dashboard:category:{CurrentVendorId}";

            try
            {
                var cachedData = await _redisService.GetAsync<List<VM_CategorySpending>>(cacheKey);
                if (cachedData != null)
                {
                    return Ok(new { success = true, data = cachedData, source = "cache" });
                }

                var spending = await _vendorHelper.GetCategorySpendingAsync(CurrentVendorId);
                await _redisService.SetAsync(cacheKey, spending, TimeSpan.FromMinutes(30));

                return Ok(new { success = true, data = spending, source = "db" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        public class CreateRazorpayOrderRequest
        {
            public int OrderId { get; set; }
            public decimal Amount { get; set; }
        }
    }

}