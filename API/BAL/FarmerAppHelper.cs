using API.Models.FarmerApp;
using API.Models.Farmer;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace API.BAL
{
    public class FarmerAppHelper
    {
        private readonly string _connectionString;

        public FarmerAppHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("DefaultConnection is not configured.");
        }

        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        // Dashboard Data
        public async Task<vm_FarmerDashboard> GetDashboardDataAsync(int farmerId)
        {
            var dashboard = new vm_FarmerDashboard();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            string sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM t_farmer_crop_listings WHERE c_farmer_id = @fid) AS total_crops,
                    (SELECT COUNT(*) FROM t_farmer_crop_listings WHERE c_farmer_id = @fid AND c_status = 'QC Requested') AS pending_qc,
                    (SELECT COUNT(*) FROM t_warehouse_slot_bookings WHERE c_farmer_id = @fid AND c_status = 'Confirmed') AS confirmed_qc,
                    (SELECT COALESCE(SUM(c_amount), 0) FROM t_payments_farmer WHERE c_farmer_id = @fid AND c_status = 'success') AS total_payments;

                SELECT DISTINCT cp.c_name, cp.c_unit_of_measure, mp.c_current_price, mp.c_ai_predicted_price
                FROM t_market_prices mp
                JOIN t_catalog_products cp ON mp.c_catalog_product_id = cp.c_id
                JOIN t_farmer_crop_listings cl ON cl.c_catalog_product_id = cp.c_id
                WHERE cl.c_farmer_id = @fid;

                SELECT c_action_type, c_details, c_created_at 
                FROM t_farmer_activities 
                WHERE c_farmer_id = @fid 
                ORDER BY c_created_at DESC LIMIT 10;";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            using var r = await cmd.ExecuteReaderAsync();

            if (await r.ReadAsync())
            {
                dashboard.Kpis.TotalCropsListed = Convert.ToInt32(r[0]);
                dashboard.Kpis.PendingQcRequests = Convert.ToInt32(r[1]);
                dashboard.Kpis.ConfirmedQcSlots = Convert.ToInt32(r[2]);
                dashboard.Kpis.TotalPaymentsReceived = Convert.ToDecimal(r[3]);
            }

            await r.NextResultAsync();
            while (await r.ReadAsync())
            {
                dashboard.MarketPrices.Add(new vm_MarketPriceWidget
                {
                    CropName = r.GetString(0),
                    Unit = r.GetString(1),
                    CurrentMarketPrice = r.GetDecimal(2),
                    AiPredictedPrice = r.GetDecimal(3)
                });
            }

            await r.NextResultAsync();
            while (await r.ReadAsync())
            {
                dashboard.RecentActivities.Add(new vm_RecentActivity
                {
                    ActionType = r.GetString(0),
                    Details = r.GetString(1),
                    CreatedAt = r.GetDateTime(2)
                });
            }

            dashboard.WeatherForecast = new List<vm_WeatherForecast>
            {
                new vm_WeatherForecast { Day = "Today", Temp = 32.5m, Condition = "Sunny" },
                new vm_WeatherForecast { Day = "Tomorrow", Temp = 34.0m, Condition = "Clear" }
            };

            return dashboard;
        }

        // Crop Listings
        public async Task<bool> SaveCropListingAsync(vm_CropListingRequest req)
        {
            try
            {
                await using var _conn = CreateConnection();
                await _conn.OpenAsync();

                string status = req.IsDraft ? "draft" : "active";
                string sql;

                if (req.ListingId == null || req.ListingId == 0)
                {
                    sql = @"INSERT INTO t_farmer_crop_listings 
                            (c_farmer_id, c_catalog_product_id, c_variety, c_quantity_available, 
                            c_unit, c_asking_price, c_harvest_date, c_farm_address, 
                            c_status, c_created_at) 
                            VALUES 
                            (@fid, @pid, @var, @qty, @unit, @price, @hdate, @loc, @status, NOW())";
                }
                else
                {
                    sql = @"UPDATE t_farmer_crop_listings SET 
                                c_quantity_available = @qty,
                                c_asking_price = @price,
                                c_status = @status,
                                c_updated_at = NOW()
                            WHERE c_id = @lid 
                            AND c_farmer_id = @fid 
                            AND c_status IN ('draft', 'active')";
                }

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@lid", req.ListingId ?? 0);
                cmd.Parameters.AddWithValue("@fid", req.FarmerId);
                cmd.Parameters.AddWithValue("@pid", req.CatalogProductId);
                cmd.Parameters.AddWithValue("@var", req.Variety ?? "");
                cmd.Parameters.AddWithValue("@qty", req.AvailableQuantity);
                cmd.Parameters.AddWithValue("@unit", req.Unit ?? "kg");
                cmd.Parameters.AddWithValue("@price", req.AskingPrice);
                cmd.Parameters.AddWithValue("@hdate", req.HarvestDate == DateTime.MinValue ? (object)DBNull.Value : req.HarvestDate);
                cmd.Parameters.AddWithValue("@loc", req.FarmAddress ?? "");
                cmd.Parameters.AddWithValue("@status", status);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR: " + ex.Message);
                throw;
            }
        }

        public async Task<bool> DeleteCropListingAsync(int farmerId, int listingId)
        {
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            const string sql = @"
                DELETE FROM t_farmer_crop_listings
                WHERE c_id = @lid
                AND c_farmer_id = @fid
                AND c_status IN ('draft', 'active')";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@lid", listingId);
            cmd.Parameters.AddWithValue("@fid", farmerId);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Available QC Slots
        public async Task<List<vm_AvailableQcSlot>> GetAvailableQcSlotsAsync(int warehouseId)
        {
            var list = new List<vm_AvailableQcSlot>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            string seedSql = @"
                INSERT INTO t_warehouse_slots (c_warehouse_id, c_slot_date, c_time_start, c_time_end, c_capacity_mt, c_is_available)
                SELECT @wid, d::date, t.start_time, t.end_time, 5.00, true
                FROM generate_series(CURRENT_DATE, CURRENT_DATE + interval '14 days', '1 day'::interval) d
                CROSS JOIN (
                    VALUES 
                        ('09:00:00'::time, '11:00:00'::time),
                        ('11:00:00'::time, '13:00:00'::time),
                        ('14:00:00'::time, '16:00:00'::time)
                ) AS t(start_time, end_time)
                WHERE NOT EXISTS (
                    SELECT 1 FROM t_warehouse_slots ws 
                    WHERE ws.c_warehouse_id = @wid 
                      AND ws.c_slot_date = d::date 
                      AND ws.c_time_start = t.start_time
                );";

            using (var seedCmd = new NpgsqlCommand(seedSql, _conn))
            {
                seedCmd.Parameters.AddWithValue("@wid", warehouseId);
                await seedCmd.ExecuteNonQueryAsync();
            }

            string sql = @"
                SELECT c_id, c_slot_date, c_time_start, c_time_end, c_capacity_mt 
                FROM t_warehouse_slots 
                WHERE c_warehouse_id = @wid AND c_is_available = TRUE AND c_slot_date >= CURRENT_DATE
                ORDER BY c_slot_date, c_time_start";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@wid", warehouseId);
            
            try 
            {
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new vm_AvailableQcSlot
                    {
                        SlotId = Convert.ToInt32(r[0]),
                        SlotDate = DateTime.TryParse(r[1].ToString(), out var d) ? d : DateTime.MinValue,
                        TimeStart = DateTime.TryParse(r[2].ToString(), out var t1) ? t1.TimeOfDay : TimeSpan.Zero,
                        TimeEnd = DateTime.TryParse(r[3].ToString(), out var t2) ? t2.TimeOfDay : TimeSpan.Zero,
                        AvailableCapacityMt = Convert.ToDecimal(r[4])
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GET SLOTS CRASH: " + ex.Message);
            }
            return list;
        }

        // Book QC Slot
        public async Task<bool> BookQcSlotAsync(vm_BookQcSlotRequest req)
        {
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            using var tx = await _conn.BeginTransactionAsync();

            try
            {
                decimal qty = 0;
                string unit = "kg";
                int assignedFoId = 0;
                
                string getInfo = @"
                    SELECT 
                        c_quantity_available, 
                        COALESCE(c_unit, 'kg'),
                        (SELECT c_id FROM t_field_officer_profiles WHERE c_warehouse_id = @wid LIMIT 1)
                    FROM t_farmer_crop_listings 
                    WHERE c_id = @lid";
                    
                using (var cmdInfo = new NpgsqlCommand(getInfo, _conn, tx))
                {
                    cmdInfo.Parameters.AddWithValue("@wid", req.WarehouseId);
                    cmdInfo.Parameters.AddWithValue("@lid", req.CropListingId);
                    using var reader = await cmdInfo.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        qty = reader.GetDecimal(0);
                        unit = reader.GetString(1);
                        assignedFoId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    }
                }

                string bookSlot = @"INSERT INTO t_warehouse_slot_bookings 
                                    (c_warehouse_id, c_farmer_id, c_crop_listing_id, c_slot_date, c_slot_time_start, c_slot_time_end, c_status) 
                                    VALUES (@wid, @fid, @lid, @sdate, @start, @end, 'pending') 
                                    RETURNING c_id";
                
                int newSlotId = 0;
                using (var cmd2 = new NpgsqlCommand(bookSlot, _conn, tx))
                {
                    cmd2.Parameters.AddWithValue("@wid", req.WarehouseId);
                    cmd2.Parameters.AddWithValue("@fid", req.FarmerId);
                    cmd2.Parameters.AddWithValue("@lid", req.CropListingId);
                    cmd2.Parameters.Add(new NpgsqlParameter("@sdate", NpgsqlTypes.NpgsqlDbType.Date) { Value = req.SlotDate });
                    cmd2.Parameters.Add(new NpgsqlParameter("@start", NpgsqlTypes.NpgsqlDbType.Time) { Value = req.TimeStart });
                    cmd2.Parameters.Add(new NpgsqlParameter("@end", NpgsqlTypes.NpgsqlDbType.Time) { Value = req.TimeEnd });
                    
                    var result = await cmd2.ExecuteScalarAsync();
                    newSlotId = Convert.ToInt32(result);
                }

                string createPr = @"INSERT INTO t_procurement_requests 
                                   (c_crop_listing_id, c_farmer_id, c_assigned_fo_id, c_requested_quantity, c_unit, c_status, c_slot_id)
                                   VALUES (@lid, @fid, @foid, @qty, @unit, 'pending', @slotId)";
                using (var cmdPr = new NpgsqlCommand(createPr, _conn, tx))
                {
                    cmdPr.Parameters.AddWithValue("@lid", req.CropListingId);
                    cmdPr.Parameters.AddWithValue("@fid", req.FarmerId);
                    cmdPr.Parameters.AddWithValue("@foid", assignedFoId > 0 ? assignedFoId : DBNull.Value);
                    cmdPr.Parameters.AddWithValue("@qty", qty);
                    cmdPr.Parameters.AddWithValue("@unit", unit);
                    cmdPr.Parameters.AddWithValue("@slotId", newSlotId);
                    await cmdPr.ExecuteNonQueryAsync();
                }

                string logAct = "INSERT INTO t_farmer_activities (c_farmer_id, c_action_type, c_details) VALUES (@fid, 'QC_BOOKED', 'Requested a QC slot. Awaiting Field Officer approval.')";
                using var cmd3 = new NpgsqlCommand(logAct, _conn, tx);
                cmd3.Parameters.AddWithValue("@fid", req.FarmerId);
                await cmd3.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                throw new Exception($"Booking Failed at Database Layer: {ex.Message}"); 
            }
        }

        // Payments
        public async Task<List<vm_FarmerPayment>> GetPaymentHistoryAsync(int farmerId)
        {
            var list = new List<vm_FarmerPayment>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            string sql = @"
                SELECT 
                    pf.c_id, cp.c_name, pf.c_amount, pf.c_payment_number, 
                    pf.c_trigger_event, pf.c_payment_mode, pf.c_utr_reference, 
                    pf.c_status, pf.c_created_at
                FROM t_payments_farmer pf
                LEFT JOIN t_procurement_requests pr ON pf.c_procurement_request_id = pr.c_id
                LEFT JOIN t_farmer_crop_listings cl ON pr.c_crop_listing_id = cl.c_id
                LEFT JOIN t_catalog_products cp ON cl.c_catalog_product_id = cp.c_id
                WHERE pf.c_farmer_id = @fid
                ORDER BY pf.c_created_at DESC";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                list.Add(new vm_FarmerPayment
                {
                    PaymentId = r.GetInt32(0),
                    CropName = r.IsDBNull(1) ? "Crop Payment" : r.GetString(1),
                    Amount = r.GetDecimal(2),
                    PaymentNumber = r.IsDBNull(3) ? 1 : r.GetInt32(3),
                    TriggerEvent = r.IsDBNull(4) ? "" : r.GetString(4),
                    PaymentMode = r.IsDBNull(5) ? "" : r.GetString(5),
                    UtrReference = r.IsDBNull(6) ? "" : r.GetString(6),
                    Status = r.IsDBNull(7) ? "" : r.GetString(7),
                    CreatedAt = r.GetDateTime(8)
                });
            }
            return list;
        }

        public async Task<List<vm_IncomeChartPoint>> GetIncomeChartAsync(int farmerId)
        {
            var list = new List<vm_IncomeChartPoint>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            string sql = @"
                SELECT TO_CHAR(c_created_at, 'Mon YYYY') AS month, SUM(c_amount) AS total
                FROM t_payments_farmer
                WHERE c_farmer_id = @fid AND c_status = 'success'
                GROUP BY TO_CHAR(c_created_at, 'Mon YYYY'), EXTRACT(MONTH FROM c_created_at)
                ORDER BY EXTRACT(MONTH FROM c_created_at) DESC LIMIT 6";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new vm_IncomeChartPoint { Month = r.GetString(0), Amount = r.GetDecimal(1) });
            }
            return list;
        }

        // Dropdowns
        public async Task<List<vm_DropdownItem>> GetCatalogDropdownAsync()
        {
            var list = new List<vm_DropdownItem>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT c_id, c_name FROM t_catalog_products WHERE c_is_active = TRUE ORDER BY c_name", _conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new vm_DropdownItem { Id = r.GetInt32(0), Name = r.GetString(1) });
            return list;
        }

        public async Task<List<vm_DropdownItem>> GetWarehousesDropdownAsync()
        {
            var list = new List<vm_DropdownItem>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT c_id, c_name FROM t_warehouses WHERE c_is_active = TRUE ORDER BY c_name", _conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new vm_DropdownItem { Id = r.GetInt32(0), Name = r.GetString(1) });
            return list;
        }

        public async Task<List<vm_DropdownItem>> GetActiveListingsDropdownAsync(int farmerId)
        {
            var list = new List<vm_DropdownItem>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            
            string sql = @"
                SELECT cl.c_id, 
                       cp.c_name || ' (' || cl.c_quantity_available || ' ' || COALESCE(cl.c_unit, 'kg') || ')'
                FROM t_farmer_crop_listings cl
                JOIN t_catalog_products cp ON cl.c_catalog_product_id = cp.c_id
                WHERE cl.c_farmer_id = @fid AND LOWER(cl.c_status) IN ('draft', 'active')";
                
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) 
            {
                list.Add(new vm_DropdownItem 
                { 
                    Id = r.GetInt32(0), 
                    Name = r.IsDBNull(1) ? "Unnamed Crop" : r.GetString(1) 
                });
            }
            return list;
        }

        // Listings & Profile
        public async Task<List<vm_CropListingResponse>> GetFarmerListingsAsync(int farmerId)
        {
            var list = new List<vm_CropListingResponse>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            string sql = @"
                SELECT cl.c_id, cp.c_name, cl.c_variety, cl.c_quantity_available, cl.c_unit, cl.c_asking_price, cl.c_status, cl.c_created_at
                FROM t_farmer_crop_listings cl
                JOIN t_catalog_products cp ON cl.c_catalog_product_id = cp.c_id
                WHERE cl.c_farmer_id = @fid ORDER BY cl.c_created_at DESC";
            
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new vm_CropListingResponse
                {
                    ListingId = r.GetInt32(0),
                    CropName = r.GetString(1),
                    Variety = r.IsDBNull(2) ? "" : r.GetString(2),
                    AvailableQuantity = r.GetDecimal(3),
                    Unit = r.IsDBNull(4) ? "" : r.GetString(4),
                    AskingPrice = r.GetDecimal(5),
                    Status = r.IsDBNull(6) ? "" : r.GetString(6),
                    CreatedAt = r.GetDateTime(7)
                });
            }
            return list;
        }

        public async Task<vm_QcDashboard> GetQcDashboardAsync(int farmerId)
        {
            var dash = new vm_QcDashboard();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            string kpiSql = @"
                SELECT 
                    (SELECT COUNT(*) FROM t_warehouse_slot_bookings WHERE c_farmer_id = @fid AND c_status IN ('pending', 'confirmed') AND c_slot_date >= CURRENT_DATE) AS upcoming,
                    (SELECT COUNT(*) FROM t_procurement_requests WHERE c_farmer_id = @fid AND c_status = 'scheduled') AS awaiting,
                    (SELECT COUNT(*) FROM t_procurement_requests pr JOIN t_quality_inspection_forms qi ON pr.c_id = qi.c_procurement_request_id WHERE pr.c_farmer_id = @fid AND qi.c_passed = TRUE) AS passed,
                    (SELECT COALESCE(ROUND((COUNT(*) FILTER (WHERE qi.c_grade IN ('A', 'Premium')) * 100.0) / NULLIF(COUNT(*), 0), 1), 0) FROM t_procurement_requests pr JOIN t_quality_inspection_forms qi ON pr.c_id = qi.c_procurement_request_id WHERE pr.c_farmer_id = @fid) AS premium_rate";
            
            using var cmd1 = new NpgsqlCommand(kpiSql, _conn);
            cmd1.Parameters.AddWithValue("@fid", farmerId);
            using var r1 = await cmd1.ExecuteReaderAsync();
            if (await r1.ReadAsync())
            {
                dash.UpcomingAppts = Convert.ToInt32(r1[0]);
                dash.AwaitingResults = Convert.ToInt32(r1[1]);
                dash.TotalQcPassed = Convert.ToInt32(r1[2]);
                dash.PremiumGradeRate = Convert.ToDecimal(r1[3]);
            }
            await r1.CloseAsync();

            string listSql = @"
                SELECT wsb.c_slot_date, wsb.c_slot_time_start, cp.c_name, wsb.c_status
                FROM t_warehouse_slot_bookings wsb
                JOIN t_farmer_crop_listings cl ON wsb.c_crop_listing_id = cl.c_id
                JOIN t_catalog_products cp ON cl.c_catalog_product_id = cp.c_id
                WHERE wsb.c_farmer_id = @fid ORDER BY wsb.c_slot_date DESC";
            
            using var cmd2 = new NpgsqlCommand(listSql, _conn);
            cmd2.Parameters.AddWithValue("@fid", farmerId);
            using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync())
            {
                var rawDate = r2[0].ToString() ?? "";
                var rawStart = r2[1].ToString() ?? "";

                dash.Appointments.Add(new vm_QcAppointment
                {
                    SlotDate = DateTime.TryParse(rawDate, out var d) ? d : DateTime.MinValue,
                    TimeStart = DateTime.TryParse(rawStart, out var t) ? t.TimeOfDay : TimeSpan.Zero,
                    CropName = r2.IsDBNull(2) ? "" : r2.GetString(2),
                    Status = r2.IsDBNull(3) ? "" : r2.GetString(3)
                });
            }
            return dash;
        }

        // Inquiries
        public async Task<List<vm_Inquiry>> GetInquiriesAsync(int farmerId)
        {
            var list = new List<vm_Inquiry>();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            string sql = "SELECT c_id, c_status, c_message, c_created_at FROM t_payment_inquiries WHERE c_farmer_id = @fid ORDER BY c_created_at DESC";
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new vm_Inquiry { Id = r.GetInt32(0), Status = r.GetString(1), Message = r.IsDBNull(2) ? "" : r.GetString(2), CreatedAt = r.GetDateTime(3) });
            }
            return list;
        }

        public async Task<bool> SubmitInquiryAsync(vm_SubmitInquiryRequest req)
        {
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            string fullMessage = $"[{req.Department} - {req.Subject}]\n{req.Details}";
            
            string sql = "INSERT INTO t_payment_inquiries (c_farmer_id, c_payment_id, c_message, c_status, c_created_at) VALUES (@fid, @pid, @msg, 'open', NOW())";
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", req.FarmerId);
            cmd.Parameters.AddWithValue("@pid", req.PaymentId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@msg", fullMessage);
            
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Profile Update
        public async Task<vm_FarmerProfileResponse> GetFarmerProfileAsync(int farmerId)
        {
            var profile = new vm_FarmerProfileResponse();
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();

            string sql = @"
                SELECT fp.c_full_name, u.c_email, fp.c_phone, fp.c_state, fp.c_district, fp.c_address,
                       b.c_bank_name, b.c_account_holder_name, b.c_account_number, b.c_ifsc_code,
                       b.c_branch_name, b.c_account_type, b.c_upi_id,
                       u.c_profile_image_url
                FROM t_farmer_profiles fp
                JOIN t_users u ON u.c_id = fp.c_user_id
                LEFT JOIN t_bank_accounts b ON u.c_id = b.c_user_id
                WHERE fp.c_id = @fid";

            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@fid", farmerId);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                profile.FullName = r.IsDBNull(0) ? "" : r.GetString(0);
                profile.Email = r.IsDBNull(1) ? "" : r.GetString(1);
                profile.Phone = r.IsDBNull(2) ? "" : r.GetString(2);
                profile.State = r.IsDBNull(3) ? "" : r.GetString(3);
                profile.District = r.IsDBNull(4) ? "" : r.GetString(4);
                profile.Address = r.IsDBNull(5) ? "" : r.GetString(5);
                
                profile.BankName = r.IsDBNull(6) ? "" : r.GetString(6);
                profile.AccountHolderName = r.IsDBNull(7) ? "" : r.GetString(7);
                profile.AccountNumber = r.IsDBNull(8) ? "" : r.GetString(8);
                profile.IfscCode = r.IsDBNull(9) ? "" : r.GetString(9);
                profile.BranchName = r.IsDBNull(10) ? "" : r.GetString(10);
                profile.AccountType = r.IsDBNull(11) ? "" : r.GetString(11);
                profile.UpiId = r.IsDBNull(12) ? "" : r.GetString(12);
                profile.ImageUrl = r.IsDBNull(13) ? "" : r.GetString(13);
            }
            return profile;
        }

        public async Task<bool> UpdateFarmerProfileAsync(vm_UpdateProfileRequest req)
        {
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            using var tx = await _conn.BeginTransactionAsync();
            try
            {
                string sql1 = "UPDATE t_farmer_profiles SET c_full_name = @fn, c_phone = @ph, c_state = @st, c_district = @dt, c_address = @addr WHERE c_id = @fid";
                using var cmd1 = new NpgsqlCommand(sql1, _conn, tx);
                cmd1.Parameters.AddWithValue("@fid", req.FarmerId);
                cmd1.Parameters.AddWithValue("@fn", req.FullName ?? "");
                cmd1.Parameters.AddWithValue("@ph", req.Phone ?? "");
                cmd1.Parameters.AddWithValue("@st", req.State ?? "");
                cmd1.Parameters.AddWithValue("@dt", req.District ?? "");
                cmd1.Parameters.AddWithValue("@addr", req.Address ?? "");
                await cmd1.ExecuteNonQueryAsync();

                string sql2 = @"
                    UPDATE t_bank_accounts SET 
                    c_bank_name = @bn, c_account_holder_name = @ahn, c_account_number = @an, c_ifsc_code = @ifsc,
                    c_branch_name = @brn, c_account_type = @at, c_upi_id = @upi
                    WHERE c_user_id = (SELECT c_user_id FROM t_farmer_profiles WHERE c_id = @fid)";
                using var cmd2 = new NpgsqlCommand(sql2, _conn, tx);
                cmd2.Parameters.AddWithValue("@fid", req.FarmerId);
                cmd2.Parameters.AddWithValue("@bn", req.BankName ?? "");
                cmd2.Parameters.AddWithValue("@ahn", req.AccountHolderName ?? "");
                cmd2.Parameters.AddWithValue("@an", req.AccountNumber ?? "");
                cmd2.Parameters.AddWithValue("@ifsc", req.IfscCode ?? "");
                cmd2.Parameters.AddWithValue("@brn", req.BranchName ?? "");
                cmd2.Parameters.AddWithValue("@at", req.AccountType ?? "");
                cmd2.Parameters.AddWithValue("@upi", req.UpiId ?? "");
                await cmd2.ExecuteNonQueryAsync();

                string sql3 = "UPDATE t_users SET c_profile_image_url = @img WHERE c_id = (SELECT c_user_id FROM t_farmer_profiles WHERE c_id = @fid)";
                using var cmd3 = new NpgsqlCommand(sql3, _conn, tx);
                cmd3.Parameters.AddWithValue("@fid", req.FarmerId);
                cmd3.Parameters.AddWithValue("@img", req.ImageUrl ?? "");
                await cmd3.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                return false;
            }
        }

        public async Task<int> GetFieldOfficerUserIdByWarehouseAsync(int warehouseId)
        {
            await using var _conn = CreateConnection();
            await _conn.OpenAsync();
            string sql = "SELECT c_user_id FROM t_field_officer_profiles WHERE c_warehouse_id = @wid LIMIT 1";
            using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@wid", warehouseId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
    }
}