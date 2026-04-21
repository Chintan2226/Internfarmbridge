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
 
        public AuthController(
            FarmerHelper farmerHelper,
            JwtService jwtService,
            IConfiguration configuration,
            EmailService emailService,
            GoogleAuthBal googleAuthBal)
        {
            _farmerHelper = farmerHelper;
            _jwtService = jwtService;
            _configuration = configuration;
            _emailService = emailService;
            _googleAuthBal = googleAuthBal;
        }
 
        // ─────────────────────────────────────────────
        // POST /api/farmer/register
        // ─────────────────────────────────────────────
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
 
                bool emailExists = await _farmerHelper.EmailExistsAsync(model.Email);
                if (emailExists)
                    return BadRequest(new { success = false, message = "Email already registered." });
 
                bool phoneExists = await _farmerHelper.PhoneExistsAsync(model.Phone);
                if (phoneExists)
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
                    // If the email fails (e.g. SMTP timeout), we just log it. 
                    // We don't want to fail the whole registration!
                    Console.WriteLine($"WARNING: Registration succeeded, but welcome email failed: {emailEx.Message}");
                }
 
                return Ok(new
                {
                    success = true,
                    message = "Registration successful! You can now log in.",
                    farmerId = farmerProfile.Id,
                    userId = farmerProfile.UserId,
                    email = model.Email
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during registration: {ex.Message}" });
            }
        }
 
        // ─────────────────────────────────────────────
        // POST /api/farmer/login
        // ─────────────────────────────────────────────
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] vm_FarmerLogin model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid login data." });
 
                User? user = null;
                FarmerProfile? farmer = null;
 
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
 
                // Generate JWT with farmer-specific claims
                var additionalClaims = new Dictionary<string, string>
                {
                    { "farmer_id", farmer.Id.ToString() },
                    { "full_name", farmer.FullName }
                };
 
                var token = _jwtService.GenerateJwtToken(user.Id, user.Email, user.Role, additionalClaims);
 
                return Ok(new
                {
                    success = true,
                    message = "Login successful.",
                    token = token,
                    role = user.Role,
                    farmerId = farmer.Id,
                    userId = user.Id,
                    fullName = farmer.FullName,
                    email = user.Email,
                    phone = farmer.Phone,
                    expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during login: {ex.Message}" });
            }
        }
 
        // ─────────────────────────────────────────────
        // GET /api/farmer/verify  — Verify JWT token
        // ─────────────────────────────────────────────
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
 
                var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
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
 
                var userId   = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email    = principal.FindFirst(ClaimTypes.Email)?.Value;
                var role     = principal.FindFirst(ClaimTypes.Role)?.Value;
                var farmerId = principal.FindFirst("farmer_id")?.Value;
 
                return Ok(new { success = true, userId, email, role, farmerId });
            }
            catch (SecurityTokenExpiredException)
            {
                return Unauthorized(new { success = false, message = "Token expired" });
            }
            catch
            {
                return Unauthorized(new { success = false, message = "Invalid token" });
            }
        }
 
        // ─────────────────────────────────────────────
        // POST /api/farmer/send-otp
        // ─────────────────────────────────────────────
        [AllowAnonymous]
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] vm_FarmerOtpRequest model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    return BadRequest(new { success = false, message = "Email is required." });
 
                // Always returns SUCCESS for security (don't reveal if email exists)
                await _farmerHelper.SendForgotPasswordOtpAsync(model.Email, _emailService);
 
                return Ok(new { success = true, message = "If the email is registered, an OTP has been sent." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error sending OTP: {ex.Message}" });
            }
        }
 
        // ─────────────────────────────────────────────
        // POST /api/farmer/reset-password
        // ─────────────────────────────────────────────
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] vm_FarmerResetPassword model)
        {
            try
            {
                if (model.NewPassword != model.ConfirmPassword)
                    return BadRequest(new { success = false, message = "Passwords do not match." });
 
                string result = await _farmerHelper.ResetFarmerPasswordAsync(
                    model.Email, model.Otp, model.NewPassword);
 
                if (result == "SUCCESS")
                    return Ok(new { success = true, message = "Password reset successful. Please log in." });
 
                return BadRequest(new { success = false, message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error resetting password: {ex.Message}" });
            }
        }
        // ─────────────────────────────────────────────
        // POST /api/auth/google-login
        // ─────────────────────────────────────────────
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
                return StatusCode(500, new { success = false, message = $"Google login failed: {ex.Message}" });
            }
        }
    }
}