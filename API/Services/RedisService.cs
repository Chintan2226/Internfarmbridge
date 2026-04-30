using System.Text.Json;
using StackExchange.Redis;

namespace API.Services
{
    public class RedisService
    {
        private readonly IDatabase _db;

        // ✅ Define global options for consistency
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public RedisService(IDatabase db)
        {
            _db = db;
        }

          //
        public async Task SetOtpAsync(string email, string otp)
        {
            await _db.StringSetAsync($"otp:{email}", otp, TimeSpan.FromMinutes(5));
        }

        //
        public async Task<string?> GetOtpAsync(string email)
        {
            return await _db.StringGetAsync($"otp:{email}");
        }


        //
        public async Task RemoveOtpAsync(string email)
        {
            await _db.KeyDeleteAsync($"otp:{email}");
        }

        //
        public async Task SetUserAsync(string email, string userJson)
        {
            await _db.StringSetAsync($"user:{email}", userJson, TimeSpan.FromMinutes(5));
        }

        //

        public async Task<string?> GetUserAsync(string email)
        {
            return await _db.StringGetAsync($"user:{email}");
        }

        //

        public async Task RemoveUserAsync(string email)
        {
            await _db.KeyDeleteAsync($"user:{email}");
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return default;
            // ✅ FIX: Pass _options here
            return JsonSerializer.Deserialize<T>(value!, _options);
        }

        public async Task DeleteAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task DeleteKeyAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task DeleteByPatternAsync(string pattern)
        {
            var endpoints = _db.Multiplexer.GetEndPoints();
            if (endpoints.Length > 0)
            {
                var server = _db.Multiplexer.GetServer(endpoints.First());
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0)
                {
                    await _db.KeyDeleteAsync(keys);
                }
            }
        }


        public async Task SetAsync<T>(string key, T data, TimeSpan expiry)
        {
            var json = JsonSerializer.Serialize(data); // Serialize uses the attributes in the model
            await _db.StringSetAsync(key, json, expiry);
        }

        public async Task AppendToListAsync<T>(string key, T newItem, int maxItems = 50)
        {
            var existingData = await GetAsync<List<T>>(key) ?? new List<T>();
            existingData.Insert(0, newItem);
            var updatedList = existingData.Take(maxItems).ToList();
            await SetAsync(key, updatedList, TimeSpan.FromDays(7));
        }
    }
}
