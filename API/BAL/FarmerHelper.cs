using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Services;
using Npgsql;
using Microsoft.Extensions.Configuration;
using API.Models.Auth;
using API.Models.Farmer;

namespace API.BAL
{
    public class FarmerHelper
    {
        private readonly NpgsqlConnection _conn;
        private readonly IConfiguration _configuration;

        public FarmerHelper(NpgsqlConnection conn, IConfiguration configuration)
        {
            _conn = conn;
            _configuration = configuration;
        }

        // ──────────────────────────────────────────
        // REGISTER FARMER
        // Inserts into t_users + t_farmer_profiles (atomic transaction)
        // ──────────────────────────────────────────
        public async Task<FarmerProfile> RegisterFarmerAsync(
            string email,
            string passwordHash,
            string fullName,
            string phone,
            string address,
            string state,
            string district)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                using var transaction = _conn.BeginTransaction();
                try
                {
                    // 1. Insert into t_users
                    var userQuery = @"
                        INSERT INTO t_users (c_email, c_password_hash, c_role, c_language_preference,
                                            c_is_first_login, c_is_active, c_is_approved)
                        VALUES (@email, @passwordHash, 'farmer', 'en', true, true, true)
                        RETURNING c_id;";

                    long userId;
                    using (var userCmd = new NpgsqlCommand(userQuery, _conn, transaction))
                    {
                        userCmd.Parameters.AddWithValue("@email", email);
                        userCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                        userId = Convert.ToInt32(await userCmd.ExecuteScalarAsync());
                    }

                    // 2. Insert into t_farmer_profiles
                    var farmerQuery = @"
                        INSERT INTO t_farmer_profiles (c_user_id, c_full_name, c_phone, c_address, c_state, c_district, c_created_at)
                        VALUES (@userId, @fullName, @phone, @address, @state, @district, NOW())
                        RETURNING c_id;";

                    long farmerId;
                    using (var farmerCmd = new NpgsqlCommand(farmerQuery, _conn, transaction))
                    {
                        farmerCmd.Parameters.AddWithValue("@userId", userId);
                        farmerCmd.Parameters.AddWithValue("@fullName", fullName);
                        farmerCmd.Parameters.AddWithValue("@phone", phone);
                        farmerCmd.Parameters.AddWithValue("@address", (object?)address ?? DBNull.Value);
                        farmerCmd.Parameters.AddWithValue("@state", (object?)state ?? DBNull.Value);
                        farmerCmd.Parameters.AddWithValue("@district", (object?)district ?? DBNull.Value);

                        farmerId = Convert.ToInt32(await farmerCmd.ExecuteScalarAsync());
                    }

                    await transaction.CommitAsync();

                    return new FarmerProfile
                    {
                        Id = (int)farmerId,
                        UserId = (int)userId,
                        FullName = fullName,
                        Phone = phone,
                        Address = address,
                        State = state,
                        District = district,
                        CreatedAt = DateTime.UtcNow
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error registering farmer: {ex.Message}", ex);
            }
        }

