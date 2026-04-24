using System.Net.Http;

public class SerpApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public SerpApiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<string> SearchGoogle(string query)
    {
        var apiKey = _config["SerpApi:ApiKey"];

        var url = $"https://serpapi.com/search.json?engine=google&q={query}&api_key={apiKey}";

        var response = await _httpClient.GetAsync(url);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}