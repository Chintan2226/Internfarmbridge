using API.BAL;
using API.Models.FarmerApp;
using API.Models.Farmer;
using API.Models.Settings;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using API.Models.FieldOfficer;
using Microsoft.AspNetCore.Http;

namespace API.Controllers
{
    [Route("api/FarmerApp")]
    [ApiController]
    [Authorize(Roles = "farmer")]
    public class FarmerAppController : ControllerBase
    {
        private readonly FarmerAppHelper _helper;
        private readonly EmailService _emailService;
        private readonly ElasticService _elasticService;
        private readonly RabbitMqService _rabbitMqService;
        private readonly CloudinaryService _cloudinaryService;
        private readonly RedisService _redisService;

        public FarmerAppController(IConfiguration configuration,
            EmailService emailService,
            ElasticService elasticService,
            RabbitMqService rabbitMqService,
            CloudinaryService cloudinaryService,
            RedisService redisService)
        {
            _helper = new FarmerAppHelper(configuration);
            _emailService = emailService;
            _elasticService = elasticService;
            _rabbitMqService = rabbitMqService;
            _cloudinaryService = cloudinaryService;
            _redisService = redisService;
        }

        private int GetTokenFarmerId()
        {
            var claim = User.FindFirst("farmer_id")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        private bool FarmerOwns(int farmerId)
            => GetTokenFarmerId() == farmerId;

        // Slot Booking
        [HttpPost("slots/book")]
        public async Task<IActionResult> BookSlot([FromBody] vm_BookQcSlotRequest req)
        {
            // if (!FarmerOwns(req.FarmerId)) return Forbid();
            var farmerId = int.Parse(User.FindFirst("farmer_id")?.Value);
            req.FarmerId = farmerId;

            bool success = await _helper.BookQcSlotAsync(req);
            var profile = await _helper.GetFarmerProfileAsync(req.FarmerId);

            if (success)
            {
                try
                {

                    if (profile != null && !string.IsNullOrEmpty(profile.Email))
                    {
                        var emailData = new AcceptEmailData
                        {
                            FarmerEmail = profile.Email,
                            FarmerName = profile.FullName ?? "Farmer",
                            ProcurementRequestId = 0,
                            CropName = "Your Listed Crop",
                            WarehouseName = "Assigned Warehouse",
                            QuantityDisplay = "Requested Quantity",
                            SlotDateFormatted = DateTime.Now.ToString("MMM dd, yyyy"),
                            TimeRange = "Standard Business Hours",
                            AcceptedAtFormatted = DateTime.Now.ToString("MMM dd, yyyy")
                        };

                        await _emailService.SendFarmerRequestAcceptedEmailAsync(emailData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to send booking email: " + ex.Message);
                }

                await _rabbitMqService.PublishToRoleAsync("admin",
                    "New QC Booking Request",
                    $"Farmer has requested a QC inspection.",
                    "qc_booking");

                int foUserId = await _helper.GetFieldOfficerUserIdByWarehouseAsync(req.WarehouseId);
                if (foUserId > 0)
                {
                    await _rabbitMqService.PublishToUserAsync(foUserId,
                        "New QC Booking Request",
                        $"Farmer {profile.FullName} has requested  a QC inspection at your warehouse.",
                        "qc_booking");
                }

                return Ok(new { success = true, message = "QC Slot booked successfully! You will receive an email confirmation." });
            }

            return StatusCode(500, new { success = false, message = "Failed to book slot." });
        }

        // Internal Notifications
        [HttpPost("internal/send-qc-slot-accepted-notification")]
        [Authorize(Roles = "admin,fo")]
        public async Task<IActionResult> SendQCSlotAcceptedNotification([FromBody] InternalSubstitutionsRequest request)
        {
            try
            {
                var profile = await _helper.GetFarmerProfileAsync(request.FarmerId);
                if (profile == null || string.IsNullOrEmpty(profile.Email))
                    return BadRequest(new { success = false, message = "Farmer email not found." });

                var data = new AcceptEmailData
                {
                    FarmerEmail = profile.Email,
                    FarmerName = request.SubstitutionData.GetValueOrDefault("{{FARMER_NAME}}", "Farmer"),
                    ProcurementRequestId = Convert.ToInt32(request.SubstitutionData.GetValueOrDefault("{{QC_REQUEST_ID}}", "0")),
                    CropName = request.SubstitutionData.GetValueOrDefault("{{CROP_NAME}}", "Crop"),
                    WarehouseName = request.SubstitutionData.GetValueOrDefault("{{WAREHOUSE_NAME}}", "Warehouse"),
                    QuantityDisplay = request.SubstitutionData.GetValueOrDefault("{{QUANTITY}}", ""),
                    SlotDateFormatted = request.SubstitutionData.GetValueOrDefault("{{SLOT_DATE}}", ""),
                    TimeRange = request.SubstitutionData.GetValueOrDefault("{{TIME_RANGE}}", ""),
                    AcceptedAtFormatted = request.SubstitutionData.GetValueOrDefault("{{ACCEPTED_AT}}", DateTime.Now.ToString("MMM dd, yyyy"))
                };

                await _emailService.SendFarmerRequestAcceptedEmailAsync(data);

                await _rabbitMqService.PublishToUserAsync(request.FarmerId,
                    "QC Request Accepted ✅",
                    $"Your QC request has been accepted by Field Officer. Your slot is confirmed for {data.SlotDateFormatted}",
                    "qc_request");

                return Ok(new { success = true, message = $"Accepted email sent to {profile.Email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("internal/send-slot-rescheduled-notification")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SendSlotRescheduledNotification([FromBody] InternalSubstitutionsRequest request)
        {
            try
            {
                var profile = await _helper.GetFarmerProfileAsync(request.FarmerId);
                if (profile == null || string.IsNullOrEmpty(profile.Email))
                    return BadRequest(new { success = false, message = "Farmer email not found." });

                var data = new RescheduleEmailData
                {
                    FarmerEmail = profile.Email,
                    FarmerName = request.SubstitutionData.GetValueOrDefault("{{FARMER_NAME}}", "Farmer"),
                    ProcurementRequestId = Convert.ToInt32(request.SubstitutionData.GetValueOrDefault("{{REQUEST_ID}}", "0")),
                    CropName = request.SubstitutionData.GetValueOrDefault("{{CROP_NAME}}", "Crop"),
                    WarehouseName = request.SubstitutionData.GetValueOrDefault("{{WAREHOUSE_NAME}}", "Warehouse"),
                    OldSlotDateFormatted = request.SubstitutionData.GetValueOrDefault("{{OLD_SLOT_DATE}}", ""),
                    OldTimeRange = request.SubstitutionData.GetValueOrDefault("{{OLD_TIME_RANGE}}", ""),
                    NewSlotDateFormatted = request.SubstitutionData.GetValueOrDefault("{{NEW_SLOT_DATE}}", ""),
                    NewTimeRange = request.SubstitutionData.GetValueOrDefault("{{NEW_TIME_RANGE}}", "")
                };

                await _emailService.SendFarmerSlotRescheduledEmailAsync(data);

                await _rabbitMqService.PublishToUserAsync(request.FarmerId,
                    "QC Slot Rescheduled 📅",
                    $"Your QC slot has been rescheduled to {data.NewSlotDateFormatted} at {data.NewTimeRange}.",
                    "qc_request");

                return Ok(new { success = true, message = $"Reschedule email sent to {profile.Email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("internal/send-advance-payment-notification")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SendAdvancePaymentNotification([FromBody] InternalAdvancePaymentRequest request)
        {
            try
            {
                var profile = await _helper.GetFarmerProfileAsync(request.FarmerId);
                if (profile == null || string.IsNullOrEmpty(profile.Email))
                    return BadRequest(new { success = false, message = "Farmer email not found." });

                await _emailService.SendFarmerAdvancePaymentEmailAsync(
                    profile.Email, request.FarmerName, request.CropName, request.TotalAmount,
                    request.PaidAmount, request.RemainingAmount, request.UtrReference, request.PaymentId
                );

                await _rabbitMqService.PublishToUserAsync(request.FarmerId,
                    "Advance Payment Processed 💰",
                    $"Your advance payment of ₹{request.PaidAmount:N2} has been processed. UTR: {request.UtrReference}",
                    "payment");

                await _rabbitMqService.PublishToRoleAsync("admin",
                    "Advance Payment Processed",
                    $"Advance payment of ₹{request.PaidAmount:N2} has been processed for farmer.",
                    "payment");

                return Ok(new { success = true, message = $"Payment email sent to {profile.Email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("internal/send-slot-cancelled-notification")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SendSlotCancelledNotification([FromBody] InternalSubstitutionsRequest request)
        {
            try
            {
                var profile = await _helper.GetFarmerProfileAsync(request.FarmerId);
                if (profile == null || string.IsNullOrEmpty(profile.Email))
                    return BadRequest(new { success = false, message = "Farmer email not found." });

                var data = new CancelEmailData
                {
                    FarmerEmail = profile.Email,
                    FarmerName = request.SubstitutionData.GetValueOrDefault("{{FARMER_NAME}}", "Farmer"),
                    ProcurementRequestId = Convert.ToInt32(request.SubstitutionData.GetValueOrDefault("{{BOOKING_ID}}", "0")),
                    CropName = request.SubstitutionData.GetValueOrDefault("{{CROP_NAME}}", "Crop"),
                    WarehouseName = request.SubstitutionData.GetValueOrDefault("{{WAREHOUSE_NAME}}", "Warehouse"),
                    SlotDateDisplay = request.SubstitutionData.GetValueOrDefault("{{SLOT_DATE}}", ""),
                    CancelledAtFormatted = request.SubstitutionData.GetValueOrDefault("{{CANCELLED_AT}}", DateTime.Now.ToString("MMM dd, yyyy")),
                    CancelReason = request.SubstitutionData.GetValueOrDefault("{{CANCEL_REASON}}", "")
                };

                await _emailService.SendFarmerRequestCancelledEmailAsync(data);

                await _rabbitMqService.PublishToUserAsync(request.FarmerId,
                    "QC Request Cancelled ❌",
                    $"Your QC request has been cancelled. Reason: {data.CancelReason ?? "No reason provided"}",
                    "qc_request");

                await _rabbitMqService.PublishToRoleAsync("admin",
                    "QC Request Cancelled",
                    $"A QC request for farmer {data.FarmerName} has been cancelled.",
                    "qc_request");

                return Ok(new { success = true, message = $"Cancel email sent to {profile.Email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Dashboard & Listings
        [HttpGet("{farmerId}/dashboard")]
        public async Task<IActionResult> GetDashboard([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            
            string cacheKey = $"farmer:dashboard:{farmerId}";
            var cachedData = await _redisService.GetAsync<vm_FarmerDashboard>(cacheKey);
            if (cachedData != null)
                return Ok(new { success = true, data = cachedData, source = "cache" });

            var data = await _helper.GetDashboardDataAsync(farmerId);
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new { success = true, data, source = "db" });
        }

        [HttpDelete("{farmerId}/crop/{listingId}")]
        public async Task<IActionResult> DeleteCropListing([FromRoute] int farmerId, [FromRoute] int listingId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var success = await _helper.DeleteCropListingAsync(farmerId, listingId);
            if (!success) return NotFound(new { success = false, message = "Crop listing not found or cannot be deleted" });

            await _rabbitMqService.PublishToUserAsync(farmerId,
                "Crop Listing Deleted",
                "Your crop listing has been deleted successfully.",
                "crop_listing");

            return Ok(new { success = true, message = "Crop listing deleted successfully" });
        }

        [HttpPost("listings")]
        public async Task<IActionResult> SaveListing([FromBody] vm_CropListingRequest req)
        {
            if (req == null) return BadRequest(new { success = false, message = "Invalid request payload." });

            req.FarmerId = GetTokenFarmerId();
            if (req.FarmerId == 0) return Forbid();

            bool success = await _helper.SaveCropListingAsync(req);

            if (success)
            {
                if (req.ListingId == null || req.ListingId == 0)
                {
                    if (!req.IsDraft)
                    {
                        await _rabbitMqService.PublishToRoleAsync("admin",
                            "New Crop Listed",
                            $"Farmer has listed a new crop for sale.",
                            "crop_listing");
                    }

                    await _rabbitMqService.PublishToUserAsync(req.FarmerId,
                        req.IsDraft ? "Crop Listing Saved as Draft" : "Crop Listing Published",
                        req.IsDraft
                            ? "Your crop listing has been saved as draft."
                            : "Your crop listing has been published successfully.",
                        "crop_listing");
                }
                else
                {
                    await _rabbitMqService.PublishToUserAsync(req.FarmerId,
                        "Crop Listing Updated",
                        "Your crop listing has been updated successfully.",
                        "crop_listing");
                }
            }

            if (success) return Ok(new { success = true, message = req.IsDraft ? "Draft saved successfully." : "Crop listing published." });
            return BadRequest(new { success = false, message = "Failed to save listing. Cannot edit confirmed QC slots." });
        }

        [HttpGet("warehouses/{warehouseId}/slots")]
        public async Task<IActionResult> GetSlots([FromRoute] int warehouseId)
        {
            var slots = await _helper.GetAvailableQcSlotsAsync(warehouseId);
            return Ok(new { success = true, data = slots });
        }

        // Payments & Finance
        [HttpGet("{farmerId}/payments")]
        public async Task<IActionResult> GetPayments([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var payments = await _helper.GetPaymentHistoryAsync(farmerId);
            return Ok(new { success = true, data = payments });
        }

        [HttpGet("{farmerId:int}/income-chart")]
        public async Task<IActionResult> GetIncomeChart([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();

            string cacheKey = $"farmer:incomechart:{farmerId}";
            var cachedData = await _redisService.GetAsync<List<vm_IncomeChartPoint>>(cacheKey);
            if (cachedData != null)
                return Ok(new { success = true, data = cachedData, source = "cache" });

            var data = await _helper.GetIncomeChartAsync(farmerId);
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new { success = true, data, source = "db" });
        }

        // Dropdowns
        [HttpGet("dropdowns/catalog")]
        public async Task<IActionResult> GetCatalogDropdown()
        {
            string cacheKey = "dropdown:catalog";
            var cachedData = await _redisService.GetAsync<List<vm_DropdownItem>>(cacheKey);
            if (cachedData != null)
                return Ok(new { success = true, data = cachedData, source = "cache" });

            var data = await _helper.GetCatalogDropdownAsync();
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new { success = true, data, source = "db" });
        }

        [HttpGet("dropdowns/warehouses")]
        public async Task<IActionResult> GetWarehousesDropdown()
        {
            // string cacheKey = "dropdown:warehouses";
            // var cachedData = await _redisService.GetAsync<List<vm_DropdownItem>>(cacheKey);
            // if (cachedData != null)
            //     return Ok(new Dictionary<string, object> { { "success", true }, { "data", cachedData }, { "source", "cache" } });

            var data = await _helper.GetWarehousesDropdownAsync();
            // await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new Dictionary<string, object> { { "success", true }, { "data", data }, { "source", "db" } });
        }

        [HttpGet("{farmerId:int}/dropdowns/active-listings")]
        public async Task<IActionResult> GetActiveListingsDropdown([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            return Ok(new Dictionary<string, object> { { "success", true }, { "data", await _helper.GetActiveListingsDropdownAsync(farmerId) } });
        }

        // Profile & QC
        [HttpGet("{farmerId:int}/listings")]
        public async Task<IActionResult> GetListings([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetFarmerListingsAsync(farmerId);
            return Ok(new Dictionary<string, object> { { "success", true }, { "data", data } });
        }

        [HttpGet("{farmerId:int}/qc-dashboard")]
        public async Task<IActionResult> GetQcDashboard([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();

            string cacheKey = $"farmer:qcdashboard:{farmerId}";
            var cachedData = await _redisService.GetAsync<vm_QcDashboard>(cacheKey);
            if (cachedData != null)
                return Ok(new Dictionary<string, object> { { "success", true }, { "data", cachedData }, { "source", "cache" } });

            var data = await _helper.GetQcDashboardAsync(farmerId);
            await _redisService.SetAsync(cacheKey, data, TimeSpan.FromMinutes(60));
            return Ok(new Dictionary<string, object> { { "success", true }, { "data", data }, { "source", "db" } });
        }

        // Inquiries
        [HttpGet("{farmerId:int}/inquiries")]
        public async Task<IActionResult> GetInquiries([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetInquiriesAsync(farmerId);
            return Ok(new Dictionary<string, object> { { "success", true }, { "data", data } });
        }

        [HttpPost("inquiries/submit")]
        public async Task<IActionResult> SubmitInquiry([FromBody] vm_SubmitInquiryRequest req)
        {
            if (req == null) return BadRequest(new { success = false, message = "Invalid request payload." });

            req.FarmerId = GetTokenFarmerId();
            if (req.FarmerId == 0) return Forbid();

            bool success = await _helper.SubmitInquiryAsync(req);

            if (success)
            {
                await _rabbitMqService.PublishToRoleAsync("admin",
                    $"New {req.Department} Inquiry",
                    $"Farmer has submitted a {req.Subject} inquiry.",
                    "inquiry");

                await _rabbitMqService.PublishToUserAsync(req.FarmerId,
                    "Inquiry Submitted",
                    "Your inquiry has been submitted. Support team will respond within 24 hours.",
                    "inquiry");
            }

            if (success) return Ok(new Dictionary<string, object> { { "success", true }, { "message", "Inquiry submitted successfully." } });
            return StatusCode(500, new Dictionary<string, object> { { "success", false }, { "message", "Failed to submit inquiry." } });
        }

        [HttpGet("{farmerId:int}/profile")]
        public async Task<IActionResult> GetProfile([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetFarmerProfileAsync(farmerId);
            return Ok(new Dictionary<string, object> { { "success", true }, { "data", data } });
        }

        [HttpPut("profile/update")]
        public async Task<IActionResult> UpdateProfile([FromBody] vm_UpdateProfileRequest req)
        {
            if (req == null) return BadRequest(new { success = false, message = "Invalid request payload." });

            req.FarmerId = GetTokenFarmerId();
            if (req.FarmerId == 0) return Forbid();

            bool success = await _helper.UpdateFarmerProfileAsync(req);

            if (success)
            {
                await _rabbitMqService.PublishToUserAsync(req.FarmerId,
                    "Profile Updated",
                    "Your profile has been updated successfully.",
                    "profile");
            }

            if (success) return Ok(new Dictionary<string, object> { { "success", true }, { "message", "Profile updated successfully." } });
            return StatusCode(500, new Dictionary<string, object> { { "success", false }, { "message", "Failed to update profile." } });
        }

        [HttpPost("profile/photo/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPhoto([FromForm] PhotoUploadDto dto)
        {
            if (dto.File == null || dto.File.Length == 0) return BadRequest("No file uploaded.");
            var result = await _cloudinaryService.UploadImageAsync(dto.File, "farmbridge/profiles");
            if (result.Success)
                return Ok(new Dictionary<string, object> { { "success", true }, { "imageUrl", result.SecureUrl } });

            return StatusCode(500, new Dictionary<string, object> { { "success", false }, { "message", result.Error ?? "Unknown error" } });
        }

        [HttpDelete("profile/photo/delete")]
        public async Task<IActionResult> DeletePhoto([FromQuery] string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return BadRequest("No image URL provided.");
            var publicId = _cloudinaryService.ExtractPublicId(imageUrl);
            if (string.IsNullOrEmpty(publicId)) return BadRequest("Invalid image URL.");
            bool success = await _cloudinaryService.DeleteImageAsync(publicId);
            return Ok(new Dictionary<string, object> { { "success", success } });
        }

        // In API/Controllers/FarmerAppController.cs
        [HttpPost("search/my-crops")]
        public async Task<IActionResult> SearchMyCrops([FromBody] SearchRequestModel request)
        {
            try
            {
                var farmerId = GetTokenFarmerId();
                request.FarmerId = farmerId;
                var results = await _elasticService.SearchCropsForMVCAsync(request);
                return Ok(results);  // This returns proper JSON
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchMyCrops error: {ex.Message}");
                return Ok(new SearchResponseModel<CropSearchResult>());  // Return empty but valid JSON
            }
        }
        [HttpPost("reindex-crops")]
        [AllowAnonymous] // ya admin only
        public async Task<IActionResult> ReindexCrops()
        {
            var result = await _elasticService.ReindexAllAsync();
            return Ok(new
            {
                success = true,
                cropListings = result.CropListings
            });
        }
    }

    public class PhotoUploadDto
    {
        public IFormFile File { get; set; }
    }

    public class InternalSubstitutionsRequest
    {
        public int FarmerId { get; set; }
        public Dictionary<string, string> SubstitutionData { get; set; } = new Dictionary<string, string>();
    }

    public class InternalAdvancePaymentRequest
    {
        public int FarmerId { get; set; }
        public string FarmerName { get; set; } = string.Empty;
        public string CropName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? UtrReference { get; set; }
        public int PaymentId { get; set; }
    }
}