using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace API.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;
        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Generates JWT token with configurable claims
        /// Token expiry: From appsettings.json Jwt:ExpiryMinutes (default 30)
        /// Algorithm: HS256 (HMAC SHA256)
        /// Used by: All authentication services (VendorAuthService, etc.)
        /// </summary>
        public string GenerateJwtToken(int userId, string email, string role, Dictionary<string, string>? additionalClaims = null)
        {
            try
            {
                var jwtKey = _configuration["Jwt:Key"];
                var jwtIssuer = _configuration["Jwt:Issuer"];
                var jwtAudience = _configuration["Jwt:Audience"];
                var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30");

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                // Build standard claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role)
                };

                // Add any additional user-type specific claims
                if (additionalClaims != null)
                {
                    foreach (var claim in additionalClaims)
                    {
                        claims.Add(new Claim(claim.Key, claim.Value));
                    }
                }

                // Create and sign JWT token
                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                    signingCredentials: credentials
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating JWT token: {ex.Message}", ex);
            }

        }
    }
}