        // ──────────────────────────────────────────
        // GET FARMER BY EMAIL (for login)
        // ──────────────────────────────────────────
        public async Task<(User user, FarmerProfile farmer)> GetFarmerByEmailAsync(string email)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = @"
                    SELECT u.c_id, u.c_email, u.c_password_hash, u.c_role, u.c_is_active, u.c_is_approved,
                           fp.c_id, fp.c_full_name, fp.c_phone, fp.c_address, fp.c_state, fp.c_district
                    FROM t_users u
                    LEFT JOIN t_farmer_profiles fp ON u.c_id = fp.c_user_id
                    WHERE LOWER(TRIM(u.c_email)) = LOWER(TRIM(@email)) AND LOWER(TRIM(u.c_role)) = 'farmer';";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@email", email);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var user = new User
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        PasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Role = reader.GetString(3),
                        IsActive = reader.GetBoolean(4),
                        IsApproved = reader.GetBoolean(5)
                    };

                    FarmerProfile? farmer = null;
                    if (!reader.IsDBNull(6))
                    {
                        farmer = new FarmerProfile
                        {
                            Id = reader.GetInt32(6),
                            UserId = user.Id,
                            FullName = reader.GetString(7),
                            Phone = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            Address = reader.IsDBNull(9) ? "" : reader.GetString(9),
                            State = reader.IsDBNull(10) ? "" : reader.GetString(10),
                            District = reader.IsDBNull(11) ? "" : reader.GetString(11)
                        };
                    }

                    return (user, farmer)!;
                }

                return (null, null)!;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving farmer by email: {ex.Message}", ex);
            }
        }

        // ──────────────────────────────────────────
        // GET FARMER BY PHONE (for login)
        // ──────────────────────────────────────────
        public async Task<(User user, FarmerProfile farmer)> GetFarmerByPhoneAsync(string phone)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = @"
                    SELECT u.c_id, u.c_email, u.c_password_hash, u.c_role, u.c_is_active, u.c_is_approved,
                           fp.c_id, fp.c_full_name, fp.c_phone, fp.c_address, fp.c_state, fp.c_district
                    FROM t_users u
                    LEFT JOIN t_farmer_profiles fp ON u.c_id = fp.c_user_id
                    WHERE fp.c_phone = @phone AND LOWER(TRIM(u.c_role)) = 'farmer';";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@phone", phone);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var user = new User
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        PasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Role = reader.GetString(3),
                        IsActive = reader.GetBoolean(4),
                        IsApproved = reader.GetBoolean(5)
                    };

                    FarmerProfile? farmer = null;
                    if (!reader.IsDBNull(6))
                    {
                        farmer = new FarmerProfile
                        {
                            Id = reader.GetInt32(6),
                            UserId = user.Id,
                            FullName = reader.GetString(7),
                            Phone = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            Address = reader.IsDBNull(9) ? "" : reader.GetString(9),
                            State = reader.IsDBNull(10) ? "" : reader.GetString(10),
                            District = reader.IsDBNull(11) ? "" : reader.GetString(11)
                        };
                    }

                    return (user, farmer)!;
                }

                return (null, null)!;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving farmer by phone: {ex.Message}", ex);
            }
        }

        // ──────────────────────────────────────────
        // EMAIL / PHONE EXISTENCE CHECKS
        // ──────────────────────────────────────────
        public async Task<bool> EmailExistsAsync(string email)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = "SELECT COUNT(1) FROM t_users WHERE LOWER(TRIM(c_email)) = LOWER(TRIM(@email));";
                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@email", email);
                var result = (long)await cmd.ExecuteScalarAsync()!;
                return result > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking email existence: {ex.Message}", ex);
            }
        }

        public async Task<bool> PhoneExistsAsync(string phone)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = "SELECT COUNT(1) FROM t_farmer_profiles WHERE c_phone = @phone;";
                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@phone", phone);
                var result = (long)await cmd.ExecuteScalarAsync()!;
                return result > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking phone existence: {ex.Message}", ex);
            }
        }

        // ──────────────────────────────────────────
        // FORGOT PASSWORD — Send OTP
        // ──────────────────────────────────────────
        public async Task<string> SendForgotPasswordOtpAsync(string email, EmailService emailService)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                // Check user exists (any role with this email)
                var checkSql = "SELECT c_id FROM t_users WHERE c_email = @email AND c_is_active = true;";
                using (var cmd = new NpgsqlCommand(checkSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null)
                        return "SUCCESS"; // Security: don't reveal if email exists
                }

                // Generate OTP
                string otp = new Random().Next(100000, 999999).ToString();
                string otpHash = BCrypt.Net.BCrypt.HashPassword(otp);
                DateTime expiresAt = DateTime.UtcNow.AddMinutes(10);

                // Upsert OTP into t_password_otps
                var upsertSql = @"
                    INSERT INTO t_password_otps (c_email, c_otp_hash, c_expires_at)
                    VALUES (@email, @otpHash, @expiresAt)
                    ON CONFLICT (c_email) DO UPDATE
                        SET c_otp_hash = @otpHash, c_expires_at = @expiresAt;";

                using (var cmd = new NpgsqlCommand(upsertSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@otpHash", otpHash);
                    cmd.Parameters.AddWithValue("@expiresAt", expiresAt);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Send OTP via email
                await emailService.SendOtpEmailAsync(email, otp);

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        // ──────────────────────────────────────────
        // RESET PASSWORD — Verify OTP + update password
        // ──────────────────────────────────────────
        public async Task<string> ResetFarmerPasswordAsync(string email, string otp, string newPassword)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                // 1. Fetch stored OTP
                string? storedHash = null;
                DateTime expiresAt = DateTime.MinValue;

                var fetchSql = "SELECT c_otp_hash, c_expires_at FROM t_password_otps WHERE c_email = @email;";
                using (var cmd = new NpgsqlCommand(fetchSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        storedHash = reader.GetString(0);
                        expiresAt = reader.GetDateTime(1);
                    }
                }

                if (storedHash == null)
                    return "OTP expired or not requested";

                if (DateTime.UtcNow > expiresAt)
                    return "OTP expired";

                if (!BCrypt.Net.BCrypt.Verify(otp, storedHash))
                    return "Invalid OTP";

                // 2. Password strength validation
                var regex = new System.Text.RegularExpressions.Regex(
                    @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*#?&]).{8,}$");
                if (!regex.IsMatch(newPassword))
                    return "Weak password. Must be 8+ chars with letters, numbers & special characters.";

                // 3. Hash and update
                string hashed = BCrypt.Net.BCrypt.HashPassword(newPassword);

                var updateSql = "UPDATE t_users SET c_password_hash = @pass WHERE c_email = @email;";
                using (var cmd = new NpgsqlCommand(updateSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@pass", hashed);
                    cmd.Parameters.AddWithValue("@email", email);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 4. Remove used OTP
                var deleteSql = "DELETE FROM t_password_otps WHERE c_email = @email;";
                using (var cmd = new NpgsqlCommand(deleteSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    await cmd.ExecuteNonQueryAsync();
                }

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        // ──────────────────────────────────────────
        // PASSWORD HELPERS
        // ──────────────────────────────────────────
        public string HashPassword(string password)
        {
            try
            {
                return BCrypt.Net.BCrypt.HashPassword(password);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error hashing password: {ex.Message}", ex);
            }
        }

        public bool VerifyPassword(string password, string hash)
        {
            try
            {
                if (!hash.StartsWith("$2"))
                    return password == hash; // plain text fallback for legacy data
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error verifying password: {ex.Message}", ex);
            }
        }
    }
}
