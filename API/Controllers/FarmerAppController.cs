using API.BAL;
using API.Models.FarmerApp;
using API.Models.Farmer;
using API.Models.Settings;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using API.Models.FieldOfficer; // ✅ This pulls in the correct Email Data models automatically

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

        public FarmerAppController(IConfiguration configuration,
            EmailService emailService,
            ElasticService elasticService)
        {
            _helper = new FarmerAppHelper(configuration);
            _emailService = emailService;
            _elasticService = elasticService;
        }

        private int GetTokenFarmerId()
        {
            var claim = User.FindFirst("farmer_id")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        private bool FarmerOwns(int farmerId)
            => GetTokenFarmerId() == farmerId;


        // =============================================================
        // SECTION 1: FARMER-INITIATED ACTIONS (WITH EMAIL LOGIC!)
        // =============================================================

        [AllowAnonymous]
        // [HttpPost("google-login")]
        // public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthRequest request)
        // {
        //     var (farmer, isNewUser) = await _farmerHelper.GoogleLoginAsync(request);

        //     if (farmer != null)
        //     {
        //         // ✅ TRIGGER EMAIL ONLY IF THEY ARE A NEW USER
        //         if (isNewUser)
        //         {
        //             await _emailService.SendFarmerWelcomeEmailAsync(request.Email, request.Name);
        //         }

        //         var token = _jwtService.GenerateJwtToken(farmer.UserId, request.Email, "farmer");
        //         return Ok(new { success = true, token = token });
        //     }

        //     return Unauthorized(new { message = "Authentication failed" });
        // }



        [HttpPost("slots/book")]
        public async Task<IActionResult> BookSlot([FromBody] vm_BookQcSlotRequest req)
        {
            if (!FarmerOwns(req.FarmerId)) return Forbid();

            bool success = await _helper.BookQcSlotAsync(req);

            if (success)
            {
                try
                {
                    var profile = await _helper.GetFarmerProfileAsync(req.FarmerId);

                    if (profile != null && !string.IsNullOrEmpty(profile.Email))
                    {
                        var emailData = new AcceptEmailData
                        {
                            FarmerEmail = profile.Email,
                            FarmerName = profile.FullName ?? "Farmer",
                            ProcurementRequestId = 0, // Fallback ID
                            CropName = "Your Listed Crop",
                            WarehouseName = "Assigned Warehouse",
                            QuantityDisplay = "Requested Quantity",
                            SlotDateFormatted = DateTime.Now.ToString("MMM dd, yyyy"), // Safe Fallback
                            TimeRange = "Standard Business Hours", // Safe fallback
                            AcceptedAtFormatted = DateTime.Now.ToString("MMM dd, yyyy")
                        };

                        await _emailService.SendFarmerRequestAcceptedEmailAsync(emailData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to send booking email: " + ex.Message);
                }

                return Ok(new { success = true, message = "QC Slot booked successfully! You will receive an email confirmation." });
            }

            return StatusCode(500, new { success = false, message = "Failed to book slot." });
        }


        // =============================================================
        // SECTION 2: INTER-SERVICE REMOTE CALL TRIGGERS 
        // =============================================================

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
                return Ok(new { success = true, message = $"Cancel email sent to {profile.Email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        // =============================================================
        // SECTION 3: EXISTING FARMER ACTIONS (UNCHANGED)
        // =============================================================

        [HttpGet("{farmerId}/dashboard")]
        public async Task<IActionResult> GetDashboard([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetDashboardDataAsync(farmerId);
            return Ok(new { success = true, data });
        }

        [HttpDelete("{farmerId}/crop/{listingId}")]
        public async Task<IActionResult> DeleteCropListing([FromRoute] int farmerId, [FromRoute] int listingId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var success = await _helper.DeleteCropListingAsync(farmerId, listingId);
            if (!success) return NotFound(new { success = false, message = "Crop listing not found or cannot be deleted" });
            return Ok(new { success = true, message = "Crop listing deleted successfully" });
        }

        [HttpPost("listings")]
        public async Task<IActionResult> SaveListing([FromBody] vm_CropListingRequest req)
        {
            if (!FarmerOwns(req.FarmerId)) return Forbid();
            bool success = await _helper.SaveCropListingAsync(req);
            if (success) return Ok(new { success = true, message = req.IsDraft ? "Draft saved successfully." : "Crop listing published." });
            return BadRequest(new { success = false, message = "Failed to save listing. Cannot edit confirmed QC slots." });
        }

        [HttpGet("warehouses/{warehouseId}/slots")]
        public async Task<IActionResult> GetSlots([FromRoute] int warehouseId)
        {
            var slots = await _helper.GetAvailableQcSlotsAsync(warehouseId);
            return Ok(new { success = true, data = slots });
        }

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
            var data = await _helper.GetIncomeChartAsync(farmerId);
            return Ok(new { success = true, data });
        }

        [HttpGet("dropdowns/catalog")]
        public async Task<IActionResult> GetCatalogDropdown()
        {
            return Ok(new { success = true, data = await _helper.GetCatalogDropdownAsync() });
        }

        [HttpGet("dropdowns/warehouses")]
        public async Task<IActionResult> GetWarehousesDropdown()
        {
            return Ok(new { success = true, data = await _helper.GetWarehousesDropdownAsync() });
        }

        [HttpGet("{farmerId:int}/dropdowns/active-listings")]
        public async Task<IActionResult> GetActiveListingsDropdown([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            return Ok(new { success = true, data = await _helper.GetActiveListingsDropdownAsync(farmerId) });
        }

        [HttpGet("{farmerId:int}/listings")]
        public async Task<IActionResult> GetListings([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetFarmerListingsAsync(farmerId);
            return Ok(new { success = true, data });
        }

        [HttpGet("{farmerId:int}/qc-dashboard")]
        public async Task<IActionResult> GetQcDashboard([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetQcDashboardAsync(farmerId);
            return Ok(new { success = true, data });
        }

        [HttpGet("{farmerId:int}/inquiries")]
        public async Task<IActionResult> GetInquiries([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetInquiriesAsync(farmerId);
            return Ok(new { success = true, data });
        }

        [HttpPost("inquiries/submit")]
        public async Task<IActionResult> SubmitInquiry([FromBody] vm_SubmitInquiryRequest req)
        {
            if (!FarmerOwns(req.FarmerId)) return Forbid();
            bool success = await _helper.SubmitInquiryAsync(req);
            if (success) return Ok(new { success = true, message = "Inquiry submitted successfully." });
            return StatusCode(500, new { success = false, message = "Failed to submit inquiry." });
        }

        [HttpGet("{farmerId:int}/profile")]
        public async Task<IActionResult> GetProfile([FromRoute] int farmerId)
        {
            if (!FarmerOwns(farmerId)) return Forbid();
            var data = await _helper.GetFarmerProfileAsync(farmerId);
            return Ok(new { success = true, data });
        }

        [HttpPut("profile/update")]
        public async Task<IActionResult> UpdateProfile([FromBody] vm_UpdateProfileRequest req)
        {
            if (!FarmerOwns(req.FarmerId)) return Forbid();
            bool success = await _helper.UpdateFarmerProfileAsync(req);
            if (success) return Ok(new { success = true, message = "Profile updated successfully." });
            return StatusCode(500, new { success = false, message = "Failed to update profile." });
        }

        //Elastic Search - Method (Mansi)

        [HttpPost("search/my-crops")]
        public async Task<IActionResult> SearchMyCrops([FromBody] SearchRequestModel request)
        {
            var farmerId = GetTokenFarmerId(); // Your existing method
            request.FarmerId = farmerId;
            var results = await _elasticService.SearchCropsForMVCAsync(request);
            return Ok(results);
        }
    }

    // 👇 EXACTLY TWO HELPER CLASSES AT THE BOTTOM. NOTHING ELSE! 👇

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
} // <--- Final closing bracket of the namespace