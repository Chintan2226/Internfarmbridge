using Npgsql;
using API.Models.Notification;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace API.BAL
{
    public class NotificationHelper
    {
        private readonly NpgsqlConnection _conn;
        private readonly IDistributedCache _cache;
        private readonly ILogger<NotificationHelper> _logger;

        public NotificationHelper(NpgsqlConnection conn, IDistributedCache cache, ILogger<NotificationHelper> logger)
        {
            _conn = conn;
            _cache = cache;
            _logger = logger;
        }

        private async Task EnsureConnectionAsync()
        {
            if (_conn.State != System.Data.ConnectionState.Open)
                await _conn.OpenAsync();
        }

        private static string CacheKey(int userId) => $"notif:unread:{userId}";

        public async Task<int> CreateNotificationAsync(int userId, string title, string body, string type, string? refType = null, int? refId = null)
        {
            try
            {
                await EnsureConnectionAsync();

                const string sql = @"
                    INSERT INTO t_notifications 
                    (c_user_id, c_type, c_title, c_body, c_reference_type, c_reference_id, c_is_read, c_created_at)
                    VALUES (@uid, @type, @title, @body, @rtype, @rid, false, NOW() AT TIME ZONE 'UTC')
                    RETURNING c_id";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@body", body);
                cmd.Parameters.AddWithValue("@rtype", refType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@rid", refId ?? (object)DBNull.Value);

                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                await _cache.RemoveAsync(CacheKey(userId));
                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateNotification failed for {UserId}", userId);
                throw;
            }
        }

        public async Task<List<vm_Notification>> GetUserNotificationsAsync(int userId)
        {
            var list = new List<vm_Notification>();

            try
            {
                await EnsureConnectionAsync();

                const string sql = @"
                    SELECT c_id, c_title, c_body, c_type, c_reference_type, c_reference_id, c_is_read, c_created_at
                    FROM t_notifications
                    WHERE c_user_id = @uid
                    ORDER BY c_created_at DESC
                    LIMIT 100";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new vm_Notification
                    {
                        Id = reader.GetInt32(0).ToString(),
                        Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Message = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Type = reader.IsDBNull(3) ? "Info" : reader.GetString(3),
                        ReferenceType = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ReferenceId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        IsRead = reader.GetBoolean(6),
                        CreatedAt = reader.GetDateTime(7)
                    });
                }

                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserNotifications failed for {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            var key = CacheKey(userId);

            try
            {
                var cached = await _cache.GetStringAsync(key);
                if (cached != null && int.TryParse(cached, out var val))
                    return val;

                await EnsureConnectionAsync();

                using var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM t_notifications WHERE c_user_id=@uid AND c_is_read=false", _conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                await _cache.SetStringAsync(key, count.ToString(),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUnreadCount failed for {UserId}", userId);
                throw;
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            try
            {
                await EnsureConnectionAsync();

                using var cmd = new NpgsqlCommand(
                    "UPDATE t_notifications SET c_is_read=true WHERE c_user_id=@uid AND c_is_read=false", _conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                var updated = await cmd.ExecuteNonQueryAsync();

                if (updated > 0)
                {
                    var key = CacheKey(userId);
                    await _cache.SetStringAsync(key, "0",
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarkAllAsRead failed for {UserId}", userId);
                throw;
            }
        }

        public async Task MarkAsReadAsync(int userId, int notificationId)
        {
            try
            {
                await EnsureConnectionAsync();

                using var cmd = new NpgsqlCommand(
                    "UPDATE t_notifications SET c_is_read=true WHERE c_id=@id AND c_user_id=@uid AND c_is_read=false",
                    _conn);

                cmd.Parameters.AddWithValue("@id", notificationId);
                cmd.Parameters.AddWithValue("@uid", userId);

                if (await cmd.ExecuteNonQueryAsync() > 0)
                    await _cache.RemoveAsync(CacheKey(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarkAsRead failed for {UserId}", userId);
                throw;
            }
        }

        public async Task DeleteAllAsync(int userId)
        {
            try
            {
                await EnsureConnectionAsync();

                using var cmd = new NpgsqlCommand(
                    "DELETE FROM t_notifications WHERE c_user_id=@uid", _conn);

                cmd.Parameters.AddWithValue("@uid", userId);
                await cmd.ExecuteNonQueryAsync();

                await _cache.RemoveAsync(CacheKey(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAll failed for {UserId}", userId);
                throw;
            }
        }

        public async Task DeleteOneAsync(int userId, int notificationId)
        {
            try
            {
                await EnsureConnectionAsync();

                using var check = new NpgsqlCommand(
                    "SELECT c_is_read FROM t_notifications WHERE c_id=@id AND c_user_id=@uid", _conn);
                check.Parameters.AddWithValue("@id", notificationId);
                check.Parameters.AddWithValue("@uid", userId);

                var wasUnread = check.ExecuteScalar() is bool isRead && !isRead;

                using var cmd = new NpgsqlCommand(
                    "DELETE FROM t_notifications WHERE c_id=@id AND c_user_id=@uid", _conn);
                cmd.Parameters.AddWithValue("@id", notificationId);
                cmd.Parameters.AddWithValue("@uid", userId);

                await cmd.ExecuteNonQueryAsync();

                if (wasUnread)
                    await _cache.RemoveAsync(CacheKey(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteOne failed for {UserId}", userId);
                throw;
            }
        }
    }
}