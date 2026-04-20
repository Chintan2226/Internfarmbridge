using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MVC.Controllers;
using Newtonsoft.Json;

namespace MVC.Services
{
     public class AuthApiService
    {
        private readonly HttpClient       _httpClient;
        private readonly IConfiguration   _config;
 
        public AuthApiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config     = config;
        }
 
        /// <summary>
        /// Calls POST /api/auth/google on the API project.
        /// Returns raw JSON string so the caller can deserialise as needed.
        /// </summary>
        public async Task<string> GoogleLogin(GoogleUserDto dto)
        {
            var apiBase  = _config["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5020";
            var endpoint = $"{apiBase}/api/auth/google";
 
            var payload = JsonConvert.SerializeObject(new
            {
                email      = dto.Email,
                name       = dto.Name,
                providerId = dto.ProviderId,
                role       = dto.Role
            });
 
            var content  = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            var body     = await response.Content.ReadAsStringAsync();
 
            if (!response.IsSuccessStatusCode)
                throw new Exception($"API returned {(int)response.StatusCode}: {body}");
 
            return body;
        }
    }
}