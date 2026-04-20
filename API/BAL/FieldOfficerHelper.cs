using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using API.Models;
using API.Models.FieldOfficer;
using API.Services;
using Npgsql;

namespace API.BAL
{
    public class FieldOfficerHelper
    {
        private readonly NpgsqlConnection _conn;
        public FieldOfficerHelper(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        // ── RESOLVE FO PROFILE ID BY USER ID ────────────────────────
        public async Task<int?> GetFoProfileIdByUserIdAsync(int userId)
        {
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    SELECT c_id
                    FROM t_field_officer_profiles
                    WHERE c_user_id = @userId
                    LIMIT 1
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@userId", userId);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return null;

                return Convert.ToInt32(result);
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET TODAY SCHEDULES ─────────────────────────────────────
        public async Task<List<object>> GetTodaySchedules(int foId)
        {
            var list = new List<object>();
            await _conn.OpenAsync();

            try
            {
                var query = @"SELECT 
                            pr.c_id,
                            fp.c_full_name,
                            cp.c_name,
                            pr.c_requested_quantity,
                            pr.c_unit,
                            fp.c_district,
                            wsb.c_slot_date,
                            wsb.c_slot_time_start,
                            wsb.c_slot_time_end,
                            pr.c_status,
                            fcl.c_asking_price
                        FROM t_procurement_requests pr
                        JOIN t_warehouse_slot_bookings wsb 
                            ON pr.c_slot_id = wsb.c_id
                        JOIN t_farmer_profiles fp 
                            ON pr.c_farmer_id = fp.c_id
                        JOIN t_farmer_crop_listings fcl 
                            ON pr.c_crop_listing_id = fcl.c_id
                        JOIN t_catalog_products cp 
                            ON fcl.c_catalog_product_id = cp.c_id
                        WHERE 
                            pr.c_assigned_fo_id = @foId
                            AND wsb.c_slot_date = CURRENT_DATE
                        ORDER BY wsb.c_slot_time_start";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        id         = reader.GetInt32(0),
                        farmerName = reader.GetString(1),
                        crop       = reader.GetString(2),
                        quantity   = reader.GetDecimal(3),
                        unit       = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        location   = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        slotDate   = reader.GetDateTime(6),
                        startTime  = reader.GetTimeSpan(7).ToString(@"hh\:mm"),
                        endTime    = reader.GetTimeSpan(8).ToString(@"hh\:mm"),
                        status     = FormatStatus(reader.GetString(9)),
                        askingPrice = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10)
                    });
                }

                return list;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET QC REQUESTS ─────────────────────────────────────────
        public async Task<List<vmQCRequest>> GetQCRequests(int foId)
        {
            var list = new List<vmQCRequest>();
            await _conn.OpenAsync();

            try
            {
                var query = @"SELECT 
                            pr.c_id,
                            fp.c_full_name,
                            cp.c_name,
                            pr.c_requested_quantity,
                            pr.c_unit,
                            fp.c_district,
                            wsb.c_id,
                            wsb.c_slot_date,
                            wsb.c_slot_time_start,
                            wsb.c_slot_time_end,
                            pr.c_status,
                            fcl.c_asking_price
                        FROM t_procurement_requests pr
                        JOIN t_farmer_profiles fp 
                            ON pr.c_farmer_id = fp.c_id
                        JOIN t_farmer_crop_listings fcl 
                            ON pr.c_crop_listing_id = fcl.c_id
                        JOIN t_catalog_products cp 
                            ON fcl.c_catalog_product_id = cp.c_id
                        LEFT JOIN t_warehouse_slot_bookings wsb
                            ON wsb.c_id = pr.c_slot_id
                        WHERE pr.c_assigned_fo_id = @foId
                        ORDER BY 
                            CASE 
                                WHEN pr.c_status = 'pending'   THEN 1
                                WHEN pr.c_status = 'scheduled' THEN 2
                                WHEN pr.c_status = 'completed' THEN 3
                                ELSE 4
                            END,
                            pr.c_id DESC";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new vmQCRequest
                    {
                        ProcurementRequestId = reader.GetInt32(0),
                        FarmerName           = reader.GetString(1),
                        CropType             = reader.GetString(2),
                        Quantity             = reader.GetDecimal(3),
                        Unit                 = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Location             = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        SlotId               = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                        SlotDate             = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                        StartTime            = reader.IsDBNull(8) ? (TimeSpan?)null : reader.GetTimeSpan(8),
                        EndTime              = reader.IsDBNull(9) ? (TimeSpan?)null : reader.GetTimeSpan(9),
                        Status               = reader.GetString(10),
                        AskingPrice          = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11)
                    });
                }

