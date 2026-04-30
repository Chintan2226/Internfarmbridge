using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using API.Models.Admin;
using API.Models.Farmer;
using API.Models.Payment;
using API.Models.Settings;
using API.Services;
using Npgsql;
using NpgsqlTypes;

namespace API.BAL
{
    public class AdminHelper
    {
        private readonly NpgsqlConnection _conn;
        private readonly ElasticService _elasticService;
        private readonly IConfiguration _configuration;

        public AdminHelper(
            NpgsqlConnection conn,
            IConfiguration configuration,
            ElasticService elasticService)
        {
            _conn = conn;
            _configuration = configuration;
            _elasticService = elasticService;
        }

        public async Task<List<dynamic>> GetFarmerList(string searchTerm, int pageNumber = 1)
        {
            // Requirement: 25 records per page
            const int pageSize = 25;

            // Logic: Page 1 starts at 0, Page 2 starts at 25, Page 3 at 50...
            int offset = (pageNumber - 1) * pageSize;

            var list = new List<dynamic>();

            // Handle empty search terms safely
            string formattedSearch = string.IsNullOrWhiteSpace(searchTerm) ? "%" : $"%{searchTerm}%";

            // SQL Explained:
            // 1. LEFT JOIN: Ensures we see the user even if profile is partially empty.
            // 2. COALESCE: Prevents NULL values from hiding the row during search.
            // 3. LIMIT/OFFSET: Handles the 25-per-page requirement.
            string sql = @"
                SELECT 
                    u.c_id as UserId, 
                    COALESCE(fp.c_full_name, 'No Name') as FullName, 
                    u.c_email as Email, 
                    COALESCE(fp.c_phone, 'N/A') as Phone, 
                    COALESCE(fp.c_district, '') as District, 
                    COALESCE(fp.c_state, '') as State,
                    u.c_is_active as IsActive,
                    u.c_created_at as RegistrationDate
                FROM t_users u
                LEFT JOIN t_farmer_profiles fp ON u.c_id = fp.c_user_id
                WHERE u.c_role = 'farmer'
                AND (
                    COALESCE(fp.c_full_name, '') ILIKE @search OR 
                    COALESCE(fp.c_phone, '')     ILIKE @search OR 
                    COALESCE(fp.c_district, '')  ILIKE @search OR
                    u.c_email                   ILIKE @search
                )
                ORDER BY u.c_created_at DESC
                LIMIT @limit OFFSET @offset";

            try
            {
                await EnsureOpenConnection();
                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@search", formattedSearch);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Phone = reader.GetString(3),
                        Location = $"{reader.GetString(4)}, {reader.GetString(5)}".Trim(new char[] { ' ', ',' }),
                        IsActive = reader.GetBoolean(6),
                        RegistrationDate = reader.GetDateTime(7)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching farmer list: {ex.Message}");
                throw;
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return list;
        }

        public async Task<vm_FarmerFullDetail> GetFarmerFullDetailById(int userId)
        {
            var detail = new vm_FarmerFullDetail();

            // Notice the LEFT JOIN in the first query below
            string sql = @"
        SELECT u.c_id, fp.c_full_name, u.c_email, fp.c_phone, fp.c_district, fp.c_state, u.c_created_at
        FROM t_users u 
        LEFT JOIN t_farmer_profiles fp ON u.c_id = fp.c_user_id 
        WHERE u.c_id = @uid;

        SELECT c_bank_name, c_account_number, c_ifsc_code, c_verification_status 
        FROM t_bank_accounts WHERE c_user_id = @uid;

        SELECT cl.c_id, cp.c_name, cl.c_quantity_available, cl.c_asking_price, cl.c_status, cl.c_created_at
        FROM t_farmer_crop_listings cl 
        JOIN t_catalog_products cp ON cl.c_catalog_product_id = cp.c_id
        WHERE cl.c_farmer_id = (SELECT c_id FROM t_farmer_profiles WHERE c_user_id = @uid);

        SELECT vo.c_id, cp.c_name, oi.c_subtotal, vo.c_status, vo.c_ordered_at
        FROM t_order_items oi 
        JOIN t_vendor_orders vo ON oi.c_order_id = vo.c_id
        JOIN t_catalog_products cp ON oi.c_catalog_product_id = cp.c_id
        WHERE oi.c_crop_listing_id IN (SELECT c_id FROM t_farmer_crop_listings WHERE c_farmer_id = (SELECT c_id FROM t_farmer_profiles WHERE c_user_id = @uid));";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            if (_conn.State != ConnectionState.Open) await _conn.OpenAsync();

            using var r = await cmd.ExecuteReaderAsync();

            // 1. Profile (Safely handling DBNulls since it's a LEFT JOIN now)
            if (await r.ReadAsync())
            {
                string dist = r.IsDBNull(4) ? "" : r.GetString(4);
                string state = r.IsDBNull(5) ? "" : r.GetString(5);
                string location = string.IsNullOrWhiteSpace(dist) && string.IsNullOrWhiteSpace(state)
                                  ? "Location not provided"
                                  : $"{dist}, {state}".Trim(',', ' ');

                detail.Profile = new vm_FarmerProfile
                {
                    FarmerID = r.GetInt32(0),
                    FullName = r.IsDBNull(1) ? "No Name" : r.GetString(1),
                    Email = r.IsDBNull(2) ? "N/A" : r.GetString(2),
                    MobileNumber = r.IsDBNull(3) ? "N/A" : r.GetString(3),
                    Location = location,
                    RegistrationDate = r.GetDateTime(6)
                };
            }

            // 2. Bank
            await r.NextResultAsync();
            while (await r.ReadAsync()) detail.BankDetails.Add(new vm_BankDetail
            {
                BankName = r.IsDBNull(0) ? "" : r.GetString(0),
                AccountNumber = r.IsDBNull(1) ? "" : r.GetString(1),
                IFSCCode = r.IsDBNull(2) ? "" : r.GetString(2),
                Status = r.IsDBNull(3) ? "" : r.GetString(3)
            });

            // 3. Crops
            await r.NextResultAsync();
            while (await r.ReadAsync()) detail.CropHistory.Add(new vm_CropHistory
            {
                ListingID = r.GetInt32(0),
                ProductName = r.IsDBNull(1) ? "" : r.GetString(1),
                Quantity = r.GetDecimal(2),
                Price = r.GetDecimal(3),
                Status = r.IsDBNull(4) ? "" : r.GetString(4),
                CreatedAt = r.GetDateTime(5)
            });

            // 4. Orders
            await r.NextResultAsync();
            while (await r.ReadAsync()) detail.OrderHistory.Add(new vm_OrderHistory
            {
                OrderID = r.GetInt32(0),
                CropName = r.IsDBNull(1) ? "" : r.GetString(1),
                Amount = r.GetDecimal(2),
                Status = r.IsDBNull(3) ? "" : r.GetString(3),
                OrderDate = r.GetDateTime(4)
            });

            await _conn.CloseAsync();
            return detail;
        }

        public async Task<List<vm_PendingPayment>> GetPendingPaymentsAsync(int userId)
        {
            var list = new List<vm_PendingPayment>();

            // This query finds completed 30% payments (Payment 1) 
            // that do NOT have a 70% settlement (Payment 2) yet.
            string sql = @"
        SELECT 
            p1.c_id AS PaymentId,
            COALESCE(cp.c_name, 'Crop Settlement') AS CropName,
            ROUND(p1.c_amount / 0.30, 2) AS TotalAmount,
            p1.c_amount AS AdvancePaid,
            ROUND(p1.c_amount / 0.30, 2) - p1.c_amount AS BalancePending,
            'Pending Admin Approval' AS Status,
            p1.c_created_at AS CreatedAt
        FROM t_payments_farmer p1
        JOIN t_farmer_profiles fp ON p1.c_farmer_id = fp.c_id
        LEFT JOIN t_procurement_requests pr ON p1.c_procurement_request_id = pr.c_id
        LEFT JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
        LEFT JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
        WHERE p1.c_payment_number = 1 
          AND p1.c_status = 'success' 
          AND fp.c_user_id = @uid
          -- CRITICAL: Ensure Payment 2 hasn't been created yet!
          AND NOT EXISTS (
              SELECT 1 FROM t_payments_farmer p2 
              WHERE p2.c_procurement_request_id = p1.c_procurement_request_id 
              AND p2.c_payment_number = 2
          )
        ORDER BY p1.c_created_at DESC";

            try
            {
                if (_conn.State != ConnectionState.Open) await _conn.OpenAsync();
                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new vm_PendingPayment
                    {
                        PaymentId = reader.GetInt32(0),
                        CropName = reader.IsDBNull(1) ? "Crop Settlement" : reader.GetString(1),
                        TotalAmount = reader.GetDecimal(2),
                        AdvancePaid = reader.GetDecimal(3),
                        BalancePending = reader.GetDecimal(4),
                        Status = reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6)
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine("Error fetching payments: " + ex.Message); }
            finally { await _conn.CloseAsync(); }

            return list;
        }
        public async Task<bool> ApproveFarmerPaymentAsync(vm_PaymentApproveRequest req)
        {
            try
            {
                if (_conn.State != ConnectionState.Open) await _conn.OpenAsync();

                string getSql = @"
                    SELECT p.c_farmer_id, p.c_procurement_request_id, p.c_amount, fp.c_user_id 
                    FROM t_payments_farmer p
                    JOIN t_farmer_profiles fp ON p.c_farmer_id = fp.c_id
                    WHERE p.c_id = @p1id";
                using var getCmd = new NpgsqlCommand(getSql, _conn);
                getCmd.Parameters.AddWithValue("@p1id", req.PaymentId);

                int farmerId = 0;
                int procReqId = 0;
                decimal advancePaid = 0;
                int userId = 0;

                using (var reader = await getCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        farmerId = reader.GetInt32(0);
                        procReqId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        advancePaid = reader.GetDecimal(2);
                        userId = reader.GetInt32(3);
                    }
                    else return false;
                }

                decimal pendingAmount = Math.Round(advancePaid / 0.30m, 2) - advancePaid;

                // ✅ Write back so controller can use these values for RabbitMQ
                req.FarmerId = userId; // Important: Use User ID for notifications!
                req.Amount = pendingAmount;

                string insertSql = @"
            INSERT INTO t_payments_farmer 
            (c_farmer_id, c_procurement_request_id, c_amount, c_payment_number, c_trigger_event, c_payment_mode, c_utr_reference, c_status, c_settled_at) 
            VALUES 
            (@fid, CASE WHEN @prid = 0 THEN NULL ELSE @prid END, @amt, 2, 'delivery_confirmed', 'bank_transfer', @utr, 'success', NOW())";

                using var insertCmd = new NpgsqlCommand(insertSql, _conn);
                insertCmd.Parameters.AddWithValue("@fid", farmerId);
                insertCmd.Parameters.AddWithValue("@prid", procReqId);
                insertCmd.Parameters.AddWithValue("@amt", pendingAmount);
                insertCmd.Parameters.AddWithValue("@utr", req.UtrReference ?? "AUTO-GEN-UTR");

                int rows = await insertCmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex) { Console.WriteLine("Error approving payment: " + ex.Message); return false; }
            finally { await _conn.CloseAsync(); }
        }
        public async Task<bool> UpdateFarmerStatusAsync(vm_FarmerStatusRequest farmer)
        {
            try
            {
                // 1. Ensure connection is open
                await EnsureOpenConnection();

                // 2. Start a Transaction (Requirement #5: Ensure atomic logging)
                using (var transaction = await _conn.BeginTransactionAsync())
                {
                    // 3. Update status in t_users
                    // Schema Match: c_is_active and c_id
                    string updateSql = "UPDATE t_users SET c_is_active = @active WHERE c_id = @uid";

                    using (var updateCmd = new NpgsqlCommand(updateSql, _conn))
                    {
                        // Note: Use farmer.Status or farmer.IsActive based on your VM property name
                        updateCmd.Parameters.AddWithValue("@active", farmer.Status);
                        updateCmd.Parameters.AddWithValue("@uid", farmer.UserId);

                        int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                        // If the UserID doesn't exist, we stop here
                        if (rowsAffected == 0) return false;
                    }

                    // 4. Prepare Notification Content
                    string title = farmer.Status ? "Account Activated" : "Account Deactivated";
                    string message = farmer.Status
                        ? "Your FarmBridge account has been activated. You can now list crops."
                        : $"Your account has been deactivated. Reason: {farmer.Reason}.";

                    // 5. Log Admin Action in t_notifications
                    // Schema Match: c_user_id, c_type, c_channel, c_title, c_body, c_reference_type, c_reference_id
                    string insertSql = @"
                INSERT INTO t_notifications 
                (c_user_id, c_type, c_channel, c_title, c_body, c_reference_type, c_reference_id)
                VALUES 
                (@uid, 'system', 'email', @title, @body, 'admin_action', @adminId)";

                    using (var insertCmd = new NpgsqlCommand(insertSql, _conn))
                    {
                        insertCmd.Parameters.AddWithValue("@uid", farmer.UserId);
                        insertCmd.Parameters.AddWithValue("@adminId", farmer.AdminId);
                        insertCmd.Parameters.AddWithValue("@title", title);
                        insertCmd.Parameters.AddWithValue("@body", message);
                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    // 6. CRITICAL: Commit the transaction to save changes permanently
                    await transaction.CommitAsync();
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("UpdateFarmer Status Error: " + e.Message);
                return false;
            }
            finally
            {
                // Always close the connection
                if (_conn.State == ConnectionState.Open) await _conn.CloseAsync();
            }
        }

        public async Task<object> GetDashboardCounts()
        {
            try
            {
                await EnsureOpenConnection();

                string sql = @"
            SELECT 
                COUNT(*) FILTER (WHERE c_role = 'farmer') AS total_farmers,

                COUNT(*) FILTER (
                    WHERE c_role = 'farmer' 
                    AND c_created_at >= NOW() - INTERVAL '7 days'
                ) AS farmers_this_week,

                COUNT(*) FILTER (
                    WHERE c_role = 'farmer' AND c_is_active = true
                ) AS active_farmers,

                COUNT(*) FILTER (
                    WHERE c_role = 'farmer' AND c_is_active = false
                ) AS deactivated_farmers,

                COUNT(*) FILTER (
                    WHERE c_role = 'farmer' 
                    AND c_is_active = false
                    AND date_trunc('month', c_created_at) = date_trunc('month', CURRENT_DATE)
                ) AS deactivated_this_month
            FROM t_users;

            SELECT COUNT(*) AS pending_verification
            FROM t_bank_accounts
            WHERE c_verification_status = 'pending';
        ";

                using var cmd = new NpgsqlCommand(sql, _conn);

                using var reader = await cmd.ExecuteReaderAsync();

                int total = 0, thisWeek = 0, active = 0, deactivated = 0, deactivatedMonth = 0, pending = 0;

                // 🔹 First Result (t_users)
                if (await reader.ReadAsync())
                {
                    total = Convert.ToInt32(reader["total_farmers"]);
                    thisWeek = Convert.ToInt32(reader["farmers_this_week"]);
                    active = Convert.ToInt32(reader["active_farmers"]);
                    deactivated = Convert.ToInt32(reader["deactivated_farmers"]);
                    deactivatedMonth = Convert.ToInt32(reader["deactivated_this_month"]);
                }

                // 🔹 Second Result (pending verification)
                if (await reader.NextResultAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        pending = Convert.ToInt32(reader["pending_verification"]);
                    }
                }

                double activeRate = total == 0 ? 0 : (active * 100.0) / total;

                return new
                {
                    RegisteredFarmers = total,
                    FarmersThisWeek = thisWeek,
                    ActiveAccounts = active,
                    ActiveRate = Math.Round(activeRate, 2),
                    Deactivated = deactivated,
                    DeactivatedThisMonth = deactivatedMonth,
                    PendingVerification = pending
                };
            }
            catch (Exception e)
            {
                Console.WriteLine("Dashboard Error: " + e.Message);
                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }


        // Helper to ensure connection is open before any command

        private async Task EnsureOpenConnection()
        {
            // Check for Broken state specifically
            if (_conn.State == ConnectionState.Broken)
            {
                await _conn.CloseAsync();
            }

            if (_conn.State == ConnectionState.Closed)
            {
                await _conn.OpenAsync();
            }
        }

        //  KPI: Today's Revenue 
        public async Task<decimal> GetTodayRevenue()
        {
            try
            {
                await EnsureOpenConnection();
                string qry =
                    @"
                    SELECT COALESCE(SUM(c_total_amount), 0)
                    FROM t_vendor_orders
                    WHERE DATE(c_ordered_at) = CURRENT_DATE
                      AND c_status != 'cancelled'";

                using var cmd = new NpgsqlCommand(qry, _conn);
                return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTodayRevenue: {ex.Message}");
                return 0;
            }
        }

        //  KPI: Total Farmers 
        public async Task<int> GetTotalFarmers()
        {
            try
            {
                await EnsureOpenConnection();
                string qry =
                    "SELECT COUNT(*) FROM t_users WHERE c_role = 'farmer' AND c_is_active = true";

                using var cmd = new NpgsqlCommand(qry, _conn);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTotalFarmers: {ex.Message}");
                return 0;
            }
        }

        //  KPI: Total Vendors 
        public async Task<int> GetTotalVendors()
        {
            try
            {
                await EnsureOpenConnection();
                string qry =
                    "SELECT COUNT(*) FROM t_users WHERE c_role = 'vendor' AND c_is_active = true";

                using var cmd = new NpgsqlCommand(qry, _conn);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTotalVendors: {ex.Message}");
                return 0;
            }
        }

        //  KPI: Active Field Officers 
        public async Task<int> GetActiveFOs()
        {
            try
            {
                await EnsureOpenConnection();
                string qry =
                    "SELECT COUNT(*) FROM t_users WHERE c_role = 'field_officer' AND c_is_active = true";

                using var cmd = new NpgsqlCommand(qry, _conn);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActiveFOs: {ex.Message}");
                return 0;
            }
        }

        // KPI: Today's New Orders
        public async Task<int> GetTodayNewOrders()
        {
            try
            {
                await EnsureOpenConnection();
                string qry =
                    "SELECT COUNT(*) FROM t_vendor_orders WHERE DATE(c_ordered_at) = CURRENT_DATE";

                using var cmd = new NpgsqlCommand(qry, _conn);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTodayNewOrders: {ex.Message}");
                return 0;
            }
        }

        //  Revenue Chart Data 
        public async Task<List<vm_ChartPoint>> GetRevenueChart(string period)
        {
            var list = new List<vm_ChartPoint>();
            try
            {
                await EnsureOpenConnection();
                string qry = period switch
                {
                    "weekly" => @"
                        SELECT TO_CHAR(c_ordered_at, 'Dy') AS label,
                               COALESCE(SUM(c_total_amount), 0) AS value
                        FROM t_vendor_orders
                        WHERE c_ordered_at >= CURRENT_DATE - INTERVAL '6 days'
                          AND c_status != 'cancelled'
                        GROUP BY DATE(c_ordered_at), TO_CHAR(c_ordered_at, 'Dy')
                        ORDER BY DATE(c_ordered_at)",

                    "yearly" => @"
                        SELECT TO_CHAR(c_ordered_at, 'Mon') AS label,
                               COALESCE(SUM(c_total_amount), 0) AS value
                        FROM t_vendor_orders
                        WHERE EXTRACT(YEAR FROM c_ordered_at) = EXTRACT(YEAR FROM CURRENT_DATE)
                          AND c_status != 'cancelled'
                        GROUP BY EXTRACT(MONTH FROM c_ordered_at), TO_CHAR(c_ordered_at, 'Mon')
                        ORDER BY EXTRACT(MONTH FROM c_ordered_at)",

                    _ => @"
                        SELECT TO_CHAR(c_ordered_at, 'DD Mon') AS label,
                               COALESCE(SUM(c_total_amount), 0) AS value
                        FROM t_vendor_orders
                        WHERE c_ordered_at >= DATE_TRUNC('month', CURRENT_DATE)
                          AND c_status != 'cancelled'
                        GROUP BY DATE(c_ordered_at), TO_CHAR(c_ordered_at, 'DD Mon')
                        ORDER BY DATE(c_ordered_at)",
                };

                using var cmd = new NpgsqlCommand(qry, _conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(
                        new vm_ChartPoint
                        {
                            Label = reader["label"].ToString()!,
                            Value = Convert.ToDecimal(reader["value"]),
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRevenueChart: {ex.Message}");
            }
            return list;
        }

        // Order Volume Chart Data
        public async Task<List<vm_ChartPoint>> GetOrderVolumeChart(string period)
        {
            var list = new List<vm_ChartPoint>();
            try
            {
                await EnsureOpenConnection();
                string qry = period switch
                {
                    "weekly" => @"
                        SELECT TO_CHAR(c_ordered_at, 'Dy') AS label,
                               COUNT(*) AS value
                        FROM t_vendor_orders
                        WHERE c_ordered_at >= CURRENT_DATE - INTERVAL '6 days'
                        GROUP BY DATE(c_ordered_at), TO_CHAR(c_ordered_at, 'Dy')
                        ORDER BY DATE(c_ordered_at)",

                    "yearly" => @"
                        SELECT TO_CHAR(c_ordered_at, 'Mon') AS label,
                               COUNT(*) AS value
                        FROM t_vendor_orders
                        WHERE EXTRACT(YEAR FROM c_ordered_at) = EXTRACT(YEAR FROM CURRENT_DATE)
                        GROUP BY EXTRACT(MONTH FROM c_ordered_at), TO_CHAR(c_ordered_at, 'Mon')
                        ORDER BY EXTRACT(MONTH FROM c_ordered_at)",

                    _ => @"
                        SELECT TO_CHAR(c_ordered_at, 'DD Mon') AS label,
                               COUNT(*) AS value
                        FROM t_vendor_orders
                        WHERE c_ordered_at >= DATE_TRUNC('month', CURRENT_DATE)
                        GROUP BY DATE(c_ordered_at), TO_CHAR(c_ordered_at, 'DD Mon')
                        ORDER BY DATE(c_ordered_at)",
                };

                using var cmd = new NpgsqlCommand(qry, _conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(
                        new vm_ChartPoint
                        {
                            Label = reader["label"].ToString()!,
                            Value = Convert.ToDecimal(reader["value"]),
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetOrderVolumeChart: {ex.Message}");
            }
            return list;
        }

        // Today's Crop Listings
        public async Task<List<vm_CropListing>> GetTodayCropListings()
        {
            var list = new List<vm_CropListing>();
            try
            {
                await EnsureOpenConnection();
                string qry =
                    @"
                    SELECT fp.c_full_name            AS farmer_name,
                           cp.c_name                 AS crop_type,
                           cl.c_quantity_available   AS quantity,
                           cl.c_unit   AS unit,
                           cl.c_status               AS quality_status,
                           cl.c_submitted_at         AS listing_time
                    FROM t_farmer_crop_listings cl
                    INNER JOIN t_farmer_profiles  fp ON fp.c_id = cl.c_farmer_id
                    INNER JOIN t_catalog_products cp ON cp.c_id = cl.c_catalog_product_id
                    WHERE DATE(cl.c_submitted_at) = CURRENT_DATE
                    ORDER BY cl.c_submitted_at DESC";

                using var cmd = new NpgsqlCommand(qry, _conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(
                        new vm_CropListing
                        {
                            FarmerName = reader["farmer_name"].ToString()!,
                            CropType = reader["crop_type"].ToString()!,
                            Quantity = Convert.ToDecimal(reader["quantity"]),
                            Unit = reader["unit"].ToString()!,
                            QualityStatus = reader["quality_status"].ToString()!,
                            ListingTime = Convert.ToDateTime(reader["listing_time"]),
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTodayCropListings: {ex.Message}");
            }
            return list;
        }

        // Pending Approvals
        public async Task<List<vm_PendingApproval>> GetPendingApprovals()
        {
            var list = new List<vm_PendingApproval>();
            try
            {
                await EnsureOpenConnection();
                string qry =
                    @"
                    SELECT u.c_id                                              AS user_id,
                           COALESCE(fop.c_full_name, vp.c_business_name, '')   AS full_name,
                           u.c_role                                            AS role,
                           u.c_created_at                                      AS applied_on
                    FROM t_users u
                    LEFT JOIN t_field_officer_profiles fop ON fop.c_user_id = u.c_id
                    LEFT JOIN t_vendor_profiles        vp  ON vp.c_user_id  = u.c_id
                    WHERE u.c_is_approved = false
                      AND u.c_role IN ('field_officer', 'vendor')
                    ORDER BY u.c_created_at ASC";

                using var cmd = new NpgsqlCommand(qry, _conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(
                        new vm_PendingApproval
                        {
                            UserId = Convert.ToInt32(reader["user_id"]),
                            FullName = reader["full_name"].ToString()!,
                            Role = reader["role"].ToString()!,
                            AppliedOn = Convert.ToDateTime(reader["applied_on"]),
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPendingApprovals: {ex.Message}");
            }
            return list;
        }

        //  Approve User 
        public async Task<int> ApproveUser(int userId)
        {
            try
            {
                await EnsureOpenConnection();
                string qry =
                    @"
                    UPDATE t_users
                    SET c_is_approved = true,
                        c_is_active   = true
                    WHERE c_id = @userId";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                await cmd.ExecuteNonQueryAsync();
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ApproveUser: {ex.Message}");
                return 0;
            }
        }

        //  Get All Catalog Products 
        public async Task<List<vm_CatalogProduct>> GetAllCatalogProducts(
            string? category = null,
            string? unitOfMeasure = null
        )
        {
            var list = new List<vm_CatalogProduct>();
            try
            {
                await EnsureOpenConnection();

                string qry =
                    @"
                    SELECT cp.c_id               AS id,
                           cp.c_name             AS name,
                           cp.c_category         AS category,
                           cp.c_unit_of_measure  AS unit_of_measure,
                           cp.c_description      AS description,
                           cp.c_image_url        AS image_url,
                           cp.c_quality_parameters::TEXT AS quality_parameters,
                           cp.c_is_active        AS is_active,
                           cp.c_created_by       AS created_by,
                           ap.c_full_name        AS created_by_name,
                           cp.c_created_at       AS created_at,
                           cp.c_updated_at       AS updated_at
                    FROM t_catalog_products cp
                    LEFT JOIN t_admin_profiles ap ON ap.c_id = cp.c_created_by
                    WHERE (@category IS NULL OR cp.c_category ILIKE @category)
                      AND (@unit IS NULL OR cp.c_unit_of_measure ILIKE @unit)
                    ORDER BY cp.c_created_at DESC";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.Add(
                    new NpgsqlParameter("@category", NpgsqlDbType.Text)
                    {
                        Value = string.IsNullOrEmpty(category)
                            ? DBNull.Value
                            : (object)$"%{category}%",
                    }
                );
                cmd.Parameters.Add(
                    new NpgsqlParameter("@unit", NpgsqlDbType.Text)
                    {
                        Value = string.IsNullOrEmpty(unitOfMeasure)
                            ? DBNull.Value
                            : (object)$"%{unitOfMeasure}%",
                    }
                );

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(MapReader(reader));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllCatalogProducts: {ex.Message}");
            }
            return list;
        }

        // Get Catalog Product By Id
        public async Task<vm_CatalogProduct?> GetCatalogProductById(long id)
        {
            vm_CatalogProduct? product = null;
            try
            {
                await EnsureOpenConnection();

                string qry =
                    @"
                    SELECT cp.c_id               AS id,
                           cp.c_name             AS name,
                           cp.c_category         AS category,
                           cp.c_unit_of_measure  AS unit_of_measure,
                           cp.c_description      AS description,
                           cp.c_image_url        AS image_url,
                           cp.c_quality_parameters::TEXT AS quality_parameters,
                           cp.c_is_active        AS is_active,
                           cp.c_created_by       AS created_by,
                           ap.c_full_name        AS created_by_name,
                           cp.c_created_at       AS created_at,
                           cp.c_updated_at       AS updated_at
                    FROM t_catalog_products cp
                    LEFT JOIN t_admin_profiles ap ON ap.c_id = cp.c_created_by
                    WHERE cp.c_id = @id";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    product = MapReader(reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCatalogProductById: {ex.Message}");
            }
            return product;
        }

        // Add Catalog Product
        /// <summary>
        /// imageUrl should be the Cloudinary SecureUrl returned after upload,
        /// or null if no image was provided.
        /// </summary>
        public async Task<long> AddCatalogProduct(vm_CatalogProduct model, long adminId)
        {
            long newId = 0;
            try
            {
                await EnsureOpenConnection();

                string qry = @"
            INSERT INTO t_catalog_products
                (c_name, c_category, c_unit_of_measure, c_description,
                 c_image_url, c_quality_parameters,
                 c_is_active, c_created_by, c_created_at, c_updated_at)
            VALUES
                (@name, @category, @unit, @description,
                 @imageUrl, @qualityParams::JSONB,
                 @isActive, @createdBy, NOW(), NOW())
            RETURNING c_id";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@category", (object?)model.Category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@unit", model.UnitOfMeasure);
                cmd.Parameters.AddWithValue("@description", (object?)model.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@imageUrl", (object?)model.ImageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@qualityParams", (object?)model.QualityParameters ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@isActive", model.IsActive);
                cmd.Parameters.AddWithValue("@createdBy", adminId);

                var result = await cmd.ExecuteScalarAsync();
                newId = Convert.ToInt64(result);

                // ✅ INDEX IN ELASTICSEARCH - AFTER INSERT
                if (newId > 0)
                {
                    var productDoc = new CatalogProductDocument
                    {
                        Id = (int)newId,
                        Name = model.Name,
                        Category = model.Category,
                        UnitOfMeasure = model.UnitOfMeasure,
                        Description = model.Description,
                        ImageUrl = model.ImageUrl,
                        IsActive = model.IsActive,
                        CreatedBy = (int)adminId
                    };
                    await _elasticService.IndexCatalogProductAsync(productDoc);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddCatalogProduct: {ex.Message}");
            }
            return newId;
        }

        //  Edit Catalog Product 
        /// <summary>
        /// Pass the updated imageUrl (new Cloudinary URL, or the existing one if image unchanged).
        /// Cloudinary replace/delete is handled before calling this method.
        /// </summary>
        public async Task<int> EditCatalogProduct(vm_CatalogProduct model, long adminId)
        {
            try
            {
                await EnsureOpenConnection();

                string qry = @"
            UPDATE t_catalog_products
            SET c_name               = @name,
                c_category           = @category,
                c_unit_of_measure    = @unit,
                c_description        = @description,
                c_image_url          = @imageUrl,
                c_quality_parameters = @qualityParams::JSONB,
                c_is_active          = @isActive,
                c_updated_at         = NOW()
            WHERE c_id = @id";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@id", model.Id);
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@category", (object?)model.Category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@unit", model.UnitOfMeasure);
                cmd.Parameters.AddWithValue("@description", (object?)model.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@imageUrl", (object?)model.ImageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@qualityParams", (object?)model.QualityParameters ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@isActive", model.IsActive);

                int rows = await cmd.ExecuteNonQueryAsync();

                // ✅ UPDATE INDEX IN ELASTICSEARCH - AFTER UPDATE
                if (rows > 0)
                {
                    var productDoc = new CatalogProductDocument
                    {
                        Id = (int)model.Id,
                        Name = model.Name,
                        Category = model.Category,
                        UnitOfMeasure = model.UnitOfMeasure,
                        Description = model.Description,
                        ImageUrl = model.ImageUrl,
                        IsActive = model.IsActive,
                        CreatedBy = (int)adminId
                    };
                    await _elasticService.IndexCatalogProductAsync(productDoc);
                }

                return rows;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EditCatalogProduct: {ex.Message}");
                return 0;
            }
        }

        //  Delete Catalog Product 
        /// <summary>
        /// Returns the existing c_image_url so the API controller can call
        /// CloudinaryService.DeleteImageAsync(ExtractPublicId(imageUrl)) after DB deletion.
        /// </summary>
        public async Task<(bool Success, string Message, string? ExistingImageUrl)> DeleteCatalogProduct(long id, long adminId)
        {
            try
            {
                await EnsureOpenConnection();

                // Block if active listings exist
                string depQry = @"
            SELECT COUNT(*) FROM t_farmer_crop_listings
            WHERE c_catalog_product_id = @id
              AND c_status IN ('active','qc_requested','qc_scheduled','qc_passed','payment_pending')";

                using var depCmd = new NpgsqlCommand(depQry, _conn);
                depCmd.Parameters.AddWithValue("@id", id);
                long activeListings = Convert.ToInt64(await depCmd.ExecuteScalarAsync());

                if (activeListings > 0)
                    return (false, $"Cannot delete: {activeListings} active listing(s) reference this product. Resolve dependencies first.", null);

                // Block if open orders exist
                string orderQry = @"
            SELECT COUNT(*) FROM t_order_items oi
            JOIN t_vendor_orders vo ON vo.c_id = oi.c_order_id
            WHERE oi.c_catalog_product_id = @id
              AND vo.c_status IN ('placed','admin_confirmed','farmer_notified','dispatched','in_transit')";

                using var orderCmd = new NpgsqlCommand(orderQry, _conn);
                orderCmd.Parameters.AddWithValue("@id", id);
                long openOrders = Convert.ToInt64(await orderCmd.ExecuteScalarAsync());

                if (openOrders > 0)
                    return (false, $"Cannot delete: {openOrders} open order(s) reference this product. Resolve dependencies first.", null);

                // Fetch name + image_url before deletion
                string infoQry = "SELECT c_name, c_image_url FROM t_catalog_products WHERE c_id = @id";
                using var infoCmd = new NpgsqlCommand(infoQry, _conn);
                infoCmd.Parameters.AddWithValue("@id", id);

                string? existingImageUrl = null;

                using (var r = await infoCmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        existingImageUrl = r["c_image_url"] == DBNull.Value ? null : r["c_image_url"].ToString();
                    }
                }

                string delQry = "DELETE FROM t_catalog_products WHERE c_id = @id";
                using var delCmd = new NpgsqlCommand(delQry, _conn);
                delCmd.Parameters.AddWithValue("@id", id);
                await delCmd.ExecuteNonQueryAsync();

                // ✅ DELETE FROM ELASTICSEARCH
                await _elasticService.DeleteDocumentAsync<CatalogProductDocument>("catalog_products", (int)id);

                return (true, "Catalog product deleted successfully.", existingImageUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteCatalogProduct: {ex.Message}");
                return (false, "An error occurred while deleting the catalog product.", null);
            }
        }

        //  Toggle Active Status 
        public async Task<int> ToggleCatalogProductStatus(long id, bool isActive, long adminId)
        {
            try
            {
                await EnsureOpenConnection();

                string qry =
                    @"
                    UPDATE t_catalog_products
                    SET c_is_active  = @isActive,
                        c_updated_at = NOW()
                    WHERE c_id = @id";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@isActive", isActive);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ToggleCatalogProductStatus: {ex.Message}");
                return 0;
            }
        }

        //  Check Dependencies 
        public async Task<vm_CatalogDependency> CheckDependencies(long id)
        {
            var dep = new vm_CatalogDependency { ProductId = id };
            try
            {
                await EnsureOpenConnection();

                string qry =
                    @"
                    SELECT
                        (SELECT COUNT(*) FROM t_farmer_crop_listings
                         WHERE c_catalog_product_id = @id
                           AND c_status IN ('active','qc_requested','qc_scheduled','qc_passed','payment_pending')
                        ) AS active_listings,
                        (SELECT COUNT(*) FROM t_order_items oi
                         JOIN t_vendor_orders vo ON vo.c_id = oi.c_order_id
                         WHERE oi.c_catalog_product_id = @id
                           AND vo.c_status IN ('placed','admin_confirmed','farmer_notified','dispatched','in_transit')
                        ) AS open_orders";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    dep.ActiveListingsCount = Convert.ToInt32(reader["active_listings"]);
                    dep.OpenOrdersCount = Convert.ToInt32(reader["open_orders"]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckDependencies: {ex.Message}");
            }
            return dep;
        }

        // Get Distinct Categories
        public async Task<List<string>> GetDistinctCategories()
        {
            var list = new List<string>();
            try
            {
                await EnsureOpenConnection();
                string qry =
                    "SELECT DISTINCT c_category FROM t_catalog_products WHERE c_category IS NOT NULL ORDER BY c_category ASC";
                using var cmd = new NpgsqlCommand(qry, _conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader["c_category"].ToString()!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDistinctCategories: {ex.Message}");
            }
            return list;
        }

        //  Get Distinct Units 
        public async Task<List<string>> GetDistinctUnits()
        {
            var list = new List<string>();
            try
            {
                await EnsureOpenConnection();
                string qry =
                    "SELECT DISTINCT c_unit_of_measure FROM t_catalog_products WHERE c_unit_of_measure IS NOT NULL ORDER BY c_unit_of_measure ASC";
                using var cmd = new NpgsqlCommand(qry, _conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader["c_unit_of_measure"].ToString()!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDistinctUnits: {ex.Message}");
            }
            return list;
        }

        // Private: Map Reader
        private static vm_CatalogProduct MapReader(NpgsqlDataReader reader)
        {
            return new vm_CatalogProduct
            {
                Id = Convert.ToInt64(reader["id"]),
                Name = reader["name"].ToString()!,
                Category =
                    reader["category"] == DBNull.Value ? null : reader["category"].ToString(),
                UnitOfMeasure = reader["unit_of_measure"].ToString()!,
                Description =
                    reader["description"] == DBNull.Value ? null : reader["description"].ToString(),
                ImageUrl =
                    reader["image_url"] == DBNull.Value ? null : reader["image_url"].ToString(),
                QualityParameters =
                    reader["quality_parameters"] == DBNull.Value
                        ? null
                        : reader["quality_parameters"].ToString(),
                IsActive = Convert.ToBoolean(reader["is_active"]),
                CreatedBy = Convert.ToInt64(reader["created_by"]),
                CreatedByName =
                    reader["created_by_name"] == DBNull.Value
                        ? null
                        : reader["created_by_name"].ToString(),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"]),
            };
        }

        public async Task<CreateFOResult> CreateFieldOfficerAsync(
            CreateFieldOfficerViewModel model,
            int adminUserId
        )
        {
            try
            {
                // Safety checks
                if (model == null)
                {
                    return new CreateFOResult
                    {
                        Success = false,
                        Message = "Invalid request data.",
                    };
                }

                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    return new CreateFOResult { Success = false, Message = "Email is required." };
                }

                if (_conn == null)
                    throw new Exception("Database connection is null.");

                // Check email uniqueness
                if (await EmailExistsAsync(model.Email))
                {
                    return new CreateFOResult
                    {
                        Success = false,
                        Message = "An account with this email already exists.",
                    };
                }

                string tempPassword = GenerateSecurePassword();
                string passwordHash = HashPassword(tempPassword);

                await EnsureOpenConnection();

                using var tx = await _conn.BeginTransactionAsync();

                try
                {
                    // Insert User
                    string insertUserSql =
                        @"
                INSERT INTO t_users
                    (c_email, c_password_hash, c_role,
                     c_is_first_login, c_is_active,
                     c_is_approved, c_created_at)
                VALUES
                    (@email, @passwordHash, 'field_officer',
                     true, true, true, NOW())
                RETURNING c_id;";

                    int newUserId;

                    using (var cmd = new NpgsqlCommand(insertUserSql, _conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@email", model.Email.Trim().ToLower());

                        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);

                        newUserId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    // Insert Profile
                    string insertProfileSql =
                        @"
                INSERT INTO t_field_officer_profiles
                    (c_user_id, c_full_name, c_phone,
                     c_warehouse_id, c_assigned_region,
                     c_created_at)
                VALUES
                    (@userId, @fullName, @phone,
                     @warehouseId, @assignedRegion, NOW());";

                    using (var cmd = new NpgsqlCommand(insertProfileSql, _conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@userId", newUserId);

                        cmd.Parameters.AddWithValue("@fullName", model.FullName ?? "");

                        cmd.Parameters.AddWithValue(
                            "@phone",
                            string.IsNullOrWhiteSpace(model.Phone) ? DBNull.Value : model.Phone
                        );

                        cmd.Parameters.AddWithValue("@warehouseId", model.WarehouseId);

                        cmd.Parameters.AddWithValue(
                            "@assignedRegion",
                            string.IsNullOrWhiteSpace(model.AssignedRegion)
                                ? DBNull.Value
                                : model.AssignedRegion
                        );

                        await cmd.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();

                    return new CreateFOResult
                    {
                        Success = true,
                        Message = "Field Officer created successfully",
                        UserId = newUserId,
                        TempPassword = tempPassword,
                    };
                }
                catch (Exception)
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("CreateFieldOfficer Error: " + e.Message);

                return new CreateFOResult { Success = false, Message = e.Message };
            }
            finally
            {
                if (_conn != null && _conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        } // 

        // 2. LIST ALL FIELD OFFICERS
        // 

        public async Task<List<dynamic>> GetAllFieldOfficersAsync(
            string searchTerm = "",
            int pageNumber = 1
        )
        {
            int pageSize = 25;
            int offset = (pageNumber - 1) * pageSize;
            DataTable dt = new DataTable();

            string sql =
                @"
                SELECT
                    u.c_id                 AS UserId,
                    p.c_id                 AS ProfileId,
                    p.c_full_name          AS FullName,
                    u.c_email              AS Email,
                    p.c_phone              AS Phone,
                    COALESCE(w.c_name,'—') AS WarehouseName,
                    p.c_assigned_region    AS AssignedRegion,
                    u.c_is_active          AS IsActive,
                    u.c_is_first_login     AS IsFirstLogin,
                    u.c_created_at         AS CreatedAt
                FROM t_users u
                JOIN t_field_officer_profiles p ON p.c_user_id = u.c_id
                LEFT JOIN t_warehouses w ON w.c_id = p.c_warehouse_id
                WHERE u.c_role = 'field_officer'
                AND (
                    @search = '' OR
                    p.c_full_name       ILIKE @search OR
                    u.c_email           ILIKE @search OR
                    p.c_phone           ILIKE @search OR
                    p.c_assigned_region ILIKE @search
                )
                ORDER BY u.c_created_at DESC
                LIMIT @limit OFFSET @offset";

            await EnsureOpenConnection();
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            await _conn.CloseAsync();

            List<dynamic> list = new List<dynamic>();
            foreach (DataRow dr in dt.Rows)
            {
                list.Add(
                    new
                    {
                        UserId = Convert.ToInt32(dr["UserId"]),
                        ProfileId = Convert.ToInt32(dr["ProfileId"]),
                        FullName = dr["FullName"].ToString(),
                        Email = dr["Email"].ToString(),
                        Phone = dr["Phone"] == DBNull.Value ? null : dr["Phone"].ToString(),
                        WarehouseName = dr["WarehouseName"].ToString(),
                        AssignedRegion = dr["AssignedRegion"] == DBNull.Value
                            ? null
                            : dr["AssignedRegion"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"]),
                        IsFirstLogin = Convert.ToBoolean(dr["IsFirstLogin"]),
                        CreatedAt = Convert.ToDateTime(dr["CreatedAt"]),
                    }
                );
            }
            return list;
        }

        // 
        // 3. GET FO FULL DETAIL
        // 

        public async Task<DataSet> GetFOFullDetailAsync(int userId)
        {
            DataSet ds = new DataSet();

            string sql =
                @"
                -- FO User Info
                SELECT
                    u.c_id             AS UserId,
                    u.c_email          AS Email,
                    u.c_is_active      AS IsActive,
                    u.c_is_first_login AS IsFirstLogin,
                    u.c_created_at     AS CreatedAt
                FROM t_users u
                WHERE u.c_id = @uid AND u.c_role = 'field_officer';
 
                -- FO Profile + Warehouse
                SELECT
                    p.c_full_name        AS FullName,
                    p.c_phone            AS Phone,
                    p.c_assigned_region  AS AssignedRegion,
                    p.c_profile_image_url AS ProfileImageUrl,
                    p.c_created_at       AS ProfileCreatedAt,
                    w.c_name             AS WarehouseName,
                    w.c_district         AS WarehouseDistrict,
                    w.c_state            AS WarehouseState
                FROM t_field_officer_profiles p
                LEFT JOIN t_warehouses w ON w.c_id = p.c_warehouse_id
                WHERE p.c_user_id = @uid;
 
                -- Assigned Procurement Requests
                SELECT
                    pr.c_id                   AS RequestId,
                    pr.c_status               AS Status,
                    pr.c_requested_quantity    AS RequestedQty,
                    pr.c_unit                  AS Unit,
                    pr.c_fo_notes              AS Notes,
                    pr.c_created_at            AS CreatedAt,
                    fp.c_full_name             AS FarmerName,
                    cp.c_name                  AS CropName
                FROM t_procurement_requests pr
                JOIN t_farmer_profiles fp       ON fp.c_id  = pr.c_farmer_id
                JOIN t_farmer_crop_listings cl  ON cl.c_id  = pr.c_crop_listing_id
                JOIN t_catalog_products cp      ON cp.c_id  = cl.c_catalog_product_id
                WHERE pr.c_assigned_fo_id = (
                    SELECT c_id FROM t_field_officer_profiles WHERE c_user_id = @uid
                )
                ORDER BY pr.c_created_at DESC;
 
                -- Quality Inspections submitted by this FO
                SELECT
                    qi.c_id               AS InspectionId,
                    qi.c_grade            AS Grade,
                    qi.c_passed           AS Passed,
                    qi.c_accepted_quantity AS AcceptedQty,
                    qi.c_rejected_quantity AS RejectedQty,
                    qi.c_submitted_at     AS SubmittedAt,
                    cp.c_name             AS CropName
                FROM t_quality_inspection_forms qi
                JOIN t_procurement_requests pr  ON pr.c_id  = qi.c_procurement_request_id
                JOIN t_farmer_crop_listings cl  ON cl.c_id  = pr.c_crop_listing_id
                JOIN t_catalog_products cp      ON cp.c_id  = cl.c_catalog_product_id
                WHERE qi.c_fo_id = (
                    SELECT c_id FROM t_field_officer_profiles WHERE c_user_id = @uid
                )
                ORDER BY qi.c_submitted_at DESC;";

            await EnsureOpenConnection();
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
            await Task.Run(() => da.Fill(ds));

            return ds;
        }

        // 
        // 5. VALIDATE FO LOGIN (Requirement #3 — only admin-created accounts)
        // 

        public async Task<DataTable> GetActiveWarehousesAsync()
        {
            DataTable dt = new DataTable();

            string sql =
                @"
                SELECT
                    c_id       AS WarehouseId,
                    c_name     AS Name,
                    c_district AS District,
                    c_state    AS State
                FROM t_warehouses
                WHERE c_is_active = true
                ORDER BY c_name";

            await EnsureOpenConnection();
            using var cmd = new NpgsqlCommand(sql, _conn);

            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            await _conn.CloseAsync();

            return dt;
        }

        // 
        // 8. GET AUDIT LOGS (Requirement #5)
        // 

        private async Task<bool> EmailExistsAsync(string email)
        {
            string sql = "SELECT COUNT(1) FROM t_users WHERE c_email = @email";

            await EnsureOpenConnection();
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@email", email.Trim().ToLower());

            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            await _conn.CloseAsync();

            return count > 0;
        }

        private static string GenerateSecurePassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghjkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@$!%*?&";
            const string all = upper + lower + digits + special;

            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[12];
            rng.GetBytes(bytes);

            var sb = new StringBuilder();
            // Guarantee at least one from each required class
            sb.Append(upper[bytes[0] % upper.Length]);
            sb.Append(lower[bytes[1] % lower.Length]);
            sb.Append(digits[bytes[2] % digits.Length]);
            sb.Append(special[bytes[3] % special.Length]);

            for (int i = 4; i < 12; i++)
                sb.Append(all[bytes[i] % all.Length]);

            // Shuffle to avoid predictable position of each class
            var chars = sb.ToString().ToCharArray();
            rng.GetBytes(bytes);
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = bytes[i % bytes.Length] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, 11);
        }

        public async Task<List<FieldOfficerRow>> GetFieldOfficersAsync()
        {
            var list = new List<FieldOfficerRow>();
            await EnsureOpenConnection();

            // This query joins the warehouse and calculates the stats the frontend needs
            var sql = @"
        SELECT 
            p.c_id, 
            'FO-' || p.c_id AS fo_code,
            p.c_full_name, 
            p.c_phone,
            u.c_email, 
            p.c_assigned_region, 
            w.c_name AS warehouse_name,
            p.c_warehouse_id,
            (SELECT COUNT(*) FROM t_procurement_requests pr WHERE pr.c_assigned_fo_id = p.c_id) AS insp_count,
            (SELECT COUNT(*) FROM t_procurement_requests pr WHERE pr.c_assigned_fo_id = p.c_id AND pr.c_quality_passed = true) AS passed_count,
            (SELECT COUNT(*) FROM t_warehouse_lots wl WHERE wl.c_fo_id = p.c_id) AS lots_managed,
            u.c_is_active, 
            p.c_created_at
        FROM t_users u
        JOIN t_field_officer_profiles p ON u.c_id = p.c_user_id
        LEFT JOIN t_warehouses w ON p.c_warehouse_id = w.c_id
        WHERE u.c_role = 'field_officer'
        ORDER BY p.c_created_at DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new FieldOfficerRow
                {
                    Id = Convert.ToInt32(reader["c_id"]),
                    FoCode = reader["fo_code"].ToString()!,
                    FullName = reader["c_full_name"].ToString()!,
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Email = reader["c_email"].ToString()!,
                    AssignedRegion = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    WarehouseName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    WarehouseId = reader.IsDBNull(7) ? null : Convert.ToInt32(reader["c_warehouse_id"]),
                    InspectionCount = Convert.ToInt32(reader["insp_count"]),
                    PassedCount = Convert.ToInt32(reader["passed_count"]),
                    LotsManaged = Convert.ToInt32(reader["lots_managed"]),
                    IsActive = reader.GetBoolean(11),
                    CreatedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                });
            }
            return list;
        }

        public async Task<FoDetailDto?> GetFoDetailAsync(long foId)
        {
            FoDetailDto? dto = null;

            var sql = @"
                SELECT
                    p.c_id,
                    'FO-' || p.c_id                                               AS fo_code,
                    p.c_full_name,
                    u.c_email,
                    COALESCE(p.c_phone, '')                                        AS phone,
                    COALESCE(p.c_assigned_region, '')                             AS assigned_region,
                    COALESCE(w.c_name, '')                                        AS warehouse_name,
                    p.c_warehouse_id,
                    u.c_is_active,
                    p.c_created_at,
                    (SELECT COUNT(*) FROM t_quality_inspection_forms qf
                     WHERE qf.c_fo_id = p.c_id)                                  AS total_inspections,
                    (SELECT COUNT(*) FROM t_quality_inspection_forms qf
                     WHERE qf.c_fo_id = p.c_id AND qf.c_passed = true)           AS passed_inspections,
                    (SELECT COUNT(*) FROM t_warehouse_lots wl
                     WHERE wl.c_fo_id = p.c_id)                                  AS lots_managed
                FROM t_field_officer_profiles p
                JOIN t_users u ON u.c_id = p.c_user_id
                LEFT JOIN t_warehouses w ON w.c_id = p.c_warehouse_id
                WHERE p.c_id = @foId";

            await EnsureOpenConnection();
            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("foId", foId);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                dto = new FoDetailDto
                {
                    Id = r.GetInt64(r.GetOrdinal("c_id")),
                    FoCode = r.GetString(r.GetOrdinal("fo_code")),
                    FullName = r.GetString(r.GetOrdinal("c_full_name")),
                    Email = r.GetString(r.GetOrdinal("c_email")),
                    Phone = r.GetString(r.GetOrdinal("phone")),
                    AssignedRegion = r.GetString(r.GetOrdinal("assigned_region")),
                    WarehouseName = r.GetString(r.GetOrdinal("warehouse_name")),
                    WarehouseId = r.IsDBNull(r.GetOrdinal("c_warehouse_id")) ? null : r.GetInt64(r.GetOrdinal("c_warehouse_id")),
                    IsActive = r.GetBoolean(r.GetOrdinal("c_is_active")),
                    CreatedAt = r.IsDBNull(r.GetOrdinal("c_created_at")) ? null : r.GetDateTime(r.GetOrdinal("c_created_at")),
                    TotalInspections = Convert.ToInt32(r["total_inspections"]),
                    PassedInspections = Convert.ToInt32(r["passed_inspections"]),
                    LotsManaged = Convert.ToInt32(r["lots_managed"])
                };
            }
            return dto;
        }

        // GET INSPECTIONS BY FO ID
        public async Task<List<FoInspectionRow>> GetFoInspectionsAsync(long foId)
        {
            var list = new List<FoInspectionRow>();

            var sql = @"
        SELECT
            qf.c_id                                         AS inspection_id,
            qf.c_procurement_request_id,
            fp.c_full_name                                  AS farmer_name,
            COALESCE(fp.c_phone, '')                        AS farmer_phone,
            cp.c_name                                       AS crop_name,
            COALESCE(cp.c_category, '')                     AS crop_category,
            COALESCE(qf.c_variety, '')                      AS variety,
            COALESCE(qf.c_grade, '')                        AS grade,
            COALESCE(qf.c_moisture_pct, 0)                  AS moisture_pct,
            COALESCE(qf.c_foreign_matter_pct, 0)            AS foreign_matter_pct,
            COALESCE(qf.c_weight_checked_kg, 0)             AS weight_checked_kg,
            COALESCE(qf.c_accepted_quantity, 0)             AS accepted_quantity,
            COALESCE(qf.c_rejected_quantity, 0)             AS rejected_quantity,
            qf.c_pest_disease_observed,
            qf.c_defects_noted,
            qf.c_remarks,
            qf.c_passed,
            COALESCE(pr.c_status, '')                       AS procurement_status,
            qf.c_submitted_at
        FROM t_quality_inspection_forms qf
        JOIN t_procurement_requests pr    ON pr.c_id = qf.c_procurement_request_id
        JOIN t_farmer_profiles fp         ON fp.c_id = pr.c_farmer_id
        JOIN t_farmer_crop_listings fcl   ON fcl.c_id = pr.c_crop_listing_id
        JOIN t_catalog_products cp        ON cp.c_id = fcl.c_catalog_product_id
        WHERE qf.c_fo_id = @foId
        ORDER BY qf.c_submitted_at DESC";

            await EnsureOpenConnection();

            // 1. Fetch the primary inspection data
            await using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("foId", foId);

                await using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        list.Add(new FoInspectionRow
                        {
                            InspectionId = r.GetInt64(r.GetOrdinal("inspection_id")),
                            ProcurementRequestId = r.GetInt64(r.GetOrdinal("c_procurement_request_id")),
                            FarmerName = r.GetString(r.GetOrdinal("farmer_name")),
                            FarmerPhone = r.GetString(r.GetOrdinal("farmer_phone")),
                            CropName = r.GetString(r.GetOrdinal("crop_name")),
                            CropCategory = r.GetString(r.GetOrdinal("crop_category")),
                            Variety = r.GetString(r.GetOrdinal("variety")),
                            Grade = r.GetString(r.GetOrdinal("grade")),
                            MoisturePct = r.GetDecimal(r.GetOrdinal("moisture_pct")),
                            ForeignMatterPct = r.GetDecimal(r.GetOrdinal("foreign_matter_pct")),
                            WeightCheckedKg = r.GetDecimal(r.GetOrdinal("weight_checked_kg")),
                            AcceptedQuantity = r.GetDecimal(r.GetOrdinal("accepted_quantity")),
                            RejectedQuantity = r.GetDecimal(r.GetOrdinal("rejected_quantity")),
                            PestDiseaseObserved = r.IsDBNull(r.GetOrdinal("c_pest_disease_observed")) ? null : r.GetString(r.GetOrdinal("c_pest_disease_observed")),
                            DefectsNoted = r.IsDBNull(r.GetOrdinal("c_defects_noted")) ? null : r.GetString(r.GetOrdinal("c_defects_noted")),
                            Remarks = r.IsDBNull(r.GetOrdinal("c_remarks")) ? null : r.GetString(r.GetOrdinal("c_remarks")),
                            Passed = r.GetBoolean(r.GetOrdinal("c_passed")),
                            ProcurementStatus = r.GetString(r.GetOrdinal("procurement_status")),
                            SubmittedAt = r.IsDBNull(r.GetOrdinal("c_submitted_at")) ? null : r.GetDateTime(r.GetOrdinal("c_submitted_at")),
                            Photos = new List<string>() // Initialize to avoid null reference in UI
                        });
                    }
                } // Reader is disposed here, connection is now free for the next command
            }

            // 2. Fetch and attach photos
            if (list.Count > 0)
            {
                var inspectionIds = list.Select(x => x.InspectionId).ToArray();

                var photoSql = @"
            SELECT c_inspection_id, c_photo_url 
            FROM t_inspection_photos 
            WHERE c_inspection_id = ANY(@ids) 
            ORDER BY c_id";

                await using var cmd2 = new NpgsqlCommand(photoSql, _conn);
                cmd2.Parameters.AddWithValue("ids", inspectionIds);

                await using var r2 = await cmd2.ExecuteReaderAsync();

                // Group photos by InspectionId in memory
                var photoMap = new Dictionary<long, List<string>>();
                while (await r2.ReadAsync())
                {
                    var id = r2.GetInt64(0);
                    var url = r2.GetString(1);

                    if (!photoMap.ContainsKey(id))
                        photoMap[id] = new List<string>();

                    photoMap[id].Add(url);
                }

                // Map photos back to the original list
                foreach (var row in list)
                {
                    if (photoMap.TryGetValue(row.InspectionId, out var urls))
                    {
                        row.Photos = urls;
                    }
                }
            }

            return list;
        }
        // GET WAREHOUSE SLOTS BY FO ID
        // Slots are linked via the procurement request → crop listing → slot booking
        public async Task<List<FoWarehouseSlotRow>> GetFoWarehouseSlotsAsync(long foId)
        {
            var list = new List<FoWarehouseSlotRow>();

            var sql = @"
                SELECT
                    wsb.c_id                                        AS slot_id,
                    fp.c_full_name                                  AS farmer_name,
                    COALESCE(fp.c_phone, '')                        AS farmer_phone,
                    cp.c_name                                       AS crop_name,
                    w.c_name                                        AS warehouse_name,
                    wsb.c_slot_date,
                    wsb.c_slot_time_start,
                    wsb.c_slot_time_end,
                    COALESCE(wsb.c_check_in_status, '')             AS check_in_status,
                    COALESCE(wsb.c_status, '')                      AS booking_status,
                    wsb.c_booked_at,
                    wsb.c_arrived_at,
                    wsb.c_notes,
                    wl.c_id                                         AS lot_id,
                    wl.c_grade                                      AS lot_grade,
                    wl.c_quantity_accepted,
                    wl.c_status                                     AS lot_status
                FROM t_procurement_requests pr
                JOIN t_farmer_crop_listings fcl   ON fcl.c_id = pr.c_crop_listing_id
                JOIN t_catalog_products cp        ON cp.c_id = fcl.c_catalog_product_id
                JOIN t_farmer_profiles fp         ON fp.c_id = pr.c_farmer_id
                JOIN t_warehouse_slot_bookings wsb ON wsb.c_crop_listing_id = fcl.c_id
                JOIN t_warehouses w               ON w.c_id = wsb.c_warehouse_id
                LEFT JOIN t_warehouse_lots wl     ON wl.c_fo_id = pr.c_assigned_fo_id
                                                  AND wl.c_farmer_id = pr.c_farmer_id
                                                  AND wl.c_catalog_product_id = fcl.c_catalog_product_id
                WHERE pr.c_assigned_fo_id = @foId
                ORDER BY wsb.c_slot_date DESC, wsb.c_slot_time_start DESC";

            await EnsureOpenConnection();
            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("foId", foId);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new FoWarehouseSlotRow
                {
                    SlotId = r.GetInt64(r.GetOrdinal("slot_id")),
                    FarmerName = r.GetString(r.GetOrdinal("farmer_name")),
                    FarmerPhone = r.GetString(r.GetOrdinal("farmer_phone")),
                    CropName = r.GetString(r.GetOrdinal("crop_name")),
                    WarehouseName = r.GetString(r.GetOrdinal("warehouse_name")),
                    SlotDate = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("c_slot_date"))),
                    SlotTimeStart = TimeOnly.FromTimeSpan(r.GetTimeSpan(r.GetOrdinal("c_slot_time_start"))),
                    SlotTimeEnd = TimeOnly.FromTimeSpan(r.GetTimeSpan(r.GetOrdinal("c_slot_time_end"))),
                    CheckInStatus = r.GetString(r.GetOrdinal("check_in_status")),
                    BookingStatus = r.GetString(r.GetOrdinal("booking_status")),
                    BookedAt = r.IsDBNull(r.GetOrdinal("c_booked_at")) ? null : r.GetDateTime(r.GetOrdinal("c_booked_at")),
                    ArrivedAt = r.IsDBNull(r.GetOrdinal("c_arrived_at")) ? null : r.GetDateTime(r.GetOrdinal("c_arrived_at")),
                    Notes = r.IsDBNull(r.GetOrdinal("c_notes")) ? null : r.GetString(r.GetOrdinal("c_notes")),
                    LotId = r.IsDBNull(r.GetOrdinal("lot_id")) ? null : r.GetInt64(r.GetOrdinal("lot_id")),
                    LotGrade = r.IsDBNull(r.GetOrdinal("lot_grade")) ? null : r.GetString(r.GetOrdinal("lot_grade")),
                    QuantityAccepted = r.IsDBNull(r.GetOrdinal("c_quantity_accepted")) ? null : r.GetDecimal(r.GetOrdinal("c_quantity_accepted")),
                    LotStatus = r.IsDBNull(r.GetOrdinal("lot_status")) ? null : r.GetString(r.GetOrdinal("lot_status"))
                });
            }
            return list;
        }

        public async Task<(bool Success, string Message)> ToggleFoStatusAsync(string adminId, string foId, bool isActive, string reason)
        {
            await EnsureOpenConnection();

            var cmd = new NpgsqlCommand(
                @"UPDATE t_users SET c_is_active = @isActive 
              WHERE c_id = (SELECT c_user_id FROM t_field_officer_profiles WHERE c_id = @id)", _conn);

            cmd.Parameters.AddWithValue("@isActive", isActive);
            cmd.Parameters.AddWithValue("@id", long.Parse(foId));

            int affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0) return (false, "Field Officer not found.");

            string action = isActive ? "ACTIVATE_FO" : "DEACTIVATE_FO";

            // SIMULATED EMAIL
            Console.WriteLine($"[SIMULATED EMAIL] FO Account {(isActive ? "Activated" : "Deactivated")}. Reason: {reason}");

            return (true, $"Field Officer account has been {(isActive ? "activated" : "deactivated")}.");
        }

        public async Task<FieldOfficerPerformance> GetFoPerformanceAsync(string foId)
        {
            await EnsureOpenConnection();

            var perf = new FieldOfficerPerformance { FieldOfficerId = foId };

            var cmd = new NpgsqlCommand(
                @"SELECT 
                COUNT(*) FILTER (WHERE c_status = 'completed') AS completed,
                COUNT(*) FILTER (WHERE c_status IN ('pending', 'scheduled', 'in_progress')) AS pending,
                COALESCE(AVG(CASE WHEN c_quality_passed THEN 100.0 ELSE 0.0 END), 0) as acceptance_rate
              FROM t_procurement_requests WHERE c_assigned_fo_id = @id", _conn);
            cmd.Parameters.AddWithValue("@id", long.Parse(foId));

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                perf.TotalInspectionsCompleted = Convert.ToInt32(reader["completed"]);
                perf.InspectionsPending = Convert.ToInt32(reader["pending"]);
                perf.AcceptanceRate = Math.Round(Convert.ToDecimal(reader["acceptance_rate"]), 2);
            }
            return perf;
        }

        // Add this method inside FieldOfficerAdminHelper.cs
        public async Task<FieldOfficerAnalyticsKpi> GetFieldOfficerAnalyticsKpiAsync()
        {
            var kpi = new FieldOfficerAnalyticsKpi();
            await EnsureOpenConnection();

            // Single query to calculate all 4 Field Officer KPIs at once
            var sql = @"
            SELECT 
                (SELECT COUNT(c_id) FROM t_users WHERE c_role = 'field_officer') AS total_fos,
                (SELECT COUNT(c_id) FROM t_procurement_requests WHERE c_status = 'completed') AS assessments_done,
                (SELECT COUNT(c_id) FROM t_procurement_requests WHERE c_status IN ('pending', 'scheduled', 'in_progress')) AS pending_reviews,
                (SELECT 
                    CASE WHEN COUNT(c_id) = 0 THEN 0 
                    ELSE ROUND((COUNT(c_id) FILTER (WHERE c_quality_passed = true) * 100.0) / COUNT(c_id), 1) 
                    END 
                 FROM t_procurement_requests WHERE c_status = 'completed') AS accuracy_rate";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                kpi.TotalFieldOfficers = Convert.ToInt32(reader["total_fos"]);
                kpi.AssessmentsDone = Convert.ToInt32(reader["assessments_done"]);
                kpi.PendingReviews = Convert.ToInt32(reader["pending_reviews"]);
                kpi.AccuracyRate = Convert.ToDecimal(reader["accuracy_rate"]);
            }

            return kpi;
        }

        private async Task LogAdminActionAsync(NpgsqlConnection conn, string adminId, string actionType, string targetType, long targetId, string reason)
        {
            var cmd = new NpgsqlCommand(
                @"INSERT INTO t_admin_audit_logs (c_admin_id, c_action_type, c_target_type, c_target_id, c_reason)
              VALUES (@adminId, @action, @tType, @tId, @reason)", conn);

            cmd.Parameters.AddWithValue("@adminId", long.Parse(adminId));
            cmd.Parameters.AddWithValue("@action", actionType);
            cmd.Parameters.AddWithValue("@tType", targetType);
            cmd.Parameters.AddWithValue("@tId", targetId);
            cmd.Parameters.AddWithValue("@reason", reason ?? "");

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<VendorRow>> GetVendorsAsync()
        {
            var list = new List<VendorRow>();

            await EnsureOpenConnection();

            var sql = @"SELECT p.c_id, p.c_business_name, p.c_contact_person, p.c_gstin, p.c_onboarding_status, p.c_created_at,
                           COALESCE((SELECT SUM(c_total_amount) FROM t_vendor_orders o WHERE o.c_vendor_id = p.c_id), 0) as total_spent
                    FROM t_vendor_profiles p
                    ORDER BY p.c_created_at DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new VendorRow
                {
                    Id = reader[0].ToString()!,
                    BusinessName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ContactName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    GstNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Status = reader.IsDBNull(4) ? "pending_approval" : reader.GetString(4),
                    RegistrationDate = reader.GetDateTime(5),
                    TotalOrderValue = reader.GetDecimal(6)
                });
            }
            return list;
        }


        public async Task<VendorRow?> GetVendorByIdAsync(int id)
        {
            await EnsureOpenConnection();

            var sql = @"SELECT p.c_id, p.c_business_name, p.c_contact_person, p.c_gstin, 
                p.c_onboarding_status, p.c_created_at,
                COALESCE((SELECT SUM(c_total_amount) 
                FROM t_vendor_orders o 
                WHERE o.c_vendor_id = p.c_id), 0) as total_spent
            FROM t_vendor_profiles p
            WHERE p.c_id = @id";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new VendorRow
                {
                    Id = reader[0].ToString()!,
                    BusinessName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ContactName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    GstNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Status = reader.IsDBNull(4) ? "pending_approval" : reader.GetString(4),
                    RegistrationDate = reader.GetDateTime(5),
                    TotalOrderValue = reader.GetDecimal(6)
                };
            }

            return null;
        }
        public async Task<(bool Success, string Message)> UpdateVendorStatusAsync(string adminId, string vendorId, string status, string reason)
        {

            await EnsureOpenConnection();

            var cmd = new NpgsqlCommand("UPDATE t_vendor_profiles SET c_onboarding_status = @status WHERE c_id = @id", _conn);
            cmd.Parameters.AddWithValue("@status", status); // 'active', 'suspended', 'pending_approval'
            cmd.Parameters.AddWithValue("@id", long.Parse(vendorId));

            int affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0) return (false, "Vendor not found.");


            if (status == "active")
            {
                Console.WriteLine($"[SIMULATED EMAIL] Sent Welcome Email to Vendor ID {vendorId}");
            }

            return (true, $"Vendor status updated to {status}.");
        }

        public async Task<List<DemandTrendPoint>> GetVendorDemandTrendAsync(string vendorId, string timeFrame, string? cropType)
        {
            var points = new List<DemandTrendPoint>();

            await EnsureOpenConnection();

            string truncScale = timeFrame.ToLower() switch
            {
                "weekly" => "week",
                "yearly" => "year",
                _ => "month"
            };

            var sql = $@"SELECT DATE_TRUNC('{truncScale}', vo.c_ordered_at) as time_label, 
                            SUM(oi.c_quantity) as vol
                     FROM t_vendor_orders vo
                     JOIN t_order_items oi ON vo.c_id = oi.c_order_id
                     LEFT JOIN t_catalog_products cp ON oi.c_catalog_product_id = cp.c_id
                     WHERE vo.c_vendor_id = @id
                     AND (@crop IS NULL OR cp.c_name ILIKE '%' || @crop || '%')
                     GROUP BY time_label
                     ORDER BY time_label ASC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@id", long.Parse(vendorId));
            cmd.Parameters.AddWithValue("@crop", (object?)cropType ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                points.Add(new DemandTrendPoint
                {
                    TimeLabel = reader.GetDateTime(0).ToString(truncScale == "month" ? "MMM yyyy" : (truncScale == "year" ? "yyyy" : "dd MMM yyyy")),
                    TotalVolume = reader.GetDecimal(1)
                });
            }
            return points;
        }

        public async Task<List<VendorReviewRow>> GetPendingReviewsAsync()
        {
            var list = new List<VendorReviewRow>();

            await EnsureOpenConnection();

            var sql = @"SELECT r.c_id, v.c_business_name, r.c_rating, r.c_review_text, r.c_status, r.c_created_at 
                    FROM t_reviews r
                    JOIN t_vendor_profiles v ON r.c_vendor_id = v.c_id
                    WHERE r.c_status = 'pending'
                    ORDER BY r.c_created_at ASC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new VendorReviewRow
                {
                    ReviewId = reader[0].ToString()!,
                    VendorName = reader.GetString(1),
                    Rating = reader.GetInt32(2),
                    ReviewText = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Status = reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
            return list;
        }

        public async Task<(bool Success, string Message)> ModerateReviewAsync(string adminId, string reviewId, string status)
        {

            await EnsureOpenConnection();

            var cmd = new NpgsqlCommand(
                "UPDATE t_reviews SET c_status = @status, c_moderated_by = @adminId, c_moderated_at = NOW() WHERE c_id = @id", _conn);

            cmd.Parameters.AddWithValue("@status", status); // 'approved' or 'rejected'
            cmd.Parameters.AddWithValue("@adminId", long.Parse(adminId));
            cmd.Parameters.AddWithValue("@id", long.Parse(reviewId));

            int affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0 ? (true, $"Review {status} successfully.") : (false, "Review not found.");
        }

        // Add this method inside VendorAdminHelper.cs
        public async Task<VendorAnalyticsKpi> GetVendorAnalyticsKpiAsync()
        {
            var kpi = new VendorAnalyticsKpi();

            await EnsureOpenConnection();

            // Single query to calculate all 4 KPIs at once
            var sql = @"
            SELECT 
                (SELECT COUNT(c_id) FROM t_vendor_profiles) AS total_vendors,
                (SELECT COALESCE(SUM(c_total_amount), 0) FROM t_vendor_orders WHERE c_status != 'cancelled') AS total_orders_value,
                (SELECT COUNT(c_id) FROM t_vendor_orders) AS orders_placed,
                (SELECT 
                    CASE WHEN COUNT(c_id) = 0 THEN 0 
                    ELSE ROUND((COUNT(c_id) FILTER (WHERE c_status = 'delivered') * 100.0) / COUNT(c_id), 1) 
                    END 
                 FROM t_vendor_orders) AS fulfillment_rate";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                kpi.TotalVendors = Convert.ToInt32(reader["total_vendors"]);
                kpi.TotalOrdersValue = Convert.ToDecimal(reader["total_orders_value"]);
                kpi.OrdersPlaced = Convert.ToInt32(reader["orders_placed"]);
                kpi.FulfillmentRate = Convert.ToDecimal(reader["fulfillment_rate"]);
            }

            return kpi;
        }

        public async Task<WarehouseKpi> GetWarehouseAnalyticsKpiAsync()
        {
            var kpi = new WarehouseKpi();
            await EnsureOpenConnection();

            var sql = @"
    WITH wh_stats AS (
        SELECT 
            w.c_id,
            w.c_daily_capacity,
            COALESCE((
                SELECT SUM(c_quantity_remaining) 
                FROM t_warehouse_lots 
                WHERE c_warehouse_id = w.c_id 
                  AND c_status NOT IN ('sold','dispatched','damaged','expired')
            ), 0) / 1000.0 AS used_qty_mt   -- Convert KG to MT here!
        FROM t_warehouses w
        WHERE w.c_is_active = true
    )
    SELECT
        COUNT(*)                                                        AS total_wh,
        COALESCE(SUM(c_daily_capacity), 0)                             AS total_cap,
        CASE WHEN SUM(c_daily_capacity) = 0 THEN 0
             ELSE ROUND((SUM(used_qty_mt) / SUM(c_daily_capacity)) * 100.0, 1)
        END                                                             AS avg_utilization,
        COUNT(*) FILTER (
            WHERE c_daily_capacity > 0 
              AND (used_qty_mt / c_daily_capacity) > 0.85
        )                                                               AS near_cap_count
    FROM wh_stats";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                kpi.TotalWarehouses = Convert.ToInt32(reader["total_wh"]);
                kpi.TotalCapacityMt = Convert.ToDecimal(reader["total_cap"]);
                kpi.AverageUtilization = Convert.ToDecimal(reader["avg_utilization"]);
                kpi.NearCapacityCount = Convert.ToInt32(reader["near_cap_count"]);
            }
            return kpi;
        }

        public async Task<List<object>> GetWarehousesAsync()
        {
            var list = new List<object>();
            await EnsureOpenConnection();

            string sql = @"
        SELECT 
            w.c_id, 
            w.c_name, 
            w.c_state, 
            w.c_district, 
            w.c_is_active,
            COALESCE(w.c_daily_capacity, 0) AS TotalCapacity,
            COALESCE((
                SELECT SUM(
                    CASE 
                        WHEN LOWER(c_unit) = 'kg' THEN c_quantity_remaining / 1000.0
                        WHEN LOWER(c_unit) = 'quintal' THEN c_quantity_remaining / 10.0
                        ELSE c_quantity_remaining 
                    END
                ) 
                FROM t_warehouse_lots 
                WHERE c_warehouse_id = w.c_id AND c_status = 'available'
            ), 0) AS UsedCapacity
        FROM t_warehouses w
        ORDER BY w.c_id";

            using var cmd = new NpgsqlCommand(sql, _conn);
            using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                // FIXED: Using Convert.ToDecimal instead of r.GetDecimal prevents casting crashes!
                decimal totalCap = r["TotalCapacity"] == DBNull.Value ? 0 : Convert.ToDecimal(r["TotalCapacity"]);
                decimal usedCap = r["UsedCapacity"] == DBNull.Value ? 0 : Convert.ToDecimal(r["UsedCapacity"]);
                decimal availableCap = totalCap - usedCap;

                decimal utilPct = totalCap > 0 ? Math.Round((usedCap / totalCap) * 100, 1) : 0;

                list.Add(new
                {
                    Id = Convert.ToInt32(r["c_id"]),
                    Name = r["c_name"].ToString(),
                    State = r["c_state"] == DBNull.Value ? "" : r["c_state"].ToString(),
                    District = r["c_district"] == DBNull.Value ? "" : r["c_district"].ToString(),
                    TotalCapacity = totalCap,
                    Used = usedCap,
                    IsActive = r["c_is_active"],
                    Available = availableCap,
                    Utilization = utilPct,
                    Status = "operational"
                });
            }
            return list;
        }

        public async Task<WarehouseDetailInfo?> GetWarehouseDetailsAsync(long id)
        {
            await EnsureOpenConnection();
            var sql = @"
                    SELECT w.c_id, w.c_name, w.c_address, w.c_state, w.c_district, w.c_is_active,
                fo.c_full_name  AS manager_name,
                fo.c_phone      AS manager_phone
            FROM t_warehouses w
            LEFT JOIN t_field_officer_profiles fo ON fo.c_warehouse_id = w.c_id
            WHERE w.c_id = @id
            LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return null;

            return new WarehouseDetailInfo
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Location = reader.IsDBNull(2) ? null : reader.GetString(2),
                State = reader.IsDBNull(3) ? null : reader.GetString(3),
                District = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                // No manager/temp/cert columns in schema — return null
                ManagerName = (string)reader["manager_name"],
                Temperature = null,
                Certification = null
            };
        }

        public async Task<List<WarehouseStockItem>> GetWarehouseStockAsync(long id)
        {
            var list = new List<WarehouseStockItem>();
            await EnsureOpenConnection();

            // Groups individual lots together by crop and grade, converts total to MT!
            var sql = @"
    WITH aggregated_stock AS (
        SELECT 
            cp.c_name                   AS product_name,
            cp.c_category               AS category,
            wl.c_grade                  AS grade,
            SUM(wl.c_quantity_remaining) AS total_quantity_kg,
            MAX(wl.c_updated_at)        AS last_updated
        FROM t_warehouse_lots wl
        JOIN t_catalog_products cp ON cp.c_id = wl.c_catalog_product_id
        WHERE wl.c_warehouse_id = @id
          AND wl.c_status NOT IN ('sold','dispatched','damaged','expired')
        GROUP BY cp.c_name, cp.c_category, wl.c_grade
    ),
    total_warehouse_stock AS (
        SELECT SUM(total_quantity_kg) as grand_total FROM aggregated_stock
    )
    SELECT 
        a.product_name,
        a.category,
        a.grade,
        (a.total_quantity_kg / 1000.0) AS quantity_mt,
        'MT' AS unit,
        a.last_updated,
        CASE WHEN t.grand_total > 0 
             THEN ROUND((a.total_quantity_kg * 100.0) / t.grand_total, 1)
             ELSE 0 
        END AS pct_of_total
    FROM aggregated_stock a
    CROSS JOIN total_warehouse_stock t
    ORDER BY a.total_quantity_kg DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new WarehouseStockItem
                {
                    ProductName = reader.GetString(0),
                    Category = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Grade = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Quantity = Math.Round(reader.GetDecimal(3), 2),
                    Unit = reader.GetString(4),
                    LastUpdated = reader.GetDateTime(5),
                    PercentageOfTotalStock = reader.GetDecimal(6)
                });
            }
            return list;
        }
        public async Task<(bool Success, string Message)> CreateWarehouseAsync(WarehouseCreateRequest req)
        {
            try
            {
                await EnsureOpenConnection();
                var sql = @"
            INSERT INTO t_warehouses (c_name, c_address, c_state, c_district, c_daily_capacity, c_is_active)
            VALUES (@name, @address, @state, @district, @capacity, true)";

                await using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@name", req.Name);
                cmd.Parameters.AddWithValue("@address", (object?)req.LocationAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@state", (object?)req.Region ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@district", (object?)req.Type ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@capacity", req.CapacityMt);
                await cmd.ExecuteNonQueryAsync();
                return (true, "Warehouse created successfully.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string Message)> UpdateWarehouseAsync(long id, WarehouseUpdateRequest req)
        {
            try
            {
                await EnsureOpenConnection();
                var sql = @"
            UPDATE t_warehouses SET
                c_name           = @name,
                c_address        = @address,
                c_state          = @state,
                c_district       = @district,
                c_daily_capacity = @capacity,
                c_is_active      = @isActive
            WHERE c_id = @id";

                await using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@name", req.Name);
                cmd.Parameters.AddWithValue("@address", (object?)req.LocationAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@state", (object?)req.Region ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@district", (object?)req.Type ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@capacity", req.CapacityMt);
                cmd.Parameters.AddWithValue("@isActive", req.IsActive);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                return (true, "Warehouse updated successfully.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string Message)> ToggleWarehouseStatusAsync(long id, bool isActive)
        {
            try
            {
                await EnsureOpenConnection();
                var sql = "UPDATE t_warehouses SET c_is_active = @isActive WHERE c_id = @id";
                await using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@isActive", isActive);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                return (true, isActive ? "Warehouse activated." : "Warehouse deactivated.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ADD THIS INSIDE YOUR HELPER CLASS
        public async Task<object> GetWarehouseInventoryAsync(int warehouseId)
        {
            await EnsureOpenConnection();

            // 1. Get the KPIs (Capacity, Used, Available)
            string kpiSql = @"
        SELECT 
            w.c_daily_capacity AS TotalCapacity,
            COALESCE((
                SELECT SUM(
                    CASE 
                        WHEN LOWER(c_unit) = 'kg' THEN c_quantity_remaining / 1000.0
                        WHEN LOWER(c_unit) = 'quintal' THEN c_quantity_remaining / 10.0
                        ELSE c_quantity_remaining 
                    END
                ) 
                FROM t_warehouse_lots 
                WHERE c_warehouse_id = w.c_id AND c_status = 'available'
            ), 0) AS UsedCapacity
        FROM t_warehouses w 
        WHERE w.c_id = @wid";

            using var cmd1 = new NpgsqlCommand(kpiSql, _conn);
            cmd1.Parameters.AddWithValue("@wid", warehouseId);
            using var r1 = await cmd1.ExecuteReaderAsync();

            decimal totalCap = 0;
            decimal usedCap = 0;
            if (await r1.ReadAsync())
            {
                // FIXED: Safe conversion here too!
                totalCap = r1["TotalCapacity"] == DBNull.Value ? 0 : Convert.ToDecimal(r1["TotalCapacity"]);
                usedCap = r1["UsedCapacity"] == DBNull.Value ? 0 : Convert.ToDecimal(r1["UsedCapacity"]);
            }
            await r1.CloseAsync();

            // 2. Get the Stock Catalog List
            string listSql = @"
        SELECT 
            wl.c_id, cp.c_name, wl.c_variety, wl.c_grade, wl.c_quantity_remaining, wl.c_unit, wl.c_created_at
        FROM t_warehouse_lots wl
        JOIN t_catalog_products cp ON wl.c_catalog_product_id = cp.c_id
        WHERE wl.c_warehouse_id = @wid AND wl.c_status = 'available'
        ORDER BY wl.c_created_at DESC";

            var stockList = new List<object>();
            using var cmd2 = new NpgsqlCommand(listSql, _conn);
            cmd2.Parameters.AddWithValue("@wid", warehouseId);
            using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync())
            {
                stockList.Add(new
                {
                    LotId = Convert.ToInt32(r2[0]),
                    CropName = r2[1].ToString(),
                    Variety = r2[2] == DBNull.Value ? "" : r2[2].ToString(),
                    Grade = r2[3].ToString(),
                    Quantity = Convert.ToDecimal(r2[4]),
                    Unit = r2[5].ToString(),
                    DateAdded = Convert.ToDateTime(r2[6])
                });
            }

            // Calculate Percentages safely
            decimal utilPct = totalCap > 0 ? Math.Round((usedCap / totalCap) * 100, 1) : 0;
            decimal availableCap = totalCap - usedCap;

            return new
            {
                TotalCapacity = totalCap,
                Used = usedCap,
                Available = availableCap,
                Utilization = utilPct,
                Stock = stockList
            };
        }

        //Elastic Search - Method (Mansi)

        public async Task<SearchResponseModel<UserSearchResult>> SearchUsersAsync(SearchRequestModel request)
        {
            return await _elasticService.SearchUsersForMVCAsync(request);
        }

        public async Task<ReindexResult> ReindexAllAsync()
        {
            return await _elasticService.ReindexAllAsync();
        }
    }
}
