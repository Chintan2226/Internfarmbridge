using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using API.Models.Vendor;
using API.Models.Auth;
using API.Models.Admin;
using API.Models.Notification;
using API.Models.Payment;
using API.Models.FieldOfficer;
using API.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;
using API.Models.Settings;

namespace API.BAL
{
    public class VendorHelper
    {
        private readonly NpgsqlConnection _conn;
        private readonly IConfiguration _configuration;
        private readonly ElasticService _elasticService;

        public VendorHelper(
            NpgsqlConnection conn,
            IConfiguration configuration,
            ElasticService elasticService)
        {
            _conn = conn;
            _configuration = configuration;
            _elasticService = elasticService;
        }
        //Shreya - Vendor Register login Jwt,token verify

        public async Task<(VendorProfile profile, bool isNewUser)> RegisterVendorGoogleAsync(string email, string displayName, string providerId)
        {
            if (_conn.State == System.Data.ConnectionState.Closed) await _conn.OpenAsync();

            var checkUserCmd = new NpgsqlCommand("SELECT c_id FROM t_users WHERE LOWER(TRIM(c_email)) = LOWER(TRIM(@email))", _conn);
            checkUserCmd.Parameters.AddWithValue("email", email);
            var existingUserId = await checkUserCmd.ExecuteScalarAsync();

            using var tran = await _conn.BeginTransactionAsync();
            try
            {
                int userId;
                bool isNewUser = false;
                if (existingUserId == null)
                {
                    isNewUser = true;
                    var userCmd = new NpgsqlCommand(@"
                INSERT INTO t_users (c_email, c_role, c_is_active, c_is_approved, c_password_hash)
                VALUES (@email, 'vendor', true, true, '') RETURNING c_id", _conn, tran);
                    userCmd.Parameters.AddWithValue("email", email);
                    userId = (int)(await userCmd.ExecuteScalarAsync())!;

                    var vendorCmd = new NpgsqlCommand(@"
                INSERT INTO t_vendor_profiles (c_user_id, c_business_name, c_contact_person)
                VALUES (@uid, @bname, @contact) RETURNING c_id", _conn, tran);
                    vendorCmd.Parameters.AddWithValue("uid", userId);
                    vendorCmd.Parameters.AddWithValue("bname", displayName);
                    vendorCmd.Parameters.AddWithValue("contact", displayName);
                    await vendorCmd.ExecuteScalarAsync();
                }
                else
                {
                    userId = (int)existingUserId;
                }

                var oauthCmd = new NpgsqlCommand(@"
            INSERT INTO t_user_oauth_accounts (c_user_id, c_provider, c_provider_user_id, c_provider_email, c_display_name)
            VALUES (@uid, 'google', @pid, @email, @name)
            ON CONFLICT (c_provider, c_provider_user_id) DO NOTHING", _conn, tran);
                oauthCmd.Parameters.AddWithValue("uid", userId);
                oauthCmd.Parameters.AddWithValue("pid", providerId);
                oauthCmd.Parameters.AddWithValue("email", email);
                oauthCmd.Parameters.AddWithValue("name", displayName);

                await oauthCmd.ExecuteNonQueryAsync();
                await tran.CommitAsync();

                return (new VendorProfile { UserId = userId, BusinessName = displayName }, isNewUser);
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
            finally { await _conn.CloseAsync(); }
        }

        public async Task<VendorProfile> RegisterVendorAsync(
        string email,
        string passwordHash,
        string businessName,
        string contactPerson,
        string phone,
        string gstin)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                // This code is added by codex - Transaction for atomicity
                using var transaction = _conn.BeginTransaction();
                try
                {
                    //Insert into t_users table
                    var userQuery = @"
                        INSERT INTO t_users (c_email, c_password_hash, c_role, c_language_preference, 
                                           c_is_first_login, c_is_active, c_is_approved)
                        VALUES (@email, @passwordHash, 'vendor', 'en', true, true, false)
                        RETURNING c_id;";

                    int userId;
                    using (var userCmd = new NpgsqlCommand(userQuery, _conn, transaction))
                    {
                        userCmd.Parameters.AddWithValue("@email", email);
                        userCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                        userId = (int)await userCmd.ExecuteScalarAsync();
                    }

                    // Insert into t_vendor_profiles table
                    var vendorQuery = @"
                        INSERT INTO t_vendor_profiles (c_user_id, c_business_name, c_contact_person, 
                                                     c_phone, c_gstin, c_onboarding_status, c_created_at)
                        VALUES (@userId, @businessName, @contactPerson, @phone, @gstin, 'pending_approval', NOW())
                        RETURNING c_id;";

                    int vendorId;
                    using (var vendorCmd = new NpgsqlCommand(vendorQuery, _conn, transaction))
                    {
                        vendorCmd.Parameters.AddWithValue("@userId", userId);
                        vendorCmd.Parameters.AddWithValue("@businessName", businessName);
                        vendorCmd.Parameters.AddWithValue("@contactPerson", contactPerson);
                        vendorCmd.Parameters.AddWithValue("@phone", phone);
                        vendorCmd.Parameters.AddWithValue("@gstin", gstin ?? "");

                        vendorId = (int)await vendorCmd.ExecuteScalarAsync();
                    }

                    //Commit transaction
                    await transaction.CommitAsync();

                    //Return vendor profile
                    return new VendorProfile
                    {
                        Id = vendorId,
                        UserId = userId,
                        BusinessName = businessName,
                        ContactPerson = contactPerson,
                        Phone = phone,
                        Gstin = gstin,
                        OnboardingStatus = "pending_approval",
                        CreatedAt = DateTime.UtcNow
                    };
                }
                catch
                {
                    //Rollback on error
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error registering vendor: {ex.Message}", ex);
            }
        }

        // get vendor by email
        /// Used for login validation - joins t_users with t_vendor_profiles
        public async Task<(User user, VendorProfile vendor)> GetVendorByEmailAsync(string email)
        {
            try
            {
                // This code is added by codex to ensure connection is open
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                //Raw SQL JOIN query
                var query = @"
                    SELECT u.c_id as UserId, u.c_email as Email, u.c_password_hash as PasswordHash, 
                           u.c_role as Role, u.c_is_active as IsActive, u.c_is_approved as IsApproved,
                           vp.c_id as VendorId, vp.c_business_name as BusinessName, 
                           vp.c_contact_person as ContactPerson, vp.c_phone as Phone, 
                           vp.c_gstin as Gstin, vp.c_onboarding_status as OnboardingStatus,
                           vp.c_approved_at as ApprovedAt
                    FROM t_users u
                    LEFT JOIN t_vendor_profiles vp ON u.c_id = vp.c_user_id
                    WHERE LOWER(TRIM(u.c_email)) = LOWER(TRIM(@email)) AND LOWER(TRIM(u.c_role)) = 'vendor';";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Map User object
                            var user = new User
                            {
                                Id = reader.GetInt32(0),
                                Email = reader.GetString(1),
                                PasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Role = reader.GetString(3),
                                IsActive = reader.GetBoolean(4),
                                IsApproved = reader.GetBoolean(5)
                            };

                            //Map VendorProfile object
                            var vendorId = reader.GetValue(6);
                            VendorProfile vendor = null;

                            if (vendorId != DBNull.Value)
                            {
                                vendor = new VendorProfile
                                {
                                    Id = (int)vendorId,
                                    UserId = user.Id,
                                    BusinessName = reader.GetString(7),
                                    ContactPerson = reader.GetString(8),
                                    Phone = reader.GetString(9),
                                    Gstin = reader.GetString(10),
                                    OnboardingStatus = reader.GetString(11),
                                    ApprovedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                                };
                            }

                            return (user, vendor);
                        }
                    }
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving vendor by email: {ex.Message}", ex);
            }
        }

        //get vendor by phone
        /// Used for login validation with phone number - joins t_users with t_vendor_profiles
        public async Task<(User user, VendorProfile vendor)> GetVendorByPhoneAsync(string phone)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = @"
                    SELECT u.c_id as UserId, u.c_email as Email, u.c_password_hash as PasswordHash, 
                           u.c_role as Role, u.c_is_active as IsActive, u.c_is_approved as IsApproved,
                           vp.c_id as VendorId, vp.c_business_name as BusinessName, 
                           vp.c_contact_person as ContactPerson, vp.c_phone as Phone, 
                           vp.c_gstin as Gstin, vp.c_onboarding_status as OnboardingStatus,
                           vp.c_approved_at as ApprovedAt
                    FROM t_users u
                    LEFT JOIN t_vendor_profiles vp ON u.c_id = vp.c_user_id
                    WHERE vp.c_phone = @phone AND LOWER(TRIM(u.c_role)) = 'vendor';";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@phone", phone);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            //Map User object
                            var user = new User
                            {
                                Id = reader.GetInt32(0),
                                Email = reader.GetString(1),
                                PasswordHash = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Role = reader.GetString(3),
                                IsActive = reader.GetBoolean(4),
                                IsApproved = reader.GetBoolean(5)
                            };

