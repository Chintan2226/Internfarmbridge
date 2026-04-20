using Npgsql;
using System.Text.RegularExpressions;
using API.Services;
using API.Models;
using API.Models.Auth;
using Microsoft.Extensions.Configuration;

namespace API.BAL
{
    public class AuthHelper
    {
        private readonly string _connectionString;
        private readonly RedisService _redis;
        private readonly EmailService _email;

        public AuthHelper(IConfiguration config, RedisService redis, EmailService email)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _redis = redis;
            _email = email;
        }

        // 🔹 FORGOT PASSWORD (SEND OTP)
        public async Task<string> SendForgotPasswordOtpAsync(vm_ForgetRequest forget)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            try
            {
                string sql = "SELECT c_id FROM t_users WHERE c_email=@email AND c_is_active=true";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@email", forget.Email);

                var result = await cmd.ExecuteScalarAsync();

                // ✅ Security: Don't reveal if email exists
                if (result == null)
                    return "SUCCESS";

                string otp = new Random().Next(100000, 999999).ToString();

                // ✅ Store OTP in Redis (5 min)
                await _redis.SetOtpAsync(forget.Email, otp);
                Console.WriteLine("OTP GENERATED: " + otp);

                // ✅ Send Email
                await _email.SendOtpEmailAsync(forget.Email, otp);

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Forgot Password Error: " + ex.Message);
                return "Something went wrong";
            }
        }

        // 🔹 RESET PASSWORD
        public async Task<string> ResetUserPasswordAsync(vm_ResetRequest reset)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            try
            {
                // ✅ Get OTP from Redis
                var storedOtp = await _redis.GetOtpAsync(reset.Email);

                if (string.IsNullOrEmpty(storedOtp))
                    return "OTP expired";

                if (storedOtp != reset.Otp)
                    return "Invalid OTP";

                // ✅ Get user
                int userId;
                string sql = "SELECT c_id FROM t_users WHERE c_email=@email";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@email", reset.Email);
                    var result = await cmd.ExecuteScalarAsync();

                    if (result == null)
                        return "User not found";

                    userId = Convert.ToInt32(result);
                }

                // ✅ Password validation
                var regex = new Regex(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*#?&]).{8,}$");

                if (!regex.IsMatch(reset.NewPassword))
                    return "Weak password";

                // ✅ Hash password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(reset.NewPassword);

                // ✅ Update DB
                string updateSql = "UPDATE t_users SET c_password_hash=@pass WHERE c_id=@id";

                using (var cmd = new NpgsqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@pass", hashedPassword);
                    cmd.Parameters.AddWithValue("@id", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // ✅ Remove OTP after success
                await _redis.RemoveOtpAsync(reset.Email);

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Reset Password Error: " + ex.Message);
                return "Something went wrong";
            }
        }

        
    }
}