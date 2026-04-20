using Npgsql;
using BC = BCrypt.Net.BCrypt;
using API.Models.Auth;
using API.Models.FieldOfficer;
using System.Threading.Tasks;
using System;

namespace API.BAL
{
    /// <summary>
    /// Database helper for Admin and Field Officer authentication.
    /// Queries: t_users, t_admin_profiles, t_field_officer_profiles
    /// Role is always read from t_users.c_role — never trusted from the client.
    /// </summary>
    public class StaffAuthHelper
    {
        private readonly NpgsqlConnection _conn;

        public StaffAuthHelper(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET STAFF USER BY EMAIL
        // Returns the t_users row + the full name from whichever profile table
        // matches the user's role (admin_profiles or field_officer_profiles).
        // Only allows role IN ('admin','field_officer').
        // Role is sourced exclusively from t_users.c_role — not from the client.
        // ─────────────────────────────────────────────────────────────────
        public async Task<(User? user, string? fullName)> GetStaffByEmailAsync(string email)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                // Single query — LEFT JOINs both profile tables.
                // COALESCE picks whichever name is non-null (only one will be, based on role).
                const string sql = @"
                    SELECT
                        u.c_id,
                        u.c_email,
                        u.c_password_hash,
                        u.c_role,
                        u.c_is_active,
                        u.c_is_approved,
                        COALESCE(ap.c_full_name, fo.c_full_name) AS c_full_name
                    FROM t_users u
                    LEFT JOIN t_admin_profiles         ap ON ap.c_user_id = u.c_id
                    LEFT JOIN t_field_officer_profiles fo ON fo.c_user_id = u.c_id
                    WHERE LOWER(TRIM(u.c_email)) = LOWER(TRIM(@email))
                    AND LOWER(TRIM(u.c_role)) IN ('admin','field_officer')
                    LIMIT 1;";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@email", email);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var user = new User
                    {
                        Id           = reader.GetInt32(0),
                        Email        = reader.GetString(1),
                        PasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Role         = reader.GetString(3),   // ← from DB, always
                        IsActive     = reader.GetBoolean(4),
                        IsApproved   = reader.GetBoolean(5)
                    };

                    string? fullName = reader.IsDBNull(6) ? null : reader.GetString(6);
                    return (user, fullName);
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving staff by email: {ex.Message}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // GET ADMIN PROFILE ID (for JWT claim)
        // ─────────────────────────────────────────────────────────────────
        public async Task<long?> GetAdminProfileIdAsync(long userId)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                const string sql = @"
                    SELECT c_id FROM t_admin_profiles
                    WHERE c_user_id = @userId
                    LIMIT 1;";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@userId", userId);

                var result = await cmd.ExecuteScalarAsync();
                return result is int id ? (long)id : (result is long lid ? lid : null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving admin profile id: {ex.Message}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // GET FIELD OFFICER DETAILS (for JWT claims)
        // ─────────────────────────────────────────────────────────────────
        public async Task<FieldOfficerProfile?> GetFieldOfficerDetailsAsync(long userId)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                const string sql = @"
                    SELECT c_id, c_warehouse_id, c_assigned_region
                    FROM t_field_officer_profiles
                    WHERE c_user_id = @userId
                    LIMIT 1;";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@userId", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new FieldOfficerProfile
                    {
                        Id             = reader.GetInt32(0),
                        UserId         = (int)userId,
                        WarehouseId    = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        AssignedRegion = reader.IsDBNull(2) ? null : reader.GetString(2)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving field officer details: {ex.Message}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PASSWORD VERIFICATION  (BCrypt)
        // ─────────────────────────────────────────────────────────────────
        public bool VerifyPassword(string plainText, string hash)
        {
            try
            {
                // Handle plain text passwords (for legacy/test data)
                if (string.IsNullOrEmpty(hash) || !hash.StartsWith("$2"))
                    return plainText == hash;

                return BC.Verify(plainText, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}