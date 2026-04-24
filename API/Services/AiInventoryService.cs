using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace API.Services
{
    public class AiInventoryService
    {
        private readonly HttpClient _httpClient;

        public AiInventoryService(HttpClient httpClient)
        {
            _httpClient = httpClient; // <--- ADD THIS EXACT LINE!
            _httpClient.BaseAddress = new Uri("http://127.0.0.1:8001/"); 
        }

        public async Task<object> GetAiInventoryDataAsync()
        {
            try
            {
                // Call the Python API
                var response = await _httpClient.GetAsync("api/inventory-intelligence");

                if (response.IsSuccessStatusCode)
                {
                    // Read the JSON directly and pass it back
                    var jsonString = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<object>(jsonString);
                }
                
                return new { success = false, message = "Python API returned an error." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Python connection failed: {ex.Message}");
                return new { success = false, message = "Ensure Python Flask API is running on port 5001." };
            }
        }
    }
}