using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.BAL;
using API.Models.Auth;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffAuthController : ControllerBase
    {
        private readonly StaffAuthHelper _staffHelper;
        private readonly JwtService _jwtService;
        private readonly IConfiguration _configuration;

        public StaffAuthController(
            StaffAuthHelper staffHelper,
            JwtService jwtService,
            IConfiguration configuration)
        {
            _staffHelper = staffHelper;
            _jwtService = jwtService;
            _configuration = configuration;
        }

        // ── POST /api/staff/login ─────────────────────────────────────────
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] vm_StaffLogin model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid login data." });

                // 1. Fetch user by email.
                //    Role is read from t_users.c_role — the client sends NO role hint.
                var (user, profileName) = await _staffHelper.GetStaffByEmailAsync(model.Email.Trim().ToLower());

                if (user == null)
                    return Unauthorized(new { success = false, message = "Invalid email or password." });

                // 2. Confirm role is staff-level (admin or field_officer).
                //    This is enforced from the DB value — the client has no input here.

                var role = user.Role.Trim().ToLower();

                if (role != "admin" && role != "field_officer")
                    return Unauthorized(new { success = false, message = "Access denied. This portal is for staff only." });

                // NOTE: ExpectedRole check removed entirely.
                //       The role is always sourced from t_users.c_role, never from the client.

                // 3. Account status checks
                if (!user.IsActive)
                    return Unauthorized(new { success = false, message = "Your account has been deactivated. Contact the system administrator." });

                if (!user.IsApproved)
                    return Unauthorized(new { success = false, message = "Your account is pending approval. Please wait for an administrator to activate it." });

                // 4. Verify password
                if (!_staffHelper.VerifyPassword(model.Password, user.PasswordHash))
                    return Unauthorized(new { success = false, message = "Invalid email or password." });

                // 5. Build role-specific additional claims from the matching profile table.
                //    Role determines which profile table is queried — not the client.
                var additionalClaims = new Dictionary<string, string>();

                if (user.Role == "admin")
                {
                    // Fetch admin_id from t_admin_profiles for JWT claim
                    var adminId = await _staffHelper.GetAdminProfileIdAsync(user.Id);
                    if (adminId.HasValue)
                        additionalClaims["admin_id"] = adminId.Value.ToString();
                }
                else // field_officer
                {
                    // Fetch fo_id, warehouse_id, assigned_region from t_field_officer_profiles
                    var foDetails = await _staffHelper.GetFieldOfficerDetailsAsync(user.Id);
                    if (foDetails != null)
                    {
                        additionalClaims["fo_id"] = foDetails.Id.ToString();
                        additionalClaims["warehouse_id"] = foDetails.WarehouseId.ToString();
                        additionalClaims["assigned_region"] = foDetails.AssignedRegion ?? "";
                    }
                }

                // 6. Generate JWT.
                //    The "role" claim is populated from t_users.c_role — the single source of truth.
                var token = _jwtService.GenerateJwtToken(
                    userId: user.Id,
                    email: user.Email,
                    role: user.Role,          // ← always from DB
                    additionalClaims: additionalClaims
                );

                return Ok(new
                {
                    success = true,
                    message = "Login successful.",
                    token,
                    role = user.Role,            // ← always from DB
                    userId = user.Id,
                    fullName = profileName,
                    email = user.Email,
                    expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error during login: {ex.Message}" });
            }
        }
    }
}