                            //Map VendorProfile object
                            var vendorId = reader.GetValue(6);
                            VendorProfile vendor = null;

                            if (vendorId != DBNull.Value)
                            {
                                vendor = new VendorProfile
                                {
                                    Id = (int)vendorId,
                                    UserId = user.Id,
                                    BusinessName = reader.GetString(7),
                                    ContactPerson = reader.GetString(8),
                                    Phone = reader.GetString(9),
                                    Gstin = reader.GetString(10),
                                    OnboardingStatus = reader.GetString(11),
                                    ApprovedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                                };
                            }

                            return (user, vendor);
                        }
                    }
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving vendor by phone: {ex.Message}", ex);
            }
        }

        //check if emailaready exists
        public async Task<bool> EmailExistsAsync(string email)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = "SELECT COUNT(1) FROM t_users WHERE LOWER(TRIM(c_email)) = LOWER(TRIM(@email));";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    var result = (long)await cmd.ExecuteScalarAsync();
                    return result > 0;
                }
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
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                var query = "SELECT COUNT(1) FROM t_vendor_profiles WHERE c_phone = @phone;";

                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@phone", phone);
                    var result = (long)await cmd.ExecuteScalarAsync();
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking phone existence: {ex.Message}", ex);
            }
        }

        // This method is added by codex to hash password using BCrypt
        // Secure password hashing before database storage
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

        // password against hash
        // Used during login validation
        public bool VerifyPassword(string password, string hash)
        {
            try
            {
                // Handle plain text passwords (for existing test data)
                if (!hash.StartsWith("$2"))
                    return password == hash;

                // Verify BCrypt hash
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error verifying password: {ex.Message}", ex);
            }

        }

        //Mansi - Vendor Dashboard
        // ─── PLACE ORDER ──────────────────────────────────────────────────────    
        public async Task<VM_PlaceOrderResponse> PlaceOrderAsync(int vendorUserId, VM_PlaceOrderRequest request)
        {
            var response = new VM_PlaceOrderResponse();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Use 'await using' for automatic disposal
            await using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                // 1. Get vendor profile id
                await using var getVendorCmd = new NpgsqlCommand(@"
            SELECT c_id, c_business_name FROM t_vendor_profiles WHERE c_user_id = @userId
        ", _conn, transaction);
                getVendorCmd.Parameters.AddWithValue("@userId", vendorUserId);

                int vendorId = 0;
                string businessName = "Vendor";

                await using (var reader = await getVendorCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        vendorId = reader.GetInt32(0);
                        businessName = reader.IsDBNull(1) ? "Vendor" : reader.GetString(1);
                    }
                }

                if (vendorId == 0)
                {
                    response.Success = false;
                    response.Message = "Vendor profile not found.";
                    return response;
                }

                // 2. Insert order
                await using var orderCmd = new NpgsqlCommand(@"
            INSERT INTO t_vendor_orders 
                (c_vendor_id, c_delivery_location_id, c_delivery_address, c_status, c_total_amount, 
                 c_ordered_at, c_updated_at)
            VALUES 
                (@vendorId, @addressId, @deliveryAddress, 'placed', @totalAmount, NOW(), NOW())
            RETURNING c_id
        ", _conn, transaction);

                orderCmd.Parameters.AddWithValue("@vendorId", vendorId);
                orderCmd.Parameters.AddWithValue("@addressId", request.AddressId);
                orderCmd.Parameters.AddWithValue("@deliveryAddress", "Standard Delivery");
                orderCmd.Parameters.AddWithValue("@totalAmount", request.TotalAmount);

                int orderId = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());

                // 3. Map Payment Method
                string paymentMethodValue = request.PaymentMethod?.ToLower() switch
                {
                    "upi" => "upi",
                    "online_banking" or "card" or "netbanking" => "online_banking",
                    _ => "upi"
                };

                await using var paymentCmd = new NpgsqlCommand(@"
            INSERT INTO t_payments_vendor (c_order_id, c_amount, c_status, c_payment_method, c_created_at)
            VALUES (@orderId, @amount, 'initiated', @paymentMethod, NOW())
        ", _conn, transaction);
                paymentCmd.Parameters.AddWithValue("@orderId", orderId);
                paymentCmd.Parameters.AddWithValue("@amount", request.TotalAmount);
                paymentCmd.Parameters.AddWithValue("@paymentMethod", paymentMethodValue);
                await paymentCmd.ExecuteNonQueryAsync();

                // 4. Insert order items & Deduct Stock
                foreach (var item in request.Items)
                {
                    decimal remainingToDeduct = item.Quantity;
                    int primaryLotId = 0;

                    // Fetch available lots for this product and grade (FIFO)
                    const string getLotsSql = @"
                        SELECT c_id, c_quantity_remaining 
                        FROM t_warehouse_lots 
                        WHERE c_catalog_product_id = @pid 
                          AND (c_grade = @grade OR (c_grade IS NULL AND @grade = ''))
                          AND LOWER(c_status) = 'available'
                          AND c_quantity_remaining > 0
                        ORDER BY c_id ASC FOR UPDATE";

                    await using var lotsCmd = new NpgsqlCommand(getLotsSql, _conn, transaction);
                    lotsCmd.Parameters.AddWithValue("@pid", item.ProductId);
                    lotsCmd.Parameters.AddWithValue("@grade", item.Grade ?? "");

                    var lotsToUpdate = new List<(int id, decimal currentQty)>();
                    await using (var lotRdr = await lotsCmd.ExecuteReaderAsync())
                    {
                        while (await lotRdr.ReadAsync())
                        {
                            lotsToUpdate.Add((lotRdr.GetInt32(0), lotRdr.GetDecimal(1)));
                        }
                    }

                    foreach (var lot in lotsToUpdate)
                    {
                        if (remainingToDeduct <= 0) break;
                        if (primaryLotId == 0) primaryLotId = lot.id;

                        decimal deductNow = Math.Min(lot.currentQty, remainingToDeduct);
                        decimal newQty = lot.currentQty - deductNow;
                        remainingToDeduct -= deductNow;

                        const string updateLotSql = @"
                            UPDATE t_warehouse_lots 
                            SET c_quantity_remaining = @newQty,
                                c_status = CASE WHEN @newQty <= 0 THEN 'out_of_stock' ELSE c_status END,
                                c_updated_at = NOW()
                            WHERE c_id = @lotId";

                        await using var updLotCmd = new NpgsqlCommand(updateLotSql, _conn, transaction);
                        updLotCmd.Parameters.AddWithValue("@newQty", newQty);
                        updLotCmd.Parameters.AddWithValue("@lotId", lot.id);
                        await updLotCmd.ExecuteNonQueryAsync();
                    }

                    if (remainingToDeduct > 0)
                    {
                        throw new Exception($"Insufficient stock for {item.ProductName} ({item.Grade}). Required: {item.Quantity}kg, but only {item.Quantity - remainingToDeduct}kg available.");
                    }

                    // Insert into order_items (using primary lot ID)
                    await using var itemCmd = new NpgsqlCommand(@"
                        INSERT INTO t_order_items 
                            (c_order_id, c_catalog_product_id, c_grade, c_quantity, c_unit_price, c_subtotal, c_lot_id)
                        VALUES 
                            (@orderId, @productId, @grade, @quantity, @price, @price * @quantity, @lotId)
                    ", _conn, transaction);
                    itemCmd.Parameters.AddWithValue("@orderId", orderId);
                    itemCmd.Parameters.AddWithValue("@productId", item.ProductId);
                    itemCmd.Parameters.AddWithValue("@grade", item.Grade ?? "");
                    itemCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                    itemCmd.Parameters.AddWithValue("@price", item.Price);
                    itemCmd.Parameters.AddWithValue("@lotId", primaryLotId);
                    await itemCmd.ExecuteNonQueryAsync();
                }

                // 5. Track Order
                await using var trackCmd = new NpgsqlCommand(@"
            INSERT INTO t_order_tracking (c_order_id, c_status_label, c_occurred_at)
            VALUES (@orderId, 'OrderPlaced', NOW())
        ", _conn, transaction);
                trackCmd.Parameters.AddWithValue("@orderId", orderId);
                await trackCmd.ExecuteNonQueryAsync();

                // 6. Clear cart
                await using var clearCartCmd = new NpgsqlCommand(@"
            DELETE FROM t_vendor_cart_items 
            WHERE c_vendor_id = @vendorId
        ", _conn, transaction);
                clearCartCmd.Parameters.AddWithValue("@vendorId", vendorId);
                await clearCartCmd.ExecuteNonQueryAsync();

                // --- SINGLE COMMIT POINT ---
                await transaction.CommitAsync();

                // ✅ INDEX ORDER IN ELASTICSEARCH
                try
                {
                    var orderDoc = new OrderDocument
                    {
                        Id = orderId,
                        VendorId = vendorId,
                        VendorBusinessName = businessName,
                        Status = "placed",
                        TotalAmount = request.TotalAmount,
                        OrderedAt = DateTime.UtcNow,
                        Items = request.Items.Select(item => new OrderItemDocument
                        {
                            CatalogProductId = item.ProductId,
                            ProductName = item.ProductName ?? "",
                            Quantity = item.Quantity,
                            UnitPrice = item.Price,
                            Subtotal = item.Quantity * item.Price
                        }).ToList()
                    };
                    await _elasticService.IndexOrderAsync(orderDoc);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to index order: {ex.Message}");
                }

                response.Success = true;
                response.OrderId = "FB-ORD-" + orderId.ToString("D8");
                response.Amount = request.TotalAmount;
                response.Message = "Order placed successfully!";
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { }
                response.Success = false;
                response.Message = "Database Error: " + ex.Message;
            }

            return response;
        }

        // ─── CANCEL ORDER ──────────────────────────────────────────────────────
        public async Task<string> CancelOrderAsync(int vendorUserId, string orderId, string reason)
        {
            if (!int.TryParse(orderId.Replace("FB-ORD-", ""), out int orderNumId))
                return "Invalid Order ID format.";

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            await using var transaction = await _conn.BeginTransactionAsync();
            try
            {
                // ✅ GET VENDOR ID FIRST
                int vendorId = 0;
                await using var getVendorIdCmd = new NpgsqlCommand(
                    "SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid", _conn, transaction);
                getVendorIdCmd.Parameters.AddWithValue("@uid", vendorUserId);
                var vendorIdResult = await getVendorIdCmd.ExecuteScalarAsync();
                if (vendorIdResult != null) vendorId = Convert.ToInt32(vendorIdResult);

                // Verify order belongs to vendor and is cancellable
                await using var checkCmd = new NpgsqlCommand(@"
            SELECT c_status FROM t_vendor_orders 
            WHERE c_id = @oid 
            AND c_vendor_id = @vendorId
        ", _conn, transaction);
                checkCmd.Parameters.AddWithValue("@oid", orderNumId);
                checkCmd.Parameters.AddWithValue("@vendorId", vendorId);

                var statusRaw = await checkCmd.ExecuteScalarAsync();
                if (statusRaw == null) return "Order not found.";

                string currentStatus = statusRaw.ToString()!;
                if (currentStatus == "cancelled") return "Order is already cancelled.";
                if (currentStatus == "delivered") return "Cannot cancel a delivered order.";
                if (currentStatus == "dispatched" || currentStatus == "in_transit")
                    return "Cannot cancel order that is already dispatched.";

                // Update order status to cancelled
                await using var updateCmd = new NpgsqlCommand(@"
            UPDATE t_vendor_orders 
            SET c_status = 'cancelled', c_cancel_reason = @reason, c_updated_at = NOW()
            WHERE c_id = @oid
        ", _conn, transaction);
                updateCmd.Parameters.AddWithValue("@oid", orderNumId);
                updateCmd.Parameters.AddWithValue("@reason", reason ?? "Cancelled by vendor");
                await updateCmd.ExecuteNonQueryAsync();

                // Add tracking entry
                await using var trackCmd = new NpgsqlCommand(@"
            INSERT INTO t_order_tracking (c_order_id, c_status_label, c_occurred_at)
            VALUES (@oid, 'Cancelled', NOW())
        ", _conn, transaction);
                trackCmd.Parameters.AddWithValue("@oid", orderNumId);
                await trackCmd.ExecuteNonQueryAsync();

                // Update payment status to refund_initiated if payment was made
                await using var payCmd = new NpgsqlCommand(@"
            UPDATE t_payments_vendor 
            SET c_status = 'refund_initiated'
            WHERE c_order_id = @oid AND c_status = 'success'
        ", _conn, transaction);
                payCmd.Parameters.AddWithValue("@oid", orderNumId);
                await payCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                // ✅ UPDATE ORDER INDEX IN ELASTICSEARCH
                try
                {
                    var orderDoc = new OrderDocument
                    {
                        Id = orderNumId,
                        VendorId = vendorId,
                        Status = "cancelled",
                        OrderedAt = DateTime.UtcNow
                    };
                    await _elasticService.IndexOrderAsync(orderDoc);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to update order index: {ex.Message}");
                }

                return "Success";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return "Error: " + ex.Message;
            }
        }


        // ─── DASHBOARD STATS ───────────────────────────────────────────────────
        public async Task<VM_DashboardStats> GetDashboardStatsAsync(int vendorUserId)
        {
            var stats = new VM_DashboardStats();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT
            COUNT(DISTINCT vo.c_id) AS active_orders,
            COUNT(CASE WHEN DATE_TRUNC('month', vo.c_ordered_at) = DATE_TRUNC('month', NOW()) THEN 1 END) AS month_orders,
            (SELECT COUNT(*) FROM t_catalog_products WHERE c_is_active = true) AS catalog_available,
            COALESCE(SUM(oi.c_quantity), 0) AS total_quantity
        FROM t_vendor_orders vo
        LEFT JOIN t_order_items oi ON oi.c_order_id = vo.c_id   -- ← YEH JOIN IMPORTANT HAI
        WHERE vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
          AND vo.c_status IN ('placed','admin_confirmed','dispatched','in_transit')";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                stats.ActiveOrders = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                stats.TotalOrdersThisMonth = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                stats.CatalogCropsAvailable = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);
                stats.TotalQuantity = rdr.IsDBNull(3) ? 0 : rdr.GetDecimal(3);
            }
            return stats;
        }

        // ─── FILTERED CATALOG ──────────────────────────────────────────────────

        public async Task<List<VM_CatalogCropItem>> GetFilteredCatalogAsync(VM_CropFilter filter)
        {
            var list = new List<VM_CatalogCropItem>();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            var sql = @"
        SELECT 
            wl.c_catalog_product_id AS id,
            cp.c_name AS name,
            cp.c_category AS category,
            cp.c_unit_of_measure AS unit,

            -- ✅ Correct price (average of lots)
            AVG(qif.c_fo_assessed_price * 1.10) AS unit_price,

            -- ✅ Correct quantity (no duplication issue)
            SUM(wl.c_quantity_remaining) AS quantity_available,

            -- ✅ Inspection image (thumbnail) with catalog fallback
            COALESCE(MIN(ip.c_photo_url), cp.c_image_url) AS image_url,

            wl.c_grade AS grade

        FROM t_warehouse_lots wl

        INNER JOIN t_catalog_products cp 
            ON wl.c_catalog_product_id = cp.c_id

        -- ✅ Only QC passed
        INNER JOIN t_quality_inspection_forms qif 
            ON wl.c_quality_inspection_id = qif.c_id

        -- ✅ Join images safely
        LEFT JOIN (
            SELECT 
                c_inspection_id,
                MIN(c_photo_url) AS c_photo_url
            FROM t_inspection_photos
            GROUP BY c_inspection_id
        ) ip 
            ON ip.c_inspection_id = qif.c_id

        WHERE 
            LOWER(wl.c_status) = 'available'
            AND qif.c_passed = true
    ";

            // 🔍 Filters
            if (!string.IsNullOrWhiteSpace(filter.CropType))
                sql += " AND cp.c_name ILIKE @type";

            if (!string.IsNullOrWhiteSpace(filter.Grade) && filter.Grade.ToLower() != "all")
                sql += " AND wl.c_grade = @grade";

            sql += @"
        GROUP BY 
            wl.c_catalog_product_id,
            cp.c_name,
            cp.c_category,
            cp.c_unit_of_measure,
            cp.c_image_url,
            wl.c_grade

        ORDER BY cp.c_name;
    ";

            await using var cmd = new NpgsqlCommand(sql, _conn);

            // Parameters
            if (!string.IsNullOrWhiteSpace(filter.CropType))
                cmd.Parameters.AddWithValue("@type", $"%{filter.CropType}%");

            if (!string.IsNullOrWhiteSpace(filter.Grade) && filter.Grade.ToLower() != "all")
                cmd.Parameters.AddWithValue("@grade", filter.Grade);

            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(new VM_CatalogCropItem
                {
                    Id = rdr.GetInt32(0),
                    Name = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                    Category = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    Unit = rdr.IsDBNull(3) ? "kg" : rdr.GetString(3),

                    UnitPrice = rdr.IsDBNull(4) ? 0 : Convert.ToDecimal(rdr[4]),
                    QuantityAvailable = rdr.IsDBNull(5) ? 0 : Convert.ToDecimal(rdr[5]),

                    ImageUrl = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    Grade = rdr.IsDBNull(7) ? "" : rdr.GetString(7)
                });
            }

            return list;
        }

         // ─── CART ──────────────────────────────────────────────────────────────
        public async Task<VM_CartSummary> GetCartSummaryAsync(int vendorUserId)
        {
            var summary = new VM_CartSummary();
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT vc.c_id, vc.c_catalog_product_id, cp.c_name, cp.c_unit_of_measure,
                    (SELECT COALESCE(AVG(qif_sub.c_fo_assessed_price * 1.10), 0) 
                     FROM t_warehouse_lots wl_sub
                     JOIN t_quality_inspection_forms qif_sub ON qif_sub.c_id = wl_sub.c_quality_inspection_id
                     WHERE wl_sub.c_catalog_product_id = cp.c_id 
                       AND LOWER(wl_sub.c_status) = 'available'
                       AND (wl_sub.c_grade = vc.c_grade OR (wl_sub.c_grade IS NULL AND vc.c_grade IS NULL))
                    ) AS unit_price, vc.c_quantity,
                    (SELECT COALESCE(SUM(c_quantity_remaining), 0) FROM t_warehouse_lots WHERE c_catalog_product_id = vc.c_catalog_product_id AND LOWER(c_status) = 'available' AND (c_grade = vc.c_grade OR (c_grade IS NULL AND vc.c_grade IS NULL))) AS available_stock,
                    vc.c_grade
                FROM t_vendor_cart_items vc
                JOIN t_catalog_products cp ON cp.c_id = vc.c_catalog_product_id


                WHERE vc.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var qty = rdr.GetDecimal(5);
                var price = rdr.GetDecimal(4);
                var availableStock = rdr.IsDBNull(6) ? 0 : rdr.GetDecimal(6);
                var grade = rdr.IsDBNull(7) ? "" : rdr.GetString(7);

                // Check if cart quantity exceeds available stock
                bool isStockValid = qty <= availableStock;

                var item = new VM_CartItem
                {
                    CartId = rdr.GetInt32(0),
                    CropId = rdr.GetInt32(1),
                    CropName = rdr.GetString(2),
                    Unit = rdr.IsDBNull(3) ? "kg" : rdr.GetString(3),
                    UnitPrice = price,
                    Quantity = qty,
                    TotalPrice = qty * price,
                    AvailableStock = availableStock,
                    IsStockValid = isStockValid,
                    Grade = grade
                };
                summary.Items.Add(item);
                summary.EstimatedOrderValue += item.TotalPrice;
                summary.TotalQuantity += qty;

                if (!isStockValid)
                    summary.HasStockIssues = true;
            }
            return summary;
        }

        public async Task<string> AddItemToCartAsync(int vendorUserId, int cropId, decimal quantity, string grade = null)
        {
            // Validation 1: quantity must be positive first
            if (quantity <= 0) return "Quantity must be greater than zero.";

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Check grade-specific available stock from t_warehouse_lots.
            // The catalog shows stock per grade, so we must check per grade
            // to avoid summing all grades and inflating the effective minimum.
            string stockSql = @"
        SELECT COALESCE(SUM(c_quantity_remaining), 0)
        FROM t_warehouse_lots
        WHERE c_catalog_product_id = @cropId
        AND LOWER(c_status) = 'available'";

            if (!string.IsNullOrWhiteSpace(grade))
                stockSql += " AND LOWER(c_grade) = LOWER(@grade)";

            await using var stockCmd = new NpgsqlCommand(stockSql, _conn);
            stockCmd.Parameters.AddWithValue("@cropId", cropId);
            if (!string.IsNullOrWhiteSpace(grade))
                stockCmd.Parameters.AddWithValue("@grade", grade);

            var availableStock = Convert.ToDecimal(await stockCmd.ExecuteScalarAsync() ?? 0);

            if (availableStock <= 0)
                return "Product is currently out of stock.";

            if (quantity > availableStock)
                return $"Only {availableStock}kg available. Please reduce quantity.";

            // Enforce minimum order: 1kg for low-stock lots, 20kg otherwise.
            // This matches the frontend stepper behaviour.
            decimal effectiveMinimum = availableStock < 20m ? 1m : 20m;
            if (quantity < effectiveMinimum)
                return $"Minimum order quantity is {effectiveMinimum}kg.";

            // UPSERT: REPLACE the quantity (not accumulate) so every
            // "Add to Cart" click sets exactly what the user picked in the popup.
            const string upsert = @"
        INSERT INTO t_vendor_cart_items (c_vendor_id, c_catalog_product_id, c_grade, c_quantity, c_added_at)
        VALUES (
            (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid),
            @cid,
            @grade,
            @qty,
            NOW()
        )
        ON CONFLICT (c_vendor_id, c_catalog_product_id, c_grade)
        DO UPDATE SET
            c_quantity = t_vendor_cart_items.c_quantity + EXCLUDED.c_quantity,

            c_added_at = NOW()";

            await using var cmd = new NpgsqlCommand(upsert, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            cmd.Parameters.AddWithValue("@cid", cropId);
            cmd.Parameters.AddWithValue("@qty", quantity);
            cmd.Parameters.AddWithValue("@grade", (object)grade ?? DBNull.Value);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Success" : "Failed to add item.";
        }

        public async Task<string> RemoveCartItemAsync(int vendorUserId, int cartId)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                DELETE FROM t_vendor_cart_items WHERE c_id = @cid
                  AND c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)";
            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@cid", cartId);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Success" : "Item not found.";
        }

        // ─── ORDER HISTORY ─────────────────────────────────────────────────────
        public async Task<List<VM_OrderHistoryItem>> GetOrderHistoryAsync(int vendorUserId)
        {
            var list = new List<VM_OrderHistoryItem>();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT vo.c_id, vo.c_ordered_at, vo.c_status, vo.c_total_amount,
                       vo.c_tracking_number, vo.c_estimated_delivery_date,
                       COUNT(oi.c_id) AS item_count
                FROM t_vendor_orders vo
                LEFT JOIN t_order_items oi ON oi.c_order_id = vo.c_id
                WHERE vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                GROUP BY vo.c_id, vo.c_ordered_at, vo.c_status, vo.c_total_amount,
                         vo.c_tracking_number, vo.c_estimated_delivery_date
                ORDER BY vo.c_ordered_at DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var rawStatus = rdr.IsDBNull(2) ? "placed" : rdr.GetString(2);
                list.Add(new VM_OrderHistoryItem
                {
                    OrderId = "FB-ORD-" + rdr.GetInt32(0).ToString("D8"),
                    OrderNumber = "FB-ORD-" + rdr.GetInt32(0).ToString("D8"),
                    PlacedAt = rdr.GetDateTime(1),
                    Status = rawStatus,
                    StatusText = MapStatusText(rawStatus),
                    StatusColor = MapStatusColor(rawStatus),
                    GrandTotal = rdr.GetDecimal(3),
                    TrackingId = rdr.IsDBNull(4) ? "Pending" : rdr.GetString(4),
                    EstimatedDelivery = rdr.IsDBNull(5) ? (DateTime?)null : rdr.GetDateTime(5),
                    ItemCount = rdr.IsDBNull(6) ? 0 : rdr.GetInt32(6)
                });
            }
            return list;
        }

        // ─── REPEAT ORDER ──────────────────────────────────────────────────────
        public async Task<string> RepeatOrderAsync(int vendorUserId, string orderId)
        {
            if (!int.TryParse(orderId.Replace("FB-ORD-", ""), out int orderNumId))
                return "Invalid Order ID format.";

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                INSERT INTO t_vendor_cart_items (c_vendor_id, c_catalog_product_id, c_grade, c_quantity, c_added_at)
                SELECT vo.c_vendor_id, oi.c_catalog_product_id, oi.c_grade, oi.c_quantity, NOW()
                FROM t_order_items oi
                JOIN t_vendor_orders vo ON oi.c_order_id = vo.c_id
                WHERE vo.c_id = @oid
                  AND vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                ON CONFLICT (c_vendor_id, c_catalog_product_id, c_grade)
                DO UPDATE SET c_quantity = t_vendor_cart_items.c_quantity + EXCLUDED.c_quantity";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@oid", orderNumId);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            int items = await cmd.ExecuteNonQueryAsync();
            return items > 0 ? $"Success: {items} items added to cart." : "Failed: Order not found or empty.";
        }

        // ─── PROFILE ───────────────────────────────────────────────────────────
        public async Task<VM_VendorProfile?> GetProfileAsync(int vendorUserId)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT vp.c_id, vp.c_business_name, vp.c_contact_person, vp.c_phone,
                    vp.c_gstin, vp.c_onboarding_status, vp.c_created_at,
                    u.c_email, u.c_profile_image_url
                FROM t_vendor_profiles vp
                JOIN t_users u ON u.c_id = vp.c_user_id
                WHERE vp.c_user_id = @uid";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();

            if (await rdr.ReadAsync())
            {
                return new VM_VendorProfile
                {
                    VendorId = rdr.GetInt32(0),
                    BusinessName = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                    ContactPerson = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    Phone = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    Gstin = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                    OnboardingStatus = rdr.IsDBNull(5) ? "pending_approval" : rdr.GetString(5),
                    MemberSince = rdr.IsDBNull(6) ? DateTime.Now : rdr.GetDateTime(6),
                    Email = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                    ProfileImageUrl = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                    IsEmailEditable = false  // Email requires admin approval
                };
            }
            return null;
        }

        public async Task<string> UpdateProfileAsync(int vendorUserId, VM_UpdateProfileRequest request)
        {
            if (request.VendorId <= 0) return "Invalid Vendor ID.";

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Only update editable fields: business_name, contact_person, phone
            const string sql = @"
                UPDATE t_vendor_profiles
                SET c_business_name = @businessName, 
                    c_contact_person = @contactPerson, 
                    c_phone = @phone,
                    c_gstin = @gstin
                WHERE c_id = @vid AND c_user_id = @userId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@businessName", request.BusinessName ?? "");
            cmd.Parameters.AddWithValue("@contactPerson", request.ContactPerson ?? "");
            cmd.Parameters.AddWithValue("@phone", request.Phone ?? "");
            cmd.Parameters.AddWithValue("@gstin", request.Gstin ?? "");
            cmd.Parameters.AddWithValue("@vid", request.VendorId);
            cmd.Parameters.AddWithValue("@userId", vendorUserId);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Profile updated successfully" : "Profile update failed";
        }

        public async Task<string> UploadProfilePhotoAsync(int vendorUserId, string imageBase64)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                UPDATE t_users 
                SET c_profile_image_url = @imageUrl
                WHERE c_id = @userId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@imageUrl", imageBase64);
            cmd.Parameters.AddWithValue("@userId", vendorUserId);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Photo uploaded successfully" : "Failed to upload photo";
        }

        // ─── CHANGE PASSWORD ──────────────────────────────────────────────────
        public async Task<string> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Get current password hash
            const string getSql = "SELECT c_password_hash FROM t_users WHERE c_id = @userId";
            await using var getCmd = new NpgsqlCommand(getSql, _conn);
            getCmd.Parameters.AddWithValue("@userId", userId);
            var currentHash = await getCmd.ExecuteScalarAsync() as string;

            if (string.IsNullOrEmpty(currentHash))
                return "User not found";

            // Verify current password (using BCrypt)
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, currentHash))
                return "Current password is incorrect";

            // Validate new password strength
            if (!IsValidPassword(newPassword))
                return "Password must be at least 8 characters with letters, numbers and a special character";

            // Hash new password
            string newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Update password
            const string updateSql = "UPDATE t_users SET c_password_hash = @newHash WHERE c_id = @userId";
            await using var updateCmd = new NpgsqlCommand(updateSql, _conn);
            updateCmd.Parameters.AddWithValue("@newHash", newHash);
            updateCmd.Parameters.AddWithValue("@userId", userId);

            int rows = await updateCmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Password changed successfully" : "Failed to change password";
        }

        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            bool hasLetter = System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Za-z]");
            bool hasNumber = System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]");
            bool hasSpecial = System.Text.RegularExpressions.Regex.IsMatch(password, "[@$!%*#?&]");

            return hasLetter && hasNumber && hasSpecial;
        }

        // ─── WISHLIST ──────────────────────────────────────────────────────────────

        // Add item to wishlist
        // BAL/VendorHelper.cs

        public async Task<string> AddToWishlistAsync(int vendorUserId, int productId, string grade)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Updated SQL to include c_grade in both INSERT and ON CONFLICT
            const string sql = @"
        INSERT INTO t_wishlist (c_vendor_id, c_catalog_product_id, c_grade, c_saved_at)
        VALUES (
            (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid),
            @productId,
            @grade,
            NOW()
        )
        ON CONFLICT (c_vendor_id, c_catalog_product_id, c_grade) DO NOTHING";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            cmd.Parameters.AddWithValue("@productId", productId);
            cmd.Parameters.AddWithValue("@grade", (object)grade ?? DBNull.Value);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Added to wishlist" : "Already in wishlist";
        }

        public async Task<List<VM_WishlistItem>> GetWishlistAsync(int vendorUserId)
        {
            var list = new List<VM_WishlistItem>();
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
        SELECT 
            cp.c_id AS product_id,
            cp.c_name AS product_name,
            cp.c_category AS category,
            cp.c_unit_of_measure AS unit,
            COALESCE(AVG(qif.c_fo_assessed_price * 1.10), 0) AS avg_price,
            COALESCE(SUM(wl.c_quantity_remaining), 0) AS quantity_available,
            COALESCE(MIN(ip.c_photo_url), cp.c_image_url) AS image_url,
            w.c_grade AS grade
        FROM t_wishlist w
        INNER JOIN t_catalog_products cp ON cp.c_id = w.c_catalog_product_id
        LEFT JOIN t_warehouse_lots wl ON wl.c_catalog_product_id = cp.c_id 
             AND (wl.c_grade = w.c_grade OR (wl.c_grade IS NULL AND w.c_grade IS NULL))
        LEFT JOIN t_quality_inspection_forms qif ON qif.c_id = wl.c_quality_inspection_id
        LEFT JOIN t_inspection_photos ip ON ip.c_inspection_id = qif.c_id
        WHERE w.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
        GROUP BY cp.c_id, cp.c_name, cp.c_category, cp.c_unit_of_measure, cp.c_image_url, w.c_grade
        ORDER BY cp.c_name, w.c_grade;";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);

            try
            {
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    list.Add(new VM_WishlistItem
                    {
                        ProductId = rdr.GetInt32(0),
                        ProductName = rdr.GetString(1),
                        Category = rdr.IsDBNull(2) ? "Other" : rdr.GetString(2),
                        Unit = rdr.IsDBNull(3) ? "kg" : rdr.GetString(3),
                        AvgPrice = rdr.GetDecimal(4),
                        QuantityAvailable = rdr.GetDecimal(5),
                        ImageUrl = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                        Grade = rdr.IsDBNull(7) ? null : rdr.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                // Log this: Console.WriteLine(ex.Message);
                throw new Exception("Database Error in GetWishlist: " + ex.Message);
            }
            return list;
        }
        // Remove item from wishlist - now grade-aware
        public async Task<string> RemoveFromWishlistAsync(int vendorUserId, int productId, string grade = null)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                DELETE FROM t_wishlist
                WHERE c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                  AND c_catalog_product_id = @productId
                  AND (
                      (@grade IS NULL AND c_grade IS NULL)
                      OR c_grade = @grade
                  )";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            cmd.Parameters.AddWithValue("@productId", productId);
            cmd.Parameters.AddWithValue("@grade", (object)grade ?? DBNull.Value);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? "Removed from wishlist" : "Item not found in wishlist";
        }


        // Check if product is in wishlist - now grade-aware
        public async Task<bool> IsInWishlistAsync(int vendorUserId, int productId, string grade = null)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT COUNT(*) FROM t_wishlist
                WHERE c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                  AND c_catalog_product_id = @productId
                  AND (
                      (@grade IS NULL AND c_grade IS NULL)
                      OR c_grade = @grade
                  )";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            cmd.Parameters.AddWithValue("@productId", productId);
            cmd.Parameters.AddWithValue("@grade", (object)grade ?? DBNull.Value);

            var count = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(count) > 0;
        }

        // ─── USER KPI STATS ────────────────────────────────────────────────────
        public async Task<VM_UserKpiStats> GetUserKpiStatsAsync(int vendorUserId)
        {
            var stats = new VM_UserKpiStats();
            if (_conn.State != ConnectionState.Open) await _conn.OpenAsync();

            const string sql = @"
        SELECT
            COUNT(DISTINCT vo.c_id) AS total_orders,
            COALESCE(SUM(vo.c_total_amount), 0) AS total_spent,
            COUNT(CASE WHEN vo.c_status = 'placed' THEN 1 END) AS pending_orders,
            COALESCE(SUM(oi.c_quantity), 0) AS total_quantity
        FROM t_vendor_orders vo
        LEFT JOIN t_order_items oi ON oi.c_order_id = vo.c_id
        WHERE vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();

            if (await rdr.ReadAsync())
            {
                // Using Convert.To... is safer for PostgreSQL numeric types
                stats.TotalOrders = rdr.IsDBNull(0) ? 0 : Convert.ToInt32(rdr.GetValue(0));
                stats.TotalSpent = rdr.IsDBNull(1) ? 0 : Convert.ToDecimal(rdr.GetValue(1));
                stats.PendingOrders = rdr.IsDBNull(2) ? 0 : Convert.ToInt32(rdr.GetValue(2));
                stats.TotalQuantity = rdr.IsDBNull(3) ? 0 : Convert.ToDecimal(rdr.GetValue(3));
            }
            return stats;
        }

        // ─── RECENT 5 ORDERS ───────────────────────────────────────────────────
        public async Task<List<VM_RecentOrder>> GetRecentOrdersAsync(int vendorUserId)
        {
            var list = new List<VM_RecentOrder>();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT c_id, c_ordered_at, c_status, c_total_amount
                FROM t_vendor_orders
                WHERE c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                ORDER BY c_ordered_at DESC LIMIT 5";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var rawStatus = rdr.IsDBNull(2) ? "placed" : rdr.GetString(2);
                list.Add(new VM_RecentOrder
                {
                    OrderNumber = "FB-ORD-" + rdr.GetInt32(0).ToString("D8"),
                    PlacedAt = rdr.GetDateTime(1),
                    Status = rawStatus,
                    StatusText = MapStatusText(rawStatus),
                    GrandTotal = rdr.GetDecimal(3)
                });
            }
            return list;
        }

        // ─── ORDER ITEMS (for tracking) ────────────────────────────────────────
        public async Task<List<VM_OrderItem>> GetOrderItemsAsync(string orderId)
        {
            var list = new List<VM_OrderItem>();
            if (!int.TryParse(orderId.Replace("FB-ORD-", ""), out int numId)) return list;

            try
            {
                // 1. Ensure connection is open
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                const string sql = @"
            SELECT oi.c_catalog_product_id, cp.c_name, oi.c_quantity, oi.c_unit_price, cp.c_unit_of_measure,
                   COALESCE(fp.c_full_name,'Unknown Farmer')
            FROM t_order_items oi
            JOIN t_catalog_products cp ON cp.c_id = oi.c_catalog_product_id
            LEFT JOIN t_farmer_crop_listings fcl ON fcl.c_id = oi.c_crop_listing_id
            LEFT JOIN t_farmer_profiles fp ON fp.c_id = fcl.c_farmer_id
            WHERE oi.c_order_id = @oid";

                // 2. Use 'await using' for automatic asynchronous disposal
                await using (var cmd = new NpgsqlCommand(sql, _conn))
                {
                    cmd.Parameters.AddWithValue("@oid", numId);

                    await using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            var qty = rdr.GetDecimal(2);
                            var price = rdr.GetDecimal(3);

                            list.Add(new VM_OrderItem
                            {
                                ProductId = rdr.GetInt32(0),
                                ProductName = rdr.GetString(1),
                                Quantity = qty,
                                Price = price,
                                Unit = rdr.IsDBNull(4) ? "kg" : rdr.GetString(4),
                                FarmerName = rdr.GetString(5),
                                Total = qty * price
                            });
                        }
                    } // Reader closes here
                } // Command closes here
            }
            catch (Exception ex)
            {
                // Log the exception here if you have a logger (e.g., _logger.LogError(ex...))
                Console.WriteLine($"Error fetching order items: {ex.Message}");
                throw; // Re-throw to let the caller handle the failure
            }
            finally
            {
                // 3. Optional: Only close connection if your architecture doesn't use a persistent/scoped connection
                // if (_conn.State == ConnectionState.Open) await _conn.CloseAsync();
            }

            return list;
        }

        // ─── ORDER TRACKING TIMELINE ───────────────────────────────────────────
        public async Task<VM_OrderTracking?> GetOrderTrackingAsync(string orderId, int vendorUserId)
        {
            if (!int.TryParse(orderId.Replace("FB-ORD-", ""), out int numId)) return null;

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
        SELECT vo.c_id, vo.c_ordered_at, vo.c_status, vo.c_total_amount,
               vo.c_tracking_number, vo.c_estimated_delivery_date
        FROM t_vendor_orders vo
        WHERE vo.c_id = @oid
          AND vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)";

            // Variables to hold data so we can close the connection reader
            DateTime orderDate;
            string status;
            decimal totalAmount;
            string? trackingNumber;
            DateTime? estimatedDelivery;

            // 1. Scope the reader so it closes immediately after reading
            using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@oid", numId);
                cmd.Parameters.AddWithValue("@uid", vendorUserId);

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    if (!await rdr.ReadAsync()) return null;

                    orderDate = rdr.GetDateTime(1);
                    status = rdr.IsDBNull(2) ? "placed" : rdr.GetString(2);
                    totalAmount = rdr.GetDecimal(3);
                    trackingNumber = rdr.IsDBNull(4) ? "Pending" : rdr.GetString(4);
                    estimatedDelivery = rdr.IsDBNull(5) ? (DateTime?)null : rdr.GetDateTime(5);
                } // Reader is DISPOSED/CLOSED here
            }

            // 2. Now that the reader is closed, the connection is free for the next call
            var tracking = new VM_OrderTracking
            {
                OrderId = orderId,
                OrderDate = orderDate,
                Status = status,
                TotalAmount = totalAmount,
                TrackingId = trackingNumber,
                EstimatedDelivery = estimatedDelivery ?? orderDate.AddDays(5),
                Timeline = BuildTimeline(status, orderDate),
                Items = await GetOrderItemsAsync(orderId) // This will now work!
            };

            return tracking;
        }

        // ─── PAYMENT HISTORY ───────────────────────────────────────────────────
        public async Task<List<VM_PaymentHistory>> GetPaymentHistoryAsync(int vendorUserId)
        {
            var list = new List<VM_PaymentHistory>();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT pv.c_id, pv.c_order_id, pv.c_amount, pv.c_status,
                       pv.c_payment_method, pv.c_gateway_ref, pv.c_created_at
                FROM t_payments_vendor pv
                JOIN t_vendor_orders vo ON vo.c_id = pv.c_order_id
                WHERE vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                ORDER BY pv.c_created_at DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var st = rdr.IsDBNull(3) ? "pending" : rdr.GetString(3);
                list.Add(new VM_PaymentHistory
                {
                    TransactionId = "TXN-" + rdr.GetInt32(0).ToString("D8"),
                    OrderId = "FB-ORD-" + rdr.GetInt32(1).ToString("D8"),
                    Amount = rdr.GetDecimal(2),
                    Status = st,
                    StatusColor = st == "success" ? "success" : (st == "failed" ? "danger" : "warning"),
                    PaymentMethod = rdr.IsDBNull(4) ? "N/A" : rdr.GetString(4),
                    GatewayRef = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                    PaymentDate = rdr.GetDateTime(6)
                });
            }
            return list;
        }

        // ─── ADDRESSES ─────────────────────────────────────────────────────────
        public async Task<List<VM_VendorAddress>> GetAddressesAsync(int vendorUserId)
        {
            var addresses = new List<VM_VendorAddress>();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
                SELECT c_id, c_address, c_city, c_state, c_pincode, c_is_active, c_created_at
                FROM t_vendor_delivery_locations
                WHERE c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
                ORDER BY c_created_at DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                addresses.Add(new VM_VendorAddress
                {
                    Id = rdr.GetInt32(0),
                    UserId = vendorUserId,
                    FullName = "Vendor",
                    PhoneNumber = "",
                    AddressLine1 = rdr.GetString(1),
                    AddressLine2 = "",
                    City = rdr.GetString(2),
                    State = rdr.GetString(3),
                    ZipCode = rdr.GetString(4),
                    IsDefault = rdr.GetBoolean(5)
                });
            }
            return addresses;
        }

        public async Task<VM_SaveAddressResponse> SaveAddressAsync(int vendorUserId, VM_SaveAddressRequest request)
        {
            var response = new VM_SaveAddressResponse();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            await using var transaction = await _conn.BeginTransactionAsync();
            try
            {
                // Step 1: If this new address is being set as default,
                // first clear the default flag from all existing addresses for this vendor
                if (request.IsDefault)
                {
                    const string clearDefaultSql = @"
                        UPDATE t_vendor_delivery_locations
                        SET c_is_active = false
                        WHERE c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)";

                    await using var clearCmd = new NpgsqlCommand(clearDefaultSql, _conn, transaction);
                    clearCmd.Parameters.AddWithValue("@uid", vendorUserId);
                    await clearCmd.ExecuteNonQueryAsync();
                }

                // Step 2: Insert the new address with the correct c_is_active value
                const string sql = @"
                INSERT INTO t_vendor_delivery_locations 
                    (c_vendor_id, c_address, c_city, c_state, c_pincode, c_is_active, c_created_at)
                VALUES 
                    ((SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid), @address, @city, @state, @pincode, @isDefault, NOW())
                RETURNING c_id";

                await using var cmd = new NpgsqlCommand(sql, _conn, transaction);
                cmd.Parameters.AddWithValue("@uid", vendorUserId);
                cmd.Parameters.AddWithValue("@address", $"{request.AddressLine1} {request.AddressLine2}".Trim());
                cmd.Parameters.AddWithValue("@city", request.City);
                cmd.Parameters.AddWithValue("@state", request.State);
                cmd.Parameters.AddWithValue("@pincode", request.ZipCode);
                cmd.Parameters.AddWithValue("@isDefault", request.IsDefault);

                var addressId = await cmd.ExecuteScalarAsync();
                await transaction.CommitAsync();

                response.Success = true;
                response.AddressId = Convert.ToInt32(addressId);
                response.Message = "Address saved successfully";
                return response;
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { }
                response.Success = false;
                response.Message = "Error saving address: " + ex.Message;
                return response;
            }
        }

        // ─── HELPERS ───────────────────────────────────────────────────────────
        private static string MapStatusText(string s) => s switch
        {
            "placed" => "Order Placed",
            "admin_confirmed" => "Confirmed",
            "farmer_notified" => "Farmer Notified",
            "dispatched" => "Dispatched",
            "in_transit" => "In Transit",
            "delivered" => "Delivered",
            "cancelled" => "Cancelled",
            _ => s
        };

        private static string MapStatusColor(string s) => s switch
        {
            "placed" => "#ff9800",
            "admin_confirmed" => "#2196f3",
            "farmer_notified" => "#4caf50",
            "dispatched" => "#9c27b0",
            "in_transit" => "#ff9800",
            "delivered" => "#4caf50",
            "cancelled" => "#f44336",
            _ => "#666666"
        };

        private static List<VM_TimelineStep> BuildTimeline(string status, DateTime orderDate)
        {
            var steps = new[]
            {
                ("placed", "Order Placed", "fa-check-circle"),
                ("admin_confirmed", "Confirmed", "fa-check-double"),
                ("farmer_notified", "Farmer Notified", "fa-bell"),
                ("dispatched", "Dispatched", "fa-box"),
                ("in_transit", "In Transit", "fa-truck"),
                ("delivered", "Delivered", "fa-home")
            };

            var order = new[] { "placed", "admin_confirmed", "farmer_notified", "dispatched", "in_transit", "delivered" };
            int curIndex = Array.IndexOf(order, status);
            if (curIndex == -1) curIndex = 0;

            var timeline = new List<VM_TimelineStep>();
            for (int i = 0; i < steps.Length; i++)
            {
                bool done = i <= curIndex;
                timeline.Add(new VM_TimelineStep
                {
                    StatusKey = steps[i].Item1,
                    StatusName = steps[i].Item2,
                    Icon = steps[i].Item3,
                    IsCompleted = done,
                    IsActive = !done && i == curIndex + 1,
                    StatusDate = done ? orderDate.AddHours(i * 8).ToString("dd MMM yyyy, hh:mm tt") : "Pending"
                });
            }
            return timeline;
        }

        // ─── RAZORPAY PAYMENT ────────────────────────────────────────────────────

        public async Task<VM_RazorpayOrderResponse> CreateRazorpayOrderAsync(int vendorUserId, int orderId, decimal amount)
        {
            var response = new VM_RazorpayOrderResponse();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Verify order belongs to vendor
            const string verifySql = @"
                SELECT c_id FROM t_vendor_orders 
                WHERE c_id = @orderId 
                AND c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @userId)";

            await using var verifyCmd = new NpgsqlCommand(verifySql, _conn);
            verifyCmd.Parameters.AddWithValue("@orderId", orderId);
            verifyCmd.Parameters.AddWithValue("@userId", vendorUserId);

            var orderExists = await verifyCmd.ExecuteScalarAsync();
            if (orderExists == null)
            {
                response.Success = false;
                response.Message = "Order not found";
                return response;
            }

            try
            {
                string receipt = $"ORD-{orderId}-{DateTime.Now.Ticks}";

                var razorpayService = new RazorpayService(_configuration);
                dynamic razorpayOrder = await razorpayService.CreateOrderAsync(amount, receipt);

                response.Success = true;
                response.OrderId = orderId.ToString();
                response.RazorpayOrderId = razorpayOrder["id"];
                response.Amount = amount;
                response.Currency = razorpayOrder["currency"];
                response.KeyId = _configuration["Razorpay:KeyId"] ?? "";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
            }

            return response;
        }

        public async Task<VM_RazorpayPaymentResponse> VerifyRazorpayPaymentAsync(int vendorUserId, VM_RazorpayPaymentVerification verification)
        {
            var response = new VM_RazorpayPaymentResponse();

            try
            {
                Console.WriteLine($"=== VERIFYING PAYMENT ===");
                Console.WriteLine($"OrderId: {verification.OrderId}");
                Console.WriteLine($"RazorpayOrderId: {verification.RazorpayOrderId}");
                Console.WriteLine($"RazorpayPaymentId: {verification.RazorpayPaymentId}");
                Console.WriteLine($"RazorpaySignature: {verification.RazorpaySignature}");

                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                await using var transaction = await _conn.BeginTransactionAsync();

                // Verify order belongs to vendor
                const string verifyOrderSql = @"
                    SELECT vo.c_id, vo.c_total_amount
                    FROM t_vendor_orders vo
                    WHERE vo.c_id = @orderId 
                    AND vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @userId)";

                await using var verifyCmd = new NpgsqlCommand(verifyOrderSql, _conn, transaction);
                verifyCmd.Parameters.AddWithValue("@orderId", verification.OrderId);
                verifyCmd.Parameters.AddWithValue("@userId", vendorUserId);

                var result = await verifyCmd.ExecuteReaderAsync();
                decimal orderAmount = 0;
                bool orderFound = false;

                if (await result.ReadAsync())
                {
                    orderFound = true;
                    orderAmount = result.GetDecimal(1);
                }
                await result.CloseAsync();

                if (!orderFound)
                {
                    response.Success = false;
                    response.Message = "Order not found";
                    return response;
                }

                // ✅ FIX: Verify signature with better error handling
                var razorpayService = new RazorpayService(_configuration);
                bool isValid = false;

                try
                {
                    isValid = razorpayService.VerifyPaymentSignature(
                        verification.RazorpayOrderId,
                        verification.RazorpayPaymentId,
                        verification.RazorpaySignature
                    );
                    Console.WriteLine($"Signature verification result: {isValid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Signature verification exception: {ex.Message}");
                    isValid = false;
                }

                if (!isValid)
                {
                    response.Success = false;
                    response.Message = "Payment verification failed - Invalid signature";
                    return response;
                }

                // Check if payment already exists
                const string checkPaymentSql = @"
                    SELECT COUNT(*) FROM t_payments_vendor 
                    WHERE c_order_id = @orderId";

                await using var checkCmd = new NpgsqlCommand(checkPaymentSql, _conn, transaction);
                checkCmd.Parameters.AddWithValue("@orderId", verification.OrderId);
                var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (existingCount > 0)
                {
                    await transaction.CommitAsync();
                    response.Success = true;
                    response.Message = "Payment already verified";
                    response.OrderId = verification.OrderId;
                    response.PaymentId = verification.RazorpayPaymentId;
                    return response;
                }

                // Insert payment record
                const string insertPaymentSql = @"
                    INSERT INTO t_payments_vendor (c_order_id, c_amount, c_status, c_payment_method, c_gateway_ref, c_created_at)
                    VALUES (@orderId, @amount, 'success', 'razorpay', @paymentId, NOW())";

                await using var paymentCmd = new NpgsqlCommand(insertPaymentSql, _conn, transaction);
                paymentCmd.Parameters.AddWithValue("@orderId", verification.OrderId);
                paymentCmd.Parameters.AddWithValue("@amount", orderAmount);
                paymentCmd.Parameters.AddWithValue("@paymentId", verification.RazorpayPaymentId);
                await paymentCmd.ExecuteNonQueryAsync();

                // Update order status
                const string updateOrderSql = @"
                    UPDATE t_vendor_orders 
                    SET c_status = 'admin_confirmed', c_updated_at = NOW()
                    WHERE c_id = @orderId";

                await using var orderCmd = new NpgsqlCommand(updateOrderSql, _conn, transaction);
                orderCmd.Parameters.AddWithValue("@orderId", verification.OrderId);
                await orderCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                response.Success = true;
                response.Message = "Payment successful!";
                response.OrderId = verification.OrderId;
                response.PaymentId = verification.RazorpayPaymentId;

                Console.WriteLine($"Payment verified successfully for OrderId: {verification.OrderId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verification error: {ex.Message}");
                response.Success = false;
                response.Message = ex.Message;
            }

            return response;
        }

        public async Task<object> UpdateAddressAsync(int vendorUserId, int addressId, VM_SaveAddressRequest request)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // Verify address belongs to vendor
            const string verifySql = @"
                SELECT c_id FROM t_vendor_delivery_locations 
                WHERE c_id = @addressId 
                AND c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @userId)";

            await using var verifyCmd = new NpgsqlCommand(verifySql, _conn);
            verifyCmd.Parameters.AddWithValue("@addressId", addressId);
            verifyCmd.Parameters.AddWithValue("@userId", vendorUserId);

            var exists = await verifyCmd.ExecuteScalarAsync();
            if (exists == null)
                return new { success = false, message = "Address not found" };

            // If setting as default, remove default from other addresses
            if (request.IsDefault)
            {
                const string removeDefaultSql = @"
                    UPDATE t_vendor_delivery_locations 
                    SET c_is_active = false 
                    WHERE c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @userId)";

                await using var defaultCmd = new NpgsqlCommand(removeDefaultSql, _conn);
                defaultCmd.Parameters.AddWithValue("@userId", vendorUserId);
                await defaultCmd.ExecuteNonQueryAsync();
            }

            // ✅ REMOVED c_updated_at FROM UPDATE QUERY
            const string updateSql = @"
                UPDATE t_vendor_delivery_locations 
                SET c_address = @address,
                    c_city = @city,
                    c_state = @state,
                    c_pincode = @pincode,
                    c_is_active = @isDefault
                WHERE c_id = @addressId";

            await using var cmd = new NpgsqlCommand(updateSql, _conn);
            cmd.Parameters.AddWithValue("@address", $"{request.AddressLine1} {request.AddressLine2}".Trim());
            cmd.Parameters.AddWithValue("@city", request.City ?? "");
            cmd.Parameters.AddWithValue("@state", request.State ?? "");
            cmd.Parameters.AddWithValue("@pincode", request.ZipCode ?? "");
            cmd.Parameters.AddWithValue("@isDefault", request.IsDefault);
            cmd.Parameters.AddWithValue("@addressId", addressId);

            int rows = await cmd.ExecuteNonQueryAsync();

            return new { success = rows > 0, message = rows > 0 ? "Address updated successfully" : "Failed to update address" };
        }
        public async Task<object> DeleteAddressAsync(int vendorUserId, int addressId)
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            // 1. Verify ownership
            const string verifySql = @"
        SELECT c_id 
        FROM t_vendor_delivery_locations 
        WHERE c_id = @addressId 
        AND c_vendor_id = (
            SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @userId
        )";

            await using (var verifyCmd = new NpgsqlCommand(verifySql, _conn))
            {
                verifyCmd.Parameters.AddWithValue("@addressId", addressId);
                verifyCmd.Parameters.AddWithValue("@userId", vendorUserId);

                var exists = await verifyCmd.ExecuteScalarAsync();
                if (exists == null)
                    return new { success = false, message = "Address not found" };
            }

            // 🔥 2. Remove reference from orders
            const string updateOrdersSql = @"
        UPDATE t_vendor_orders
        SET c_delivery_location_id = NULL
        WHERE c_delivery_location_id = @addressId";

            await using (var updateCmd = new NpgsqlCommand(updateOrdersSql, _conn))
            {
                updateCmd.Parameters.AddWithValue("@addressId", addressId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            // 🔥 3. Delete address
            const string deleteSql = @"
        DELETE FROM t_vendor_delivery_locations
        WHERE c_id = @addressId";

            await using (var deleteCmd = new NpgsqlCommand(deleteSql, _conn))
            {
                deleteCmd.Parameters.AddWithValue("@addressId", addressId);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            return new
            {
                success = true,
                message = "Address deleted successfully"
            };
        }


        // ─── MONTHLY PURCHASE TRENDS ───────────────────────────────────────────
        public async Task<VM_MonthlyTrends> GetMonthlyPurchaseTrendsAsync(int vendorUserId)
        {
            var trends = new VM_MonthlyTrends();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
        SELECT 
            TO_CHAR(DATE_TRUNC('month', vo.c_ordered_at), 'Mon') as month,
            EXTRACT(MONTH FROM vo.c_ordered_at) as month_num,
            COALESCE(SUM(vo.c_total_amount), 0) as total
        FROM t_vendor_orders vo
        WHERE vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
            AND vo.c_ordered_at >= DATE_TRUNC('year', NOW())
            AND vo.c_status != 'cancelled'
        GROUP BY DATE_TRUNC('month', vo.c_ordered_at), EXTRACT(MONTH FROM vo.c_ordered_at)
        ORDER BY month_num";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();

            var monthlyData = new List<VM_MonthlyData>();
            while (await rdr.ReadAsync())
            {
                monthlyData.Add(new VM_MonthlyData
                {
                    Month = rdr.GetString(0),
                    Amount = rdr.GetDecimal(2)
                });
            }

            trends.MonthlyData = monthlyData;
            return trends;
        }

        // ─── SPENDING BY CATEGORY ──────────────────────────────────────────────
        public async Task<List<VM_CategorySpending>> GetCategorySpendingAsync(int vendorUserId)
        {
            var list = new List<VM_CategorySpending>();

            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();

            const string sql = @"
        SELECT 
            cp.c_category,
            COALESCE(SUM(oi.c_quantity * oi.c_unit_price), 0) as total_spent,
            COUNT(DISTINCT oi.c_order_id) as order_count
        FROM t_order_items oi
        INNER JOIN t_catalog_products cp ON cp.c_id = oi.c_catalog_product_id
        INNER JOIN t_vendor_orders vo ON vo.c_id = oi.c_order_id
        WHERE vo.c_vendor_id = (SELECT c_id FROM t_vendor_profiles WHERE c_user_id = @uid)
            AND vo.c_status != 'cancelled'
        GROUP BY cp.c_category
        ORDER BY total_spent DESC";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@uid", vendorUserId);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(new VM_CategorySpending
                {
                    Category = rdr.IsDBNull(0) ? "Other" : rdr.GetString(0),
                    TotalSpent = rdr.GetDecimal(1),
                    OrderCount = rdr.GetInt32(2)
                });
            }

            return list;
        }

        //Elastic Search - Method (Mansi)

        public async Task<SearchResponseModel<CatalogSearchResult>> SearchCatalogAsync(SearchRequestModel request)
        {
            request.IsActive = true;
            return await _elasticService.SearchCatalogForMVCAsync(request);
        }
    }
}