using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MVC.Services
{
    public interface IUnsplashService
    {
        Task<string> GetPhotoUrlAsync(string query);
        Task<List<string>> GetPhotosUrlsAsync(string query, int count);
    }

    public class UnsplashService : IUnsplashService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UnsplashService> _logger;

        public UnsplashService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<UnsplashService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration; 
            _cache = cache;
            _logger = logger;

            string accessKey = _configuration["Unsplash:AccessKey"];
            if (!string.IsNullOrEmpty(accessKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Client-ID {accessKey}");
            }
        }

        public async Task<string> GetPhotoUrlAsync(string query)
        {
            var urls = await GetPhotosUrlsAsync(query, 1);
            return urls.FirstOrDefault() ?? "https://images.unsplash.com/photo-1500937386664-56d1dfef3854?w=800&q=80"; // fallback
        }

        public async Task<List<string>> GetPhotosUrlsAsync(string query, int count)
        {
            string cacheKey = $"Unsplash_{query}_{count}";
            if (_cache.TryGetValue(cacheKey, out List<string> cachedUrls))
            {
                return cachedUrls;
            }

            try
            {
                var response = await _httpClient.GetAsync($"https://api.unsplash.com/photos/random?query={Uri.EscapeDataString(query)}&count={count}&orientation=landscape");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(content);
                    
                    var urls = new List<string>();
                    
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in document.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("urls", out var urlsElement))
                            {
                                if (urlsElement.TryGetProperty("raw", out var rawElement))
                                {
                                    urls.Add(rawElement.GetString() + "&q=100&w=2560&auto=format");
                                }
                                else if (urlsElement.TryGetProperty("full", out var fullElement))
                                {
                                    urls.Add(fullElement.GetString());
                                }
                            }
                        }
                    }
                    else if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (document.RootElement.TryGetProperty("urls", out var urlsElement))
                        {
                            if (urlsElement.TryGetProperty("raw", out var rawElement))
                            {
                                urls.Add(rawElement.GetString() + "&q=100&w=2560&auto=format");
                            }
                            else if (urlsElement.TryGetProperty("full", out var fullElement))
                            {
                                urls.Add(fullElement.GetString());
                            }
                        }
                    }

                    if (urls.Any())
                    {
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromHours(12)); // Cache for 12 hours since we only have 50 requests/hour

                        _cache.Set(cacheKey, urls, cacheEntryOptions);
                        return urls;
                    }
                }
                else
                {
                    _logger.LogWarning($"Unsplash API returned {response.StatusCode}. Maybe rate limited.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from Unsplash API");
            }

            return new List<string>(); 
        }
    }
}
