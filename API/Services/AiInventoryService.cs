using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace API.Services
{
    public class AiInventoryService
    {
        private readonly HttpClient _httpClient;

        public AiInventoryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<object> GetAiInventoryDataAsync()
        {
            var endpoints = new List<string>
            {
                "http://127.0.0.1:8001/api/inventory-intelligence",
                "http://127.0.0.1:8000/api/inventory-intelligence"
            };

            try
            {
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(endpoint);
                        if (!response.IsSuccessStatusCode) continue;

                        var jsonString = await response.Content.ReadAsStringAsync();
                        var parsed = JsonSerializer.Deserialize<object>(jsonString);
                        if (parsed != null) return parsed;
                    }
                    catch
                    {
                        // Try next endpoint
                    }
                }

                return new { success = false, message = "Python inventory API is not reachable on ports 8001 or 8000." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Python connection failed: {ex.Message}");
                return new { success = false, message = "Ensure Python FastAPI inventory service is running (8001 or 8000)." };
            }
        }
    }
}