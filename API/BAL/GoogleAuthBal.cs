using System.Data;
using API.Models.Auth;
using API.Services;
using Npgsql;

namespace API.BAL
{
    /// <summary>
    /// Business logic for Google OAuth login.
    /// Creates or fetches user from DB, then returns a JWT
    /// identical in structure to normal email/password login.
    /// </summary>
    public class GoogleAuthBal
    {
        private readonly NpgsqlConnection _conn;
        private readonly JwtService _jwtService;

        public GoogleAuthBal(NpgsqlConnection conn, JwtService jwtService)
        {
            _conn = conn;
            _jwtService = jwtService; 
        }

        public async Task<object> GoogleLogin(GoogleDto dto)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            using var tran = await _conn.BeginTransactionAsync();

            try
            {
                int userId;
                string role;
                int farmerId;

                // 1. Check if user already exists
                var checkCmd = new NpgsqlCommand(
                    "SELECT c_id, c_role, c_profile_image_url FROM t_users WHERE c_email = @email",
                    _conn, tran);

                checkCmd.Parameters.AddWithValue("email", dto.Email);

                using var reader = await checkCmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    await reader.CloseAsync();

                    // 2. Insert User
                    var insertCmd = new NpgsqlCommand(@"
                INSERT INTO t_users
                (c_email, c_role, c_is_active, c_is_approved, c_is_first_login, c_profile_image_url)
                VALUES (@email, @role, true, true, true, @pic)
                RETURNING c_id, c_role",
                        _conn, tran);

                    insertCmd.Parameters.AddWithValue("email", dto.Email);
                    insertCmd.Parameters.AddWithValue("role", dto.Role ?? "farmer");
                    insertCmd.Parameters.AddWithValue("pic", dto.PictureUrl ?? (object)DBNull.Value);

                    using var insertReader = await insertCmd.ExecuteReaderAsync();
                    await insertReader.ReadAsync();

                    userId = Convert.ToInt32(insertReader[0]);
                    role = insertReader[1].ToString();

                    await insertReader.CloseAsync();
                }
                else
                {
                    await reader.ReadAsync();

                    userId = Convert.ToInt32(reader[0]);
                    role = reader[1].ToString();

                    await reader.CloseAsync();
                }

                // 3. Check Farmer Profile
                var farmerCheckCmd = new NpgsqlCommand(@"
            SELECT c_id FROM t_farmer_profiles
            WHERE c_user_id = @uid",
                    _conn, tran);

                farmerCheckCmd.Parameters.AddWithValue("uid", userId);

                var farmerObj = await farmerCheckCmd.ExecuteScalarAsync();

                if (farmerObj == null)
                {
                    // 4. Insert Farmer Profile
                    var insertFarmerCmd = new NpgsqlCommand(@"
                INSERT INTO t_farmer_profiles
                (c_user_id, c_full_name)
                VALUES (@uid, @name)
                RETURNING c_id",
                        _conn, tran);

                    insertFarmerCmd.Parameters.AddWithValue("uid", userId);
                    insertFarmerCmd.Parameters.AddWithValue("name", dto.Name ?? "Google User");

                    farmerId = Convert.ToInt32(await insertFarmerCmd.ExecuteScalarAsync());
                }
                else
                {
                    farmerId = Convert.ToInt32(farmerObj);
                }

                // 5. Insert OAuth Mapping
                var oauthCmd = new NpgsqlCommand(@"
            INSERT INTO t_user_oauth_accounts
            (c_user_id, c_provider, c_provider_user_id, c_provider_email, c_display_name)
            VALUES (@uid, 'google', @pid, @email, @name)
            ON CONFLICT (c_provider, c_provider_user_id) DO NOTHING",
                    _conn, tran);

                oauthCmd.Parameters.AddWithValue("uid", userId);
                oauthCmd.Parameters.AddWithValue("pid", dto.ProviderId);
                oauthCmd.Parameters.AddWithValue("email", dto.Email);
                oauthCmd.Parameters.AddWithValue("name", dto.Name ?? "Google User");

                await oauthCmd.ExecuteNonQueryAsync();

                await tran.CommitAsync();

                // 6. JWT Claims
                var additionalClaims = new Dictionary<string, string>
        {
            { "login_provider", "google" },
            { "farmer_id", farmerId.ToString() },
            { "profile_image_url", dto.PictureUrl ?? "" }
        };

                var token = _jwtService.GenerateJwtToken(
                    userId,
                    dto.Email,
                    role,
                    additionalClaims
                );

                return new
                {
                    success = true,
                    message = "Google login successful.",
                    token = token,
                    role = role,
                    userId = userId,
                    farmerId = farmerId,
                    email = dto.Email,
                    fullName = dto.Name,
                    expiryMinutes = 30
                };
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
    }
}