                return list;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET SINGLE QC REQUEST BY ID ─────────────────────────────
        // Used by Quality Form page to pre-fill farmer/crop details
        public async Task<vmQCRequest?> GetQCRequestById(int requestId)
        {
            await _conn.OpenAsync();

            try
            {
                var query = @"SELECT 
                            pr.c_id,
                            fp.c_full_name,
                            cp.c_name,
                            pr.c_requested_quantity,
                            pr.c_unit,
                            fp.c_district,
                            wsb.c_id,
                            wsb.c_slot_date,
                            wsb.c_slot_time_start,
                            wsb.c_slot_time_end,
                            pr.c_status,
                            fcl.c_asking_price
                        FROM t_procurement_requests pr
                        JOIN t_farmer_profiles fp 
                            ON pr.c_farmer_id = fp.c_id
                        JOIN t_farmer_crop_listings fcl 
                            ON pr.c_crop_listing_id = fcl.c_id
                        JOIN t_catalog_products cp 
                            ON fcl.c_catalog_product_id = cp.c_id
                        LEFT JOIN t_warehouse_slot_bookings wsb
                            ON wsb.c_id = pr.c_slot_id
                        WHERE pr.c_id = @requestId";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@requestId", requestId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                return new vmQCRequest
                {
                    ProcurementRequestId = reader.GetInt32(0),
                    FarmerName           = reader.GetString(1),
                    CropType             = reader.GetString(2),
                    Quantity             = reader.GetDecimal(3),
                    Unit                 = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Location             = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    SlotId               = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                    SlotDate             = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                    StartTime            = reader.IsDBNull(8) ? (TimeSpan?)null : reader.GetTimeSpan(8),
                    EndTime              = reader.IsDBNull(9) ? (TimeSpan?)null : reader.GetTimeSpan(9),
                    Status               = reader.GetString(10),
                    AskingPrice          = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11)
                };
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── FARMER CONTACT (email notifications) ────────────────────
        public async Task<FarmerNotifyInfo?> GetFarmerNotifyInfo(int procurementRequestId)
        {
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    SELECT u.c_email, fp.c_full_name, cp.c_name
                    FROM t_procurement_requests pr
                    JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
                    JOIN t_users u ON fp.c_user_id = u.c_id
                    JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
                    JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
                    WHERE pr.c_id = @requestId
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@requestId", procurementRequestId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                return new FarmerNotifyInfo
                {
                    Email     = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    FullName  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    CropName  = reader.IsDBNull(2) ? "" : reader.GetString(2)
                };
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── SUBMIT QUALITY INSPECTION ────────────────────────────────
        // Inserts inspection form + updates procurement request + creates warehouse lot if passed
        public async Task<bool> SubmitInspection(QualityInspectionForm model)
        {
            var inspectionId = await SubmitInspectionAndGetId(model);
            return inspectionId.HasValue;
        }

        // ── SUBMIT QUALITY INSPECTION & RETURN ID ────────────────────
        public async Task<int?> SubmitInspectionAndGetId(QualityInspectionForm model)
        {
            await _conn.OpenAsync();
            using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                // STEP 1: Get required IDs + farmer's asking price
                int farmerId         = 0;
                int cropListingId    = 0;
                int warehouseId      = 0;
                int catalogProductId = 0;
                decimal farmerAskingPrice = 0;

                var getDetailsQuery = @"
                    SELECT 
                        pr.c_farmer_id,
                        pr.c_crop_listing_id,
                        fop.c_warehouse_id,
                        fcl.c_catalog_product_id,
                        fcl.c_asking_price
                    FROM t_procurement_requests pr
                    JOIN t_field_officer_profiles fop ON fop.c_id = @foId
                    JOIN t_farmer_crop_listings fcl ON fcl.c_id = pr.c_crop_listing_id
                    WHERE pr.c_id = @requestId
                ";

                using (var cmd = new NpgsqlCommand(getDetailsQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@foId",      model.FoId);
                    cmd.Parameters.AddWithValue("@requestId", model.ProcurementRequestId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        throw new Exception("Procurement request not found");

                    farmerId         = reader.GetInt32(0);
                    cropListingId    = reader.GetInt32(1);
                    warehouseId      = reader.GetInt32(2);
                    catalogProductId = reader.GetInt32(3);
                    farmerAskingPrice = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                }

                // If FO didn't provide assessed price, use farmer's asking price
                decimal finalPrice = model.FoAssessedPrice > 0 ? model.FoAssessedPrice : farmerAskingPrice;

                // STEP 2: Insert into t_quality_inspection_forms
                int inspectionId = 0;

                var insertInspectionQuery = @"
                    INSERT INTO t_quality_inspection_forms (
                        c_procurement_request_id,
                        c_fo_id,
                        c_moisture_pct,
                        c_foreign_matter_pct,
                        c_pest_disease_observed,
                        c_variety,
                        c_grade,
                        c_weight_checked_kg,
                        c_accepted_quantity,
                        c_rejected_quantity,
                        c_defects_noted,
                        c_remarks,
                        c_passed,
                        c_fo_assessed_price,
                        c_submitted_at
                    ) VALUES (
                        @procurementRequestId,
                        @foId,
                        @moisturePct,
                        @foreignMatterPct,
                        @pestDisease,
                        @variety,
                        @grade,
                        @weightChecked,
                        @acceptedQty,
                        @rejectedQty,
                        @defectsNoted,
                        @remarks,
                        @passed,
                        @foAssessedPrice,
                        NOW()
                    ) RETURNING c_id
                ";

                using (var cmd = new NpgsqlCommand(insertInspectionQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@procurementRequestId", model.ProcurementRequestId);
                    cmd.Parameters.AddWithValue("@foId",                 model.FoId);
                    cmd.Parameters.AddWithValue("@moisturePct",          model.MoisturePct);
                    cmd.Parameters.AddWithValue("@foreignMatterPct",     model.ForeignMatterPct);
                    cmd.Parameters.AddWithValue("@pestDisease",          model.PestDiseaseObserved  ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@variety",              model.Variety              ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@grade",                model.Grade               ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@weightChecked",        model.WeightCheckedKg);
                    cmd.Parameters.AddWithValue("@acceptedQty",          model.AcceptedQuantity);
                    cmd.Parameters.AddWithValue("@rejectedQty",          model.RejectedQuantity);
                    cmd.Parameters.AddWithValue("@defectsNoted",         model.DefectsNoted         ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@remarks",              model.Remarks              ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@passed",               model.Passed);
                    cmd.Parameters.AddWithValue("@foAssessedPrice",      finalPrice);  // Use fallback price

                    inspectionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // STEP 3: Update procurement request → completed
                var updateRequestQuery = @"
                    UPDATE t_procurement_requests
                    SET c_status     = 'completed',
                        c_updated_at = NOW()
                    WHERE c_id = @requestId
                ";

                using (var cmd = new NpgsqlCommand(updateRequestQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@requestId", model.ProcurementRequestId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // STEP 4: Update farmer crop listing status
                var listingStatus    = model.Passed ? "qc_passed" : "qc_failed";

                var updateListingQuery = @"
                    UPDATE t_farmer_crop_listings
                    SET c_status     = @status,
                        c_updated_at = NOW()
                    WHERE c_id = @cropListingId
                ";

                using (var cmd = new NpgsqlCommand(updateListingQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@status",        listingStatus);
                    cmd.Parameters.AddWithValue("@cropListingId", cropListingId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // STEP 5: If PASSED → create warehouse lot
                if (model.Passed)
                {
                    var insertLotQuery = @"
                        INSERT INTO t_warehouse_lots (
                            c_warehouse_id,
                            c_catalog_product_id,
                            c_farmer_id,
                            c_fo_id,
                            c_quality_inspection_id,
                            c_grade,
                            c_unit,
                            c_quantity_accepted,
                            c_quantity_remaining,
                            c_status,
                            c_created_at,
                            c_updated_at
                        ) VALUES (
                            @warehouseId,
                            @catalogProductId,
                            @farmerId,
                            @foId,
                            @inspectionId,
                            @grade,
                            'kg',
                            @acceptedQty,
                            @acceptedQty,
                            'available',
                            NOW(),
                            NOW()
                        )
                    ";

                    using var cmd = new NpgsqlCommand(insertLotQuery, _conn, transaction);
                    cmd.Parameters.AddWithValue("@warehouseId",      warehouseId);
                    cmd.Parameters.AddWithValue("@catalogProductId", catalogProductId);
                    cmd.Parameters.AddWithValue("@farmerId",         farmerId);
                    cmd.Parameters.AddWithValue("@foId",             model.FoId);
                    cmd.Parameters.AddWithValue("@inspectionId",     inspectionId);
                    cmd.Parameters.AddWithValue("@grade",            model.Grade ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@acceptedQty",      model.AcceptedQuantity);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return inspectionId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("SubmitInspection ERROR: " + ex.Message);
                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── SAVE INSPECTION PHOTOS ───────────────────────────────────
        // Stores uploaded Cloudinary URLs against the inspection.
        public async Task SaveInspectionPhotosAsync(int inspectionId, List<string> photoUrls)
        {
            if (inspectionId <= 0 || photoUrls == null || photoUrls.Count == 0) return;

            await _conn.OpenAsync();
            using var transaction = await _conn.BeginTransactionAsync();
            try
            {
                var query = @"
                    INSERT INTO t_inspection_photos (
                        c_inspection_id,
                        c_photo_url
                    ) VALUES (
                        @inspectionId,
                        @photoUrl
                    )
                ";

                foreach (var url in photoUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    using var cmd = new NpgsqlCommand(query, _conn, transaction);
                    cmd.Parameters.AddWithValue("@inspectionId", inspectionId);
                    cmd.Parameters.AddWithValue("@photoUrl", url);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("SaveInspectionPhotosAsync ERROR: " + ex.Message);
                throw;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
        // ── ACCEPT REQUEST ──────────────────────────────────────────
        public async Task<AcceptEmailData?> AcceptRequest(int requestId)
        {
            await _conn.OpenAsync();
            using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                var updateRequest = new NpgsqlCommand(@"
                    UPDATE t_procurement_requests
                    SET c_status = 'scheduled'
                    WHERE c_id = @id
                ", _conn, transaction);
                updateRequest.Parameters.AddWithValue("@id", requestId);
                var n = await updateRequest.ExecuteNonQueryAsync();
                if (n == 0)
                {
                    await transaction.RollbackAsync();
                    return null;
                }

                var updateSlot = new NpgsqlCommand(@"
                    UPDATE t_warehouse_slot_bookings
                    SET c_check_in_status = 'scheduled',
                        c_status = 'confirmed'
                    WHERE c_id = (
                        SELECT c_slot_id 
                        FROM t_procurement_requests 
                        WHERE c_id = @id
                    )
                ", _conn, transaction);
                updateSlot.Parameters.AddWithValue("@id", requestId);
                await updateSlot.ExecuteNonQueryAsync();

                var notifySql = @"
                    SELECT u.c_email, fp.c_full_name, cp.c_name,
                           wsb.c_slot_date, wsb.c_slot_time_start, wsb.c_slot_time_end,
                           COALESCE(w.c_name, '—'),
                           pr.c_requested_quantity,
                           COALESCE(pr.c_unit, 'kg')
                    FROM t_procurement_requests pr
                    JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
                    JOIN t_users u ON fp.c_user_id = u.c_id
                    JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
                    JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
                    LEFT JOIN t_field_officer_profiles fop ON pr.c_assigned_fo_id = fop.c_id
                    LEFT JOIN t_warehouses w ON fop.c_warehouse_id = w.c_id
                    LEFT JOIN t_warehouse_slot_bookings wsb ON pr.c_slot_id = wsb.c_id
                    WHERE pr.c_id = @requestId
                ";

                var acceptedAt = DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt");
                AcceptEmailData? data = null;
                using (var cmd = new NpgsqlCommand(notifySql, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@requestId", requestId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var email = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        var name  = reader.IsDBNull(1) ? "Farmer" : reader.GetString(1);
                        var crop  = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        var slotDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                        var slotStart = reader.IsDBNull(4) ? (TimeSpan?)null : reader.GetTimeSpan(4);
                        var slotEnd   = reader.IsDBNull(5) ? (TimeSpan?)null : reader.GetTimeSpan(5);
                        var whName    = reader.IsDBNull(6) ? "—" : reader.GetString(6);
                        var qty       = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7);
                        var unit      = reader.IsDBNull(8) ? "kg" : reader.GetString(8);

                        data = new AcceptEmailData
                        {
                            ProcurementRequestId = requestId,
                            FarmerEmail          = email,
                            FarmerName           = name,
                            CropName             = crop,
                            WarehouseName        = whName,
                            QuantityDisplay      = $"{qty} {unit}".Trim(),
                            SlotDateFormatted    = slotDate?.ToString("MMM dd, yyyy") ?? "—",
                            TimeRange            = slotStart.HasValue && slotEnd.HasValue
                                ? $"{slotStart.Value:hh\\:mm} – {slotEnd.Value:hh\\:mm}"
                                : "—",
                            AcceptedAtFormatted  = acceptedAt
                        };
                    }
                }

                await transaction.CommitAsync();
                return data;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("AcceptRequest ERROR: " + ex.Message);
                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── RESCHEDULE REQUEST ──────────────────────────────────────
        public async Task<RescheduleEmailData?> RescheduleRequest(int requestId, DateTime date, TimeSpan start, TimeSpan end)
        {
            await _conn.OpenAsync();

            try
            {
                int? oldSlotId     = null;
                int  farmerId      = 0;
                int  cropListingId = 0;
                RescheduleEmailData? emailData = null;

                var getQuery = @"
                    SELECT 
                        pr.c_slot_id,
                        pr.c_farmer_id,
                        pr.c_crop_listing_id,
                        u.c_email,
                        fp.c_full_name,
                        cp.c_name,
                        wsb.c_slot_date,
                        wsb.c_slot_time_start,
                        wsb.c_slot_time_end,
                        COALESCE(wh.c_name, '—')
                    FROM t_procurement_requests pr
                    JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
                    JOIN t_users u ON fp.c_user_id = u.c_id
                    JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
                    JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
                    LEFT JOIN t_field_officer_profiles fop ON pr.c_assigned_fo_id = fop.c_id
                    LEFT JOIN t_warehouses wh ON fop.c_warehouse_id = wh.c_id
                    LEFT JOIN t_warehouse_slot_bookings wsb ON pr.c_slot_id = wsb.c_id
                    WHERE pr.c_id = @requestId
                ";

                using (var cmd = new NpgsqlCommand(getQuery, _conn))
                {
                    cmd.Parameters.AddWithValue("@requestId", requestId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return null;

                    oldSlotId     = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                    farmerId      = reader.GetInt32(1);
                    cropListingId = reader.GetInt32(2);
                    var email     = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var name      = reader.IsDBNull(4) ? "Farmer" : reader.GetString(4);
                    var crop      = reader.IsDBNull(5) ? "" : reader.GetString(5);
                    var oldDate   = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                    var oldStart  = reader.IsDBNull(7) ? (TimeSpan?)null : reader.GetTimeSpan(7);
                    var oldEnd    = reader.IsDBNull(8) ? (TimeSpan?)null : reader.GetTimeSpan(8);
                    var whName    = reader.IsDBNull(9) ? "—" : reader.GetString(9);

                    emailData = new RescheduleEmailData
                    {
                        ProcurementRequestId  = requestId,
                        FarmerEmail           = email,
                        FarmerName            = name,
                        CropName              = crop,
                        WarehouseName         = whName,
                        OldSlotDateFormatted  = oldDate?.ToString("MMM dd, yyyy") ?? "—",
                        OldTimeRange          = oldStart.HasValue && oldEnd.HasValue
                            ? $"{oldStart.Value:hh\\:mm} – {oldEnd.Value:hh\\:mm}"
                            : "—",
                        NewSlotDateFormatted  = date.ToString("MMM dd, yyyy"),
                        NewTimeRange          = $"{start:hh\\:mm} – {end:hh\\:mm}"
                    };
                }

                int newSlotId = 0;

                // Check if slot exists for same date+time
                var checkQuery = @"
                    SELECT c_id FROM t_warehouse_slot_bookings
                    WHERE c_farmer_id = @farmerId
                      AND c_slot_date = @date
                      AND c_slot_time_start = @start
                ";

                using (var cmd = new NpgsqlCommand(checkQuery, _conn))
                {
                    cmd.Parameters.AddWithValue("@farmerId", farmerId);
                    cmd.Parameters.AddWithValue("@date",     date);
                    cmd.Parameters.AddWithValue("@start",    start);

                    var existing = await cmd.ExecuteScalarAsync();
                    if (existing != null)
                    {
                        newSlotId = Convert.ToInt32(existing);
                    }
                    else
                    {
                        var insertSlotQuery = @"
                            INSERT INTO t_warehouse_slot_bookings
                            (c_warehouse_id, c_farmer_id, c_crop_listing_id,
                             c_slot_date, c_slot_time_start, c_slot_time_end,
                             c_check_in_status, c_status, c_booked_at)
                            VALUES
                            (1, @farmerId, @cropListingId,
                             @date, @start, @end,
                             'scheduled', 'confirmed', NOW())
                            RETURNING c_id
                        ";

                        using var insertCmd = new NpgsqlCommand(insertSlotQuery, _conn);
                        insertCmd.Parameters.AddWithValue("@farmerId",      farmerId);
                        insertCmd.Parameters.AddWithValue("@cropListingId", cropListingId);
                        insertCmd.Parameters.AddWithValue("@date",          date);
                        insertCmd.Parameters.AddWithValue("@start",         start);
                        insertCmd.Parameters.AddWithValue("@end",           end);

                        newSlotId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                    }
                }

                // Update procurement request
                var updateRequestQuery = @"
                    UPDATE t_procurement_requests
                    SET c_slot_id    = @newSlotId,
                        c_status     = 'scheduled',
                        c_updated_at = NOW()
                    WHERE c_id = @requestId
                ";

                using (var cmd = new NpgsqlCommand(updateRequestQuery, _conn))
                {
                    cmd.Parameters.AddWithValue("@newSlotId",  newSlotId);
                    cmd.Parameters.AddWithValue("@requestId",  requestId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Mark old slot as rescheduled
                if (oldSlotId.HasValue)
                {
                    var updateOldSlot = @"
                        UPDATE t_warehouse_slot_bookings
                        SET c_status = 'rescheduled'
                        WHERE c_id = @oldSlotId
                    ";

                    using var cmd = new NpgsqlCommand(updateOldSlot, _conn);
                    cmd.Parameters.AddWithValue("@oldSlotId", oldSlotId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                return emailData;
            }
            catch (Exception ex)
            {
                Console.WriteLine("RescheduleRequest ERROR: " + ex.Message);
                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── CANCEL REQUEST (FO) ─────────────────────────────────────
        public async Task<CancelEmailData?> CancelProcurementRequest(int requestId)
        {
            await _conn.OpenAsync();

            try
            {
                var loadSql = @"
                    SELECT pr.c_status, pr.c_slot_id, u.c_email, fp.c_full_name, cp.c_name,
                           wsb.c_slot_date, wsb.c_slot_time_start, wsb.c_slot_time_end,
                           COALESCE(w.c_name, '—')
                    FROM t_procurement_requests pr
                    JOIN t_farmer_profiles fp ON pr.c_farmer_id = fp.c_id
                    JOIN t_users u ON fp.c_user_id = u.c_id
                    JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
                    JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
                    LEFT JOIN t_field_officer_profiles fop ON pr.c_assigned_fo_id = fop.c_id
                    LEFT JOIN t_warehouses w ON fop.c_warehouse_id = w.c_id
                    LEFT JOIN t_warehouse_slot_bookings wsb ON pr.c_slot_id = wsb.c_id
                    WHERE pr.c_id = @requestId
                ";

                string status = "";
                int? slotId = null;
                string email = "";
                string name = "Farmer";
                string crop = "";
                DateTime? slotDate = null;
                TimeSpan? slotStart = null;
                TimeSpan? slotEnd = null;
                string warehouseName = "—";

                using (var cmd = new NpgsqlCommand(loadSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@requestId", requestId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return null;

                    status = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    slotId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                    email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    name = reader.IsDBNull(3) ? "Farmer" : reader.GetString(3);
                    crop = reader.IsDBNull(4) ? "" : reader.GetString(4);
                    slotDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    slotStart = reader.IsDBNull(6) ? null : reader.GetTimeSpan(6);
                    slotEnd = reader.IsDBNull(7) ? null : reader.GetTimeSpan(7);
                    warehouseName = reader.IsDBNull(8) ? "—" : reader.GetString(8);
                }

                if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                    return null;

                // Some environments enforce different enum/check values.
                // Try most specific state first, then graceful fallbacks.
                var nextStatuses = new[] { "cancelled", "rejected", "completed" };
                var requestUpdated = false;
                foreach (var next in nextStatuses)
                {
                    try
                    {
                        using var cmd = new NpgsqlCommand(@"
                            UPDATE t_procurement_requests
                            SET c_status = @status, c_updated_at = NOW()
                            WHERE c_id = @id
                        ", _conn);
                        cmd.Parameters.AddWithValue("@status", next);
                        cmd.Parameters.AddWithValue("@id", requestId);
                        requestUpdated = await cmd.ExecuteNonQueryAsync() > 0;
                        if (requestUpdated) break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CancelProcurementRequest status '{next}' failed: {ex.Message}");
                    }
                }

                if (!requestUpdated)
                    return null;

                // Best-effort slot status update; do not fail request cancellation.
                if (slotId.HasValue)
                {
                    try
                    {
                        using var cmd = new NpgsqlCommand(@"
                            UPDATE t_warehouse_slot_bookings
                            SET c_check_in_status = 'cancelled'
                            WHERE c_id = @slotId
                        ", _conn);
                        cmd.Parameters.AddWithValue("@slotId", slotId.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine("CancelProcurementRequest slot check-in status update failed: " + ex1.Message);
                        try
                        {
                            using var cmd = new NpgsqlCommand(@"
                                UPDATE t_warehouse_slot_bookings
                                SET c_status = 'cancelled'
                                WHERE c_id = @slotId
                            ", _conn);
                            cmd.Parameters.AddWithValue("@slotId", slotId.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("CancelProcurementRequest slot status fallback failed: " + ex2.Message);
                        }
                    }
                }

                var slotSummary = slotDate.HasValue && slotStart.HasValue && slotEnd.HasValue
                    ? $"{slotDate.Value:MMM dd, yyyy} — {slotStart.Value:hh\\:mm} to {slotEnd.Value:hh\\:mm}"
                    : "No slot was linked to this request.";

                var slotDateDisplay = slotDate.HasValue && slotStart.HasValue && slotEnd.HasValue
                    ? $"{slotDate.Value:MMM dd, yyyy} · {slotStart.Value:hh\\:mm} – {slotEnd.Value:hh\\:mm}"
                    : "—";

                return new CancelEmailData
                {
                    ProcurementRequestId = requestId,
                    FarmerEmail = email,
                    FarmerName = name,
                    CropName = crop,
                    WarehouseName = warehouseName,
                    SlotDateDisplay = slotDateDisplay,
                    CancelledAtFormatted = DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"),
                    CancelReason = "Cancelled by Field Officer.",
                    SlotSummary = slotSummary
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("CancelProcurementRequest ERROR: " + ex.Message);
                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── FORMAT STATUS ───────────────────────────────────────────
        private string FormatStatus(string status)
        {
            return status switch
            {
                "pending"     => "Pending",
                "scheduled"   => "In Progress",
                "in_progress" => "In Progress",
                "completed"   => "Completed",
                _             => status
            };
        }

        // ── GET PROFILE ─────────────────────────────────────────────
        public async Task<vmProfile?> GetProfileAsync(int foProfileId)
        {
            await _conn.OpenAsync();
            try
            {
                var qry = @"
                    SELECT
                        fp.c_id,
                        fp.c_user_id,
                        SPLIT_PART(fp.c_full_name, ' ', 1) AS first_name,
                        SPLIT_PART(fp.c_full_name, ' ', 2) AS last_name,
                        u.c_email,
                        fp.c_phone,
                        fp.c_assigned_region,
                        u.c_profile_image_url,
                        fp.c_created_at,
                        w.c_name,
                        w.c_address
                    FROM t_field_officer_profiles fp
                    JOIN t_users u ON fp.c_user_id = u.c_id
                    JOIN t_warehouses w ON fp.c_warehouse_id = w.c_id
                    WHERE fp.c_id = @Id
                ";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("Id", foProfileId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                return new vmProfile
                {
                    Id               = reader.GetInt32(0),
                    UserId           = reader.GetInt32(1),
                    FirstName        = reader.GetString(2),
                    LastName         = reader.GetString(3),
                    Email            = reader.GetString(4),
                    Phone            = reader.IsDBNull(5) ? null : reader.GetString(5),
                    AssignedRegion   = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ProfileImageUrl  = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CreatedAt        = reader.GetDateTime(8),
                    WarehouseName    = reader.GetString(9),
                    WarehouseAddress = reader.GetString(10)
                };
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── UPDATE PROFILE ──────────────────────────────────────────
        public async Task<bool> UpdateProfileAsync(int id, vmUpdateProfile model)
        {
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    UPDATE t_field_officer_profiles
                    SET 
                        c_full_name = @FullName,
                        c_phone     = @Phone
                    WHERE c_id = @Id
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@FullName", model.FirstName + " " + model.LastName);
                cmd.Parameters.AddWithValue("@Phone",    model.Phone ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Id",       id);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── UPDATE IMAGE ────────────────────────────────────────────
        public async Task<bool> UpdateImageAsync(int id, string imageUrl)
        {
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    UPDATE t_users u
                    SET c_profile_image_url = @Url
                    FROM t_field_officer_profiles fp
                    WHERE fp.c_id = @Id
                      AND fp.c_user_id = u.c_id
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@Url", imageUrl ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Id",  id);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── CHANGE PASSWORD ─────────────────────────────────────────
        public async Task<bool> ChangePasswordAsync(string email, string password)
        {
            await _conn.OpenAsync();
            try
            {
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                var query = @"
                    UPDATE t_users
                    SET c_password_hash = @Password
                    WHERE c_email = @Email
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@Password", hashedPassword);
                cmd.Parameters.AddWithValue("@Email",    email);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                return false;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
        public async Task<List<vmWarehouseCatalog>> GetWarehouseCatalog(int foId)
        {
            var list = new List<vmWarehouseCatalog>();
            await _conn.OpenAsync();

            try
            {
                var query = @"
                    SELECT 
                        wl.c_id,
                        cp.c_name,
                        wl.c_grade,
                        wl.c_variety,
                        wl.c_quantity_remaining,
                        wl.c_unit,
                        w.c_name,
                        wl.c_status
                    FROM t_warehouse_lots wl
                    JOIN t_catalog_products cp ON wl.c_catalog_product_id = cp.c_id
                    JOIN t_warehouses w ON wl.c_warehouse_id = w.c_id
                    WHERE wl.c_fo_id = @foId
                    ORDER BY wl.c_created_at DESC
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new vmWarehouseCatalog
                    {
                        LotId       = reader.GetInt32(0),
                        ProductName = reader.GetString(1),
                        Grade       = reader.GetString(2),
                        Variety     = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Quantity    = reader.GetDecimal(4),
                        Unit        = reader.GetString(5),
                        Warehouse   = reader.GetString(6),
                        Status      = reader.GetString(7)
                    });
                }

                return list;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET INSPECTION DETAIL ────────────────────────────────────
        // Fetch completed inspection with calculated 30%/70% split
        public async Task<dynamic?> GetInspectionDetail(int procurementRequestId)
        {
            await _conn.OpenAsync();

            try
            {
                var query = @"
                    SELECT 
                        qif.c_id,
                        qif.c_procurement_request_id,
                        qif.c_fo_id,
                        qif.c_moisture_pct,
                        qif.c_foreign_matter_pct,
                        qif.c_pest_disease_observed,
                        qif.c_variety,
                        qif.c_grade,
                        qif.c_weight_checked_kg,
                        qif.c_accepted_quantity,
                        qif.c_rejected_quantity,
                        qif.c_defects_noted,
                        qif.c_remarks,
                        qif.c_passed,
                        qif.c_fo_assessed_price,
                        qif.c_submitted_at,
                        fp.c_full_name,
                        fp.c_phone,
                        fp.c_district,
                        cp.c_name,
                        pr.c_farmer_id
                    FROM t_quality_inspection_forms qif
                    JOIN t_procurement_requests pr 
                        ON qif.c_procurement_request_id = pr.c_id
                    JOIN t_farmer_profiles fp 
                        ON pr.c_farmer_id = fp.c_id
                    JOIN t_farmer_crop_listings fcl 
                        ON pr.c_crop_listing_id = fcl.c_id
                    JOIN t_catalog_products cp 
                        ON fcl.c_catalog_product_id = cp.c_id
                    WHERE pr.c_id = @procurementRequestId
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@procurementRequestId", procurementRequestId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                decimal acceptedQty = reader.GetDecimal(9);
                decimal foPrice = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14);
                decimal totalValue = acceptedQty * foPrice;
                decimal advanceAmount = Math.Round(totalValue * 0.30m, 2);
                decimal remainingAmount = Math.Round(totalValue * 0.70m, 2);

                return new
                {
                    inspectionId = reader.GetInt32(0),
                    procurementRequestId = reader.GetInt32(1),
                    foId = reader.GetInt32(2),
                    moisturePct = reader.GetDecimal(3),
                    foreignMatterPct = reader.GetDecimal(4),
                    pestDisease = reader.IsDBNull(5) ? null : reader.GetString(5),
                    variety = reader.IsDBNull(6) ? null : reader.GetString(6),
                    grade = reader.GetString(7),
                    weightChecked = reader.GetDecimal(8),
                    acceptedQuantity = acceptedQty,
                    rejectedQuantity = reader.GetDecimal(10),
                    defectsNoted = reader.IsDBNull(11) ? null : reader.GetString(11),
                    remarks = reader.IsDBNull(12) ? null : reader.GetString(12),
                    passed = reader.GetBoolean(13),
                    foAssessedPrice = foPrice,
                    submittedAt = reader.GetDateTime(15),
                    farmerName = reader.GetString(16),
                    farmerPhone = reader.IsDBNull(17) ? null : reader.GetString(17),
                    farmerDistrict = reader.IsDBNull(18) ? null : reader.GetString(18),
                    cropName = reader.GetString(19),
                    farmerId = reader.GetInt32(20),
                    
                    // Calculated amounts
                    totalValue = totalValue,
                    advanceAmount = advanceAmount,
                    remainingAmount = remainingAmount
                };
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── REQUEST PAYMENT ──────────────────────────────────────────
        // Create payment request + notification to admin
        public async Task<bool> RequestPayment(int inspectionId, int farmerId, int procurementRequestId, decimal advanceAmount)
        {
            await _conn.OpenAsync();
            using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                // STEP 1: Insert into t_payment_requests
                var insertPaymentQuery = @"
                    INSERT INTO t_payment_requests (
                        c_farmer_id,
                        c_procurement_request_id,
                        c_amount,
                        c_status,
                        c_requested_at
                    ) VALUES (
                        @farmerId,
                        @procurementRequestId,
                        @amount,
                        'pending',
                        NOW()
                    ) RETURNING c_id
                ";

                int paymentRequestId = 0;

                using (var cmd = new NpgsqlCommand(insertPaymentQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@farmerId", farmerId);
                    cmd.Parameters.AddWithValue("@procurementRequestId", procurementRequestId);
                    cmd.Parameters.AddWithValue("@amount", advanceAmount);

                    var result = await cmd.ExecuteScalarAsync();
                    paymentRequestId = result != null ? (int)result : 0;
                }

                // STEP 2: Insert notification to admin (notify all admins)
                var insertNotificationQuery = @"
                    INSERT INTO t_notifications (
                        c_user_id,
                        c_type,
                        c_title,
                        c_body,
                        c_reference_id,
                        c_is_read,
                        c_created_at
                    ) 
                    SELECT 
                        u.c_id,
                        'payment_request',
                        'Payment Request Pending',
                        'FO has requested 30% advance payment (₹' || @amount || ') for inspection',
                        @paymentRequestId,
                        false,
                        NOW()
                    FROM t_users u
                    WHERE u.c_role = 'admin'
                ";

                using (var cmd = new NpgsqlCommand(insertNotificationQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@amount", advanceAmount);
                    cmd.Parameters.AddWithValue("@paymentRequestId", paymentRequestId);

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("RequestPayment ERROR: " + ex.Message);
                return false;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
        public async Task<byte[]> GenerateInspectionPdfAsync(int procurementRequestId)
        {
            var data = await GetInspectionDetail(procurementRequestId);

            if (data == null)
                throw new Exception("Inspection not found");

            var pdfService = new PdfService();

            return pdfService.GenerateInspectionPdf(data);
        }

        // ══════════════════════════════════════════════════════════════
        // ──             PAYMENT METHODS (Integrated)                 ──
        // ══════════════════════════════════════════════════════════════

        // ── GENERATE UTR REFERENCE ─────────────────────────────────────
        /// <summary>Generate unique UTR reference for transaction</summary>
        private string GenerateUtrReference()
        {
            // Format: TDS-{Timestamp}-{Random}
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string random = new Random().Next(1000, 9999).ToString();
            return $"TDS-{timestamp}-{random}";
        }

        // ── PROCESS ADVANCE PAYMENT (30%) ──────────────────────────────
        /// <summary>Automatically process 30% advance payment when inspection passes</summary>
        public async Task<dynamic?> ProcessAdvancePayment(
            int procurementRequestId,
            int farmerId,
            int inspectionId,
            decimal advanceAmount)
        {
            await _conn.OpenAsync();
            using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                // Check if payment already exists
                var checkQuery = @"
                    SELECT c_id, c_status FROM t_payments_farmer
                    WHERE c_procurement_request_id = @procReqId
                      AND c_payment_number = 1
                    LIMIT 1
                ";

                using (var cmd = new NpgsqlCommand(checkQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@procReqId", procurementRequestId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var existingId = reader.GetInt32(0);
                        var status = reader.GetString(1);
                        return new
                        {
                            success = false,
                            message = $"Payment already exists with status: {status}",
                            paymentId = existingId,
                            status = status
                        };
                    }
                }

                // Create payment record
                string utrRef = GenerateUtrReference();

                var insertQuery = @"
                    INSERT INTO t_payments_farmer (
                        c_farmer_id,
                        c_payment_request_id,
                        c_procurement_request_id,
                        c_lot_id,
                        c_amount,
                        c_payment_number,
                        c_trigger_event,
                        c_payment_mode,
                        c_utr_reference,
                        c_status,
                        c_created_at
                    ) VALUES (
                        @farmerId,
                        @paymentRequestId,
                        @procReqId,
                        @lotId,
                        @amount,
                        1,
                        'qc_passed',
                        'bank_transfer',
                        @utrRef,
                        'success',
                        NOW()
                    ) RETURNING c_id, c_utr_reference, c_created_at
                ";

                int paymentId = 0;
                string utrid = "";
                DateTime createdAt = DateTime.UtcNow;

                using (var cmd = new NpgsqlCommand(insertQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@farmerId", farmerId);
                    cmd.Parameters.AddWithValue("@paymentRequestId", DBNull.Value);
                    cmd.Parameters.AddWithValue("@procReqId", procurementRequestId);
                    cmd.Parameters.AddWithValue("@lotId", DBNull.Value);
                    cmd.Parameters.AddWithValue("@amount", advanceAmount);
                    cmd.Parameters.AddWithValue("@utrRef", utrRef);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        paymentId = reader.GetInt32(0);
                        utrid = reader.GetString(1);
                        createdAt = reader.GetDateTime(2);
                    }
                }

                await transaction.CommitAsync();

                return new
                {
                    success = true,
                    message = "Payment processed successfully",
                    paymentId = paymentId,
                    utrReference = utrid,
                    amount = advanceAmount,
                    status = "success",
                    createdAt = createdAt
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"ProcessAdvancePayment ERROR: {ex.Message}");
                return new
                {
                    success = false,
                    message = $"Payment failed: {ex.Message}",
                    error = ex.Message
                };
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET PAYMENT HISTORY ────────────────────────────────────────
        /// <summary>Get all payments for a farmer</summary>
        public async Task<List<dynamic>> GetFarmerPaymentHistory(int farmerId)
        {
            var payments = new List<dynamic>();
            await _conn.OpenAsync();

            try
            {
                var query = @"
                    SELECT 
                        pf.c_id,
                        pf.c_utr_reference,
                        pf.c_amount,
                        pf.c_payment_number,
                        pf.c_trigger_event,
                        pf.c_status,
                        pf.c_created_at,
                        pf.c_settled_at,
                        cp.c_name as crop_name,
                        pr.c_requested_quantity,
                        pr.c_unit
                    FROM t_payments_farmer pf
                    LEFT JOIN t_procurement_requests pr 
                        ON pf.c_procurement_request_id = pr.c_id
                    LEFT JOIN t_farmer_crop_listings fcl 
                        ON pr.c_crop_listing_id = fcl.c_id
                    LEFT JOIN t_catalog_products cp 
                        ON fcl.c_catalog_product_id = cp.c_id
                    WHERE pf.c_farmer_id = @farmerId
                    ORDER BY pf.c_created_at DESC
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@farmerId", farmerId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    payments.Add(new
                    {
                        paymentId = reader.GetInt32(0),
                        utrReference = reader.GetString(1),
                        amount = reader.GetDecimal(2),
                        paymentNumber = reader.GetInt32(3),
                        triggerEvent = reader.GetString(4),
                        status = reader.GetString(5),
                        createdAt = reader.GetDateTime(6),
                        settledAt = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                        cropName = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                        quantity = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                        unit = reader.IsDBNull(10) ? "kg" : reader.GetString(10)
                    });
                }

                return payments;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET FO PAYMENT SUMMARY ──────────────────────────────────────
        /// <summary>Get all farmer payments triggered from procurement requests assigned to this FO.</summary>
        public async Task<List<dynamic>> GetFoPaymentSummary(int foId)
        {
            var payments = new List<dynamic>();
            await _conn.OpenAsync();

            try
            {
                var query = @"
                    SELECT
                        pf.c_id,
                        pf.c_utr_reference,
                        pf.c_amount,
                        pf.c_payment_number,
                        pf.c_status,
                        pf.c_created_at,
                        pf.c_settled_at,
                        cp.c_name AS crop_name
                    FROM t_payments_farmer pf
                    INNER JOIN t_procurement_requests pr
                        ON pf.c_procurement_request_id = pr.c_id
                    LEFT JOIN t_farmer_crop_listings fcl
                        ON pr.c_crop_listing_id = fcl.c_id
                    LEFT JOIN t_catalog_products cp
                        ON fcl.c_catalog_product_id = cp.c_id
                    WHERE pr.c_assigned_fo_id = @foId
                    ORDER BY pf.c_created_at DESC
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    payments.Add(new
                    {
                        paymentId = reader.GetInt32(0),
                        utrReference = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                        amount = reader.GetDecimal(2),
                        paymentNumber = reader.GetInt32(3),
                        status = reader.IsDBNull(4) ? "initiated" : reader.GetString(4),
                        createdAt = reader.GetDateTime(5),
                        settledAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                        cropName = reader.IsDBNull(7) ? "N/A" : reader.GetString(7)
                    });
                }

                return payments;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── GET PAYMENT BY ID ──────────────────────────────────────────
        /// <summary>Get specific payment details</summary>
        public async Task<dynamic?> GetPaymentDetail(int paymentId)
        {
            await _conn.OpenAsync();

            try
            {
                var query = @"
                    SELECT 
                        pf.c_id,
                        pf.c_farmer_id,
                        pf.c_utr_reference,
                        pf.c_amount,
                        pf.c_payment_number,
                        pf.c_trigger_event,
                        pf.c_payment_mode,
                        pf.c_status,
                        pf.c_created_at,
                        pf.c_settled_at,
                        fp.c_full_name,
                        fp.c_phone,
                        pr.c_id,
                        cp.c_name
                    FROM t_payments_farmer pf
                    LEFT JOIN t_farmer_profiles fp ON pf.c_farmer_id = fp.c_id
                    LEFT JOIN t_procurement_requests pr ON pf.c_procurement_request_id = pr.c_id
                    LEFT JOIN t_farmer_crop_listings fcl ON pr.c_crop_listing_id = fcl.c_id
                    LEFT JOIN t_catalog_products cp ON fcl.c_catalog_product_id = cp.c_id
                    WHERE pf.c_id = @paymentId
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@paymentId", paymentId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new
                    {
                        paymentId = reader.GetInt32(0),
                        farmerId = reader.GetInt32(1),
                        utrReference = reader.GetString(2),
                        amount = reader.GetDecimal(3),
                        paymentNumber = reader.GetInt32(4),
                        triggerEvent = reader.GetString(5),
                        paymentMode = reader.GetString(6),
                        status = reader.GetString(7),
                        createdAt = reader.GetDateTime(8),
                        settledAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                        farmerName = reader.IsDBNull(10) ? "N/A" : reader.GetString(10),
                        farmerPhone = reader.IsDBNull(11) ? "N/A" : reader.GetString(11),
                        procurementRequestId = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                        cropName = reader.IsDBNull(13) ? "N/A" : reader.GetString(13)
                    };
                }

                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        // ── CHECK IF PAYMENT ALREADY DONE ──────────────────────────────
        /// <summary>Check if advance payment (payment #1) already exists for this procurement</summary>
        public async Task<bool> HasAdvancePayment(int procurementRequestId)
        {
            await _conn.OpenAsync();

            try
            {
                var query = @"
                    SELECT COUNT(*) FROM t_payments_farmer
                    WHERE c_procurement_request_id = @procReqId
                      AND c_payment_number = 1
                      AND c_status = 'success'
                ";

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@procReqId", procurementRequestId);

                var result = await cmd.ExecuteScalarAsync();
                return result != null && (long)result > 0;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
        public async Task<List<object>> GetNotificationsAsync(int foId)
        {
            var list = new List<object>();
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    SELECT c_id, c_title, c_message, c_is_read, c_redirect_url, c_created_at
                    FROM t_notifications
                    WHERE c_recipient_id = @foId AND c_recipient_type = 'field_officer'
                    ORDER BY c_created_at DESC
                    LIMIT 50
                ";
                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        id          = reader.GetInt32(0),
                        title       = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        message     = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        isRead      = reader.GetBoolean(3),
                        redirectUrl = reader.IsDBNull(4) ? "#" : reader.GetString(4),
                        createdAt   = reader.GetDateTime(5)
                    });
                }
                return list;
            }
            finally { await _conn.CloseAsync(); }
        }

        public async Task MarkAllNotificationsReadAsync(int foId)
        {
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    UPDATE t_notifications 
                    SET c_is_read = true 
                    WHERE c_recipient_id = @foId AND c_recipient_type = 'field_officer'
                ";
                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);
                await cmd.ExecuteNonQueryAsync();
            }
            finally { await _conn.CloseAsync(); }
        }

        public async Task ClearAllNotificationsAsync(int foId)
        {
            await _conn.OpenAsync();
            try
            {
                var query = @"
                    DELETE FROM t_notifications 
                    WHERE c_recipient_id = @foId AND c_recipient_type = 'field_officer'
                ";
                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@foId", foId);
                await cmd.ExecuteNonQueryAsync();
            }
            finally { await _conn.CloseAsync(); }
        }
    }
}