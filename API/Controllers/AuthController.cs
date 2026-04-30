using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API.BAL;
using API.Models.Auth;
using API.Models.Farmer;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")] 
    public class AuthController : ControllerBase
    {
        private readonly FarmerHelper _farmerHelper;
        private readonly JwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly GoogleAuthBal _googleAuthBal;
        private readonly AdminHelper _adminHelper;
        private readonly RabbitMqService _rabbitMqService;

        public AuthController(
            FarmerHelper farmerHelper,
            JwtService jwtService,
            IConfiguration configuration,
            EmailService emailService,
            GoogleAuthBal googleAuthBal,
            AdminHelper adminHelper,
            RabbitMqService rabbitMqService)
        {
            _farmerHelper = farmerHelper;
            _jwtService = jwtService;
            _configuration = configuration;
            _emailService = emailService;
            _googleAuthBal = googleAuthBal;
            _adminHelper = adminHelper;
            _rabbitMqService = rabbitMqService;
        }

        // Farmer Registration
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] vm_FarmerRegister model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid registration data.",
                        errors = ModelState.Values.SelectMany(v => v.Errors)
                    });

                if (model.Password != model.ConfirmPassword)
                    return BadRequest(new { success = false, message = "Passwords do not match." });

                if (await _farmerHelper.EmailExistsAsync(model.Email))
                    return BadRequest(new { success = false, message = "Email already registered." });

                if (await _farmerHelper.PhoneExistsAsync(model.Phone))
                    return BadRequest(new { success = false, message = "Phone number already registered." });

                string hashedPassword = _farmerHelper.HashPassword(model.Password);

                var farmerProfile = await _farmerHelper.RegisterFarmerAsync(
                    model.Email,
                    hashedPassword,
                    model.FullName,
                    model.Phone,
                    model.Address ?? "",
                    model.State ?? "",
                    model.District ?? ""
                );
                
                try
                {
                    await _emailService.SendFarmerWelcomeEmailAsync(model.Email, model.FullName);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"WARNING: Welcome email failed: {emailEx.Message}");
                }

                await _rabbitMqService.PublishToRoleAsync(
                    role: "admin",
                    title: "New Farmer Registered",
                    message: $"Farmer {model.FullName} ({model.Email}) has registered.",
                    type: "registration",
                    refType: "Farmer",
                    refId: farmerProfile.UserId
                );

                return Ok(new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Registration successful! You can now log in." },
                    { "farmerId", farmerProfile.Id },
                    { "userId", farmerProfile.UserId },
                    { "email", model.Email }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during registration: {ex.Message}" });
            }
        }

       // Farmer Login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] vm_FarmerLogin model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid login data." });

                User? user;
                FarmerProfile? farmer;

                if (model.EmailOrPhone.Contains("@"))
                    (user, farmer) = await _farmerHelper.GetFarmerByEmailAsync(model.EmailOrPhone);
                else
                    (user, farmer) = await _farmerHelper.GetFarmerByPhoneAsync(model.EmailOrPhone);

                if (user == null || farmer == null)
                    return Unauthorized(new { success = false, message = "Invalid email/phone or password." });

                if (!user.IsActive)
                    return Unauthorized(new { success = false, message = "Account is inactive. Please contact support." });

                if (!_farmerHelper.VerifyPassword(model.Password, user.PasswordHash))
                    return Unauthorized(new { success = false, message = "Invalid email/phone or password." });

                var additionalClaims = new Dictionary<string, string>
                {
                    { "farmer_id", farmer.Id.ToString() },
                    { "full_name", farmer.FullName },
                    { "profile_image_url", user.ProfileImageUrl ?? "" }
                };

                var token = _jwtService.GenerateJwtToken(user.Id, user.Email, user.Role, additionalClaims);

                // 👉 ADDED THIS BLOCK: Tell the frontend to show the Welcome Modal
                Response.Cookies.Append("ShowWelcomeGuide", "true", new CookieOptions { 
                    Expires = DateTimeOffset.UtcNow.AddMinutes(5), // Temporary cookie
                    Path = "/" // Applies to the whole site
                });

                return Ok(new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Login successful." },
                    { "token", token },
                    { "role", user.Role },
                    { "farmerId", farmer.Id },
                    { "userId", user.Id },
                    { "fullName", farmer.FullName },
                    { "email", user.Email },
                    { "phone", farmer.Phone },
                    { "profileImageUrl", user.ProfileImageUrl ?? "" },
                    { "expiryMinutes", int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30") }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during login: {ex.Message}" });
            }
        }
        // Verify Token
        [HttpGet("verify")]
        public IActionResult VerifyToken()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer "))
                    return Unauthorized(new { success = false, message = "Token missing" });

                var token = authHeader.Substring("Bearer ".Length).Trim();
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
                    ClockSkew = TimeSpan.Zero
                };

                ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = principal.FindFirst(ClaimTypes.Email)?.Value;
                var role = principal.FindFirst(ClaimTypes.Role)?.Value;
                var farmerId = principal.FindFirst("farmer_id")?.Value;

                return Ok(new { success = true, userId, email, role, farmerId });
            }
            catch (SecurityTokenExpiredException) { return Unauthorized(new { success = false, message = "Token expired" }); }
            catch { return Unauthorized(new { success = false, message = "Invalid token" }); }
        }

        // Forgot Password
        [AllowAnonymous]
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] vm_FarmerOtpRequest model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    return BadRequest(new { success = false, message = "Email is required." });

                await _farmerHelper.SendForgotPasswordOtpAsync(model.Email, _emailService);
                return Ok(new { success = true, message = "If the email is registered, an OTP has been sent." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error sending OTP: {ex.Message}" });
            }
        }

        // Reset Password
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] vm_FarmerResetPassword model)
        {
            try
            {
                if (model.NewPassword != model.ConfirmPassword)
                    return BadRequest(new { success = false, message = "Passwords do not match." });

                string result = await _farmerHelper.ResetFarmerPasswordAsync(model.Email, model.Otp, model.NewPassword);
                if (result == "SUCCESS")
                    return Ok(new { success = true, message = "Password reset successful. Please log in." });

                return BadRequest(new { success = false, message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error resetting password: {ex.Message}" });
            }
        }

        // Google Login
        [AllowAnonymous]
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleDto dto)
        {
            try
            {
                var result = await _googleAuthBal.GoogleLogin(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new Dictionary<string, object> { { "success", false }, { "message", $"Google login failed: {ex.Message}" } });
            }
        }
    }
}