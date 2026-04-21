using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.BAL;
using API.Models;
using API.Models.FieldOfficer;
using API.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using API.Models.Settings;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "field_officer")]
    public class FieldOfficerController : ControllerBase
    {
        private readonly FieldOfficerHelper _helper;
        private readonly CloudinaryService _cloudinaryService;
        private readonly EmailService _emailService;
        private readonly ILogger<FieldOfficerController> _logger;
        private readonly RabbitMqService _rabbitMqService;

        public FieldOfficerController(
            FieldOfficerHelper helper,
            CloudinaryService cloudinaryService,
            EmailService emailService,
            ILogger<FieldOfficerController> logger,
            RabbitMqService rabbitMqService)
            ElasticService elasticService)
        {
            _helper = helper;
            _cloudinaryService = cloudinaryService;
            _emailService = emailService;
            _logger = logger;
            _rabbitMqService = rabbitMqService;
            _elasticService = elasticService;
        }

        private async Task TryNotifyFarmerAsync(Func<Task> send)
        {
            try
            {
                await send().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Farmer email notification failed (check EmailSettings SMTP + farmer email in DB)");
            }
        }

        private Task SendInspectionReportEmailAsync(int procurementRequestId, string? grade, bool passed) =>
            TryNotifyFarmerAsync(async () =>
            {
                var farmer = await _helper.GetFarmerNotifyInfo(procurementRequestId);
                if (farmer == null || string.IsNullOrWhiteSpace(farmer.Email))
                {
                    _logger.LogWarning(
                        "Inspection email skipped: no farmer email for procurement request {ProcurementRequestId}",
                        procurementRequestId);
                    return;
                }

                var pdf = await _helper.GenerateInspectionPdfAsync(procurementRequestId);
                await _emailService.SendFarmerInspectionReportEmailAsync(
                    farmer,
                    pdf,
                    procurementRequestId,
                    string.IsNullOrWhiteSpace(grade) ? "—" : grade,
                    passed);
            });

        private int GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("sub");
            if (int.TryParse(userIdClaim, out var userIdFromToken))
                return userIdFromToken;

            throw new UnauthorizedAccessException("Missing or invalid field officer token.");
        }

        private async Task<int> GetFieldOfficerProfileIdAsync()
        {
            var userId = GetCurrentUserId();
            var foProfileId = await _helper.GetFoProfileIdByUserIdAsync(userId);
            if (!foProfileId.HasValue)
                throw new KeyNotFoundException("Field officer profile not found for current user.");
            return foProfileId.Value;
        }

        // ── TODAY SCHEDULES ─────────────────────────────────────────
        [HttpGet("today-schedules")]
        public async Task<IActionResult> GetTodaySchedules()
        {
            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();
                var data = await _helper.GetTodaySchedules(foId);
                return Ok(data);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ── ALL QC REQUESTS ─────────────────────────────────────────
        [HttpGet("qc-requests")]
        public async Task<IActionResult> GetQCRequests()
        {
            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();
                var data = await _helper.GetQCRequests(foId);
                return Ok(data);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ── SINGLE QC REQUEST BY ID ──────────────────────────────────
        // Called by Quality Form page to pre-fill farmer/crop details
        [HttpGet("qc-request/{id}")]
        public async Task<IActionResult> GetQCRequestById(int id)
        {
            var data = await _helper.GetQCRequestById(id);

            if (data == null)
                return NotFound(new { message = "Request not found" });

            return Ok(data);
        }

        // ── ACCEPT REQUEST ───────────────────────────────────────────
        [HttpPost("accept-request")]
        public async Task<IActionResult> AcceptRequest([FromBody] AcceptRequestDto request)
        {
            if (request == null || request.RequestId <= 0)
                return BadRequest(new { message = "Invalid request ID" });

            var data = await _helper.AcceptRequest(request.RequestId);

            if (data == null)
                return BadRequest(new { message = "Accept failed" });

            await TryNotifyFarmerAsync(() => _emailService.SendFarmerRequestAcceptedEmailAsync(data));

            // ✅ NOTIFICATION: QC Request Accepted
            await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                "QC Request Accepted ✅",
                $"Your QC request has been accepted by Field Officer. Your slot is confirmed for {data.SlotDateFormatted}",
                "qc_request");

            // ✅ NOTIFICATION to Admin
            await _rabbitMqService.PublishToRoleAsync("admin",
                "QC Request Accepted by FO",
                $"Field Officer has accepted a QC request for farmer {data.FarmerName}.",
                "qc_request");

            return Ok(new { message = "Request accepted" });
        }

        // ── RESCHEDULE REQUEST ───────────────────────────────────────
        [HttpPost("reschedule-request")]
        public async Task<IActionResult> RescheduleRequest(
            [FromForm] int requestId,
            [FromForm] string slotDate,
            [FromForm] string startTime,
            [FromForm] string endTime)
        {
            try
            {
                var date = DateTime.Parse(slotDate);
                var start = TimeSpan.Parse(startTime);
                var end = TimeSpan.Parse(endTime);

                var emailData = await _helper.RescheduleRequest(requestId, date, start, end);

                if (emailData == null)
                    return BadRequest(new { message = "Reschedule failed" });

                await TryNotifyFarmerAsync(() => _emailService.SendFarmerSlotRescheduledEmailAsync(emailData));

                // ✅ NOTIFICATION: QC Request Rescheduled
                await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                    "QC Slot Rescheduled 📅",
                    $"Your QC slot has been rescheduled to {date:dd MMM yyyy} at {start}.",
                    "qc_request");

                return Ok(new { message = "Rescheduled successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ── CANCEL REQUEST ───────────────────────────────────────────
        [HttpPost("cancel-request")]
        public async Task<IActionResult> CancelRequest([FromBody] CancelRequestDto request)
        {
            if (request == null || request.RequestId <= 0)
                return BadRequest(new { message = "Invalid request ID" });

            var data = await _helper.CancelProcurementRequest(request.RequestId);

            if (data == null)
                return BadRequest(new { message = "Cancel failed or request cannot be cancelled" });

            await TryNotifyFarmerAsync(() => _emailService.SendFarmerRequestCancelledEmailAsync(data));

            // ✅ NOTIFICATION: QC Request Cancelled
            await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                "QC Request Cancelled ❌",
                $"Your QC request has been cancelled. Reason: {data.CancelReason ?? "No reason provided"}",
                "qc_request");

            // ✅ NOTIFICATION to Admin
            await _rabbitMqService.PublishToRoleAsync("admin",
                "QC Request Cancelled by FO",
                $"Field Officer has cancelled a QC request for farmer {data.FarmerName}.",
                "qc_request");

            return Ok(new { message = "Request cancelled" });
        }

        // ── SUBMIT QUALITY INSPECTION ────────────────────────────────
        // Inserts inspection → updates procurement request → creates warehouse lot
        [HttpPost("submit-inspection")]
        public async Task<IActionResult> SubmitInspection([FromBody] QualityInspectionForm model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            int foId = await GetFieldOfficerProfileIdAsync();
            model.FoId = foId;

            var result = await _helper.SubmitInspection(model);

            if (!result)
                return BadRequest(new { message = "Submission failed. Please try again." });

            await SendInspectionReportEmailAsync(model.ProcurementRequestId, model.Grade, model.Passed);

            // ✅ NOTIFICATION: Inspection Results
            var farmerInfo = await _helper.GetFarmerNotifyInfo(model.ProcurementRequestId);
            if (model.Passed)
            {
                await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                    "QC Inspection Passed! 🎉",
                    $"Great news! Your {farmerInfo?.CropName} has passed QC inspection with {model.Grade} grade.",
                    "qc_result");
            }
            else
            {
                await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                    "QC Inspection Failed",
                    $"Your {farmerInfo?.CropName} did not pass QC inspection. Please check the report for details.",
                    "qc_result");
            }

            // ✅ NOTIFICATION to Admin
            await _rabbitMqService.PublishToRoleAsync("admin",
                model.Passed ? "QC Inspection Passed" : "QC Inspection Failed",
                $"Field Officer completed inspection for {farmerInfo?.CropName} - Result: {(model.Passed ? "Passed" : "Failed")}",
                "qc_result");

            return Ok(new { message = "Inspection submitted successfully" });
        }

        // ── SUBMIT INSPECTION WITH PHOTOS (Cloudinary) ───────────────
        [HttpPost("submit-inspection-with-photos")]
        public async Task<IActionResult> SubmitInspectionWithPhotos(
            [FromForm] int procurementRequestId,
            [FromForm] decimal moisturePct,
            [FromForm] decimal foreignMatterPct,
            [FromForm] string? pestDiseaseObserved,
            [FromForm] string? variety,
            [FromForm] string? grade,
            [FromForm] decimal weightCheckedKg,
            [FromForm] decimal acceptedQuantity,
            [FromForm] decimal rejectedQuantity,
            [FromForm] string? defectsNoted,
            [FromForm] string? remarks,
            [FromForm] decimal foAssessedPrice,
            [FromForm] bool passed,
            [FromForm] List<IFormFile>? photos)
        {
            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();

                var model = new QualityInspectionForm
                {
                    ProcurementRequestId = procurementRequestId,
                    FoId = foId,
                    MoisturePct = moisturePct,
                    ForeignMatterPct = foreignMatterPct,
                    PestDiseaseObserved = pestDiseaseObserved,
                    Variety = variety,
                    Grade = grade,
                    WeightCheckedKg = weightCheckedKg,
                    AcceptedQuantity = acceptedQuantity,
                    RejectedQuantity = rejectedQuantity,
                    DefectsNoted = defectsNoted,
                    Remarks = remarks,
                    FoAssessedPrice = foAssessedPrice,
                    Passed = passed
                };

                var inspectionId = await _helper.SubmitInspectionAndGetId(model);
                if (!inspectionId.HasValue)
                    return BadRequest(new { message = "Submission failed. Please try again." });

                var uploadedUrls = new List<string>();
                if (photos != null && photos.Count > 0)
                {
                    foreach (var photo in photos.Where(p => p != null && p.Length > 0))
                    {
                        var upload = await _cloudinaryService.UploadImageAsync(photo, "farmbridge/inspections");
                        if (upload.Success && !string.IsNullOrWhiteSpace(upload.SecureUrl))
                        {
                            uploadedUrls.Add(upload.SecureUrl);
                        }
                    }
                }

                if (uploadedUrls.Count > 0)
                    await _helper.SaveInspectionPhotosAsync(inspectionId.Value, uploadedUrls);

                await SendInspectionReportEmailAsync(procurementRequestId, grade, passed);

                // ✅ NOTIFICATION: Inspection Results
                var farmerInfo = await _helper.GetFarmerNotifyInfo(procurementRequestId);
                if (passed)
                {
                    await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                        "QC Inspection Passed! 🎉",
                        $"Great news! Your {farmerInfo?.CropName} has passed QC inspection with {grade} grade.",
                        "qc_result");
                }
                else
                {
                    await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                        "QC Inspection Failed",
                        $"Your {farmerInfo?.CropName} did not pass QC inspection. Please check the report for details.",
                        "qc_result");
                }

                return Ok(new
                {
                    message = "Inspection submitted successfully",
                    inspectionId = inspectionId.Value,
                    uploadedPhotos = uploadedUrls.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET INSPECTION DETAIL ────────────────────────────────────
        // Fetch completed inspection with calculated 30%/70% amounts
        // Accept procurementRequestId to find the associated inspection
        [HttpGet("inspection/{procurementRequestId}")]
        public async Task<IActionResult> GetInspectionDetail(int procurementRequestId)
        {
            var data = await _helper.GetInspectionDetail(procurementRequestId);

            if (data == null)
                return NotFound(new { message = "Inspection not found" });

            return Ok(data);
        }

        // ── REQUEST PAYMENT ──────────────────────────────────────────
        // Create payment request + notify admin
        [HttpPost("request-payment")]
        public async Task<IActionResult> RequestPayment(
            [FromBody] PaymentRequestDto request)
        {
            try
            {
                int inspectionId = request.inspectionId;
                int farmerId = request.farmerId;
                int procurementRequestId = request.procurementRequestId;
                decimal advanceAmount = request.advanceAmount;

                var result = await _helper.RequestPayment(
                    inspectionId,
                    farmerId,
                    procurementRequestId,
                    advanceAmount
                );

                if (!result)
                    return BadRequest(new { message = "Payment request failed" });

                // ✅ NOTIFICATION: Payment Request Created
                await _rabbitMqService.PublishToUserAsync(farmerId,
                    "Payment Initiated 💰",
                    $"Your payment of ₹{advanceAmount:N2} has been initiated. It will be credited within 3-5 business days.",
                    "payment");

                // ✅ NOTIFICATION to Admin
                await _rabbitMqService.PublishToRoleAsync("admin",
                    "Payment Request Created",
                    $"Field Officer has initiated a payment request of ₹{advanceAmount:N2} for farmer.",
                    "payment");

                return Ok(new { 
                    message = "Payment request created successfully",
                    advanceAmount = advanceAmount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── GET PROFILE ──────────────────────────────────────────────
        [HttpGet("profile/me")]
        public async Task<IActionResult> GetProfile()
        {
            var foId = await GetFieldOfficerProfileIdAsync();
            var profile = await _helper.GetProfileAsync(foId);
            if (profile == null)
                return NotFound(new { success = false, message = "Profile not found" });
            return Ok(new { success = true, data = profile });
        }

        // ── GET NOTIFICATIONS ─────────────────────────────────────────
        [HttpGet("GetNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();
                var notifications = await _helper.GetNotificationsAsync(foId);
                return Ok(new { success = true, data = notifications });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("MarkAllNotificationsRead")]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();
                await _helper.MarkAllNotificationsReadAsync(foId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("ClearAllNotifications")]
        public async Task<IActionResult> ClearAllNotifications()
        {
            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();
                await _helper.ClearAllNotificationsAsync(foId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── UPDATE PROFILE ───────────────────────────────────────────
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] vmUpdateProfile req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var foId = await GetFieldOfficerProfileIdAsync();
            req.Id = foId;
            var result = await _helper.UpdateProfileAsync(foId, req);

            if (!result)
                return BadRequest(new { message = "Update failed" });

            // ✅ NOTIFICATION: Profile Updated
            await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                "Profile Updated",
                "Your profile has been updated successfully.",
                "profile");

            return Ok(new { message = "Profile updated successfully" });
        }

        // ── CHANGE PASSWORD ──────────────────────────────────────────
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromForm] string email,
            [FromForm] string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
                return BadRequest(new { message = "Email and password required" });

            var result = await _helper.ChangePasswordAsync(email, newPassword);

            if (!result)
                return BadRequest(new { message = "Update failed" });

            // ✅ NOTIFICATION: Password Changed
            var user = await _helper.GetUserByEmailAsync(email);
            if (user != null)
            {
                await _rabbitMqService.PublishToUserAsync(GetCurrentUserId(),
                    "Password Changed",
                    "Your password has been changed successfully.",
                    "security");
            }

            return Ok(new { message = "Password updated successfully" });
        }

        [HttpGet("warehouse-catalog")]
        public async Task<IActionResult> GetWarehouseCatalog()
        {
            int foId = await GetFieldOfficerProfileIdAsync();
            var data = await _helper.GetWarehouseCatalog(foId);
            return Ok(data);
        }

        // ── UPLOAD PROFILE IMAGE ─────────────────────────────────────
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            try
            {
                int foId = await GetFieldOfficerProfileIdAsync();
                var profile = await _helper.GetProfileAsync(foId);
                var oldImageUrl = profile?.ProfileImageUrl;

                var upload = await _cloudinaryService.ReplaceImageAsync(
                    image,
                    oldImageUrl,
                    "farmbridge/fieldofficer"
                );

                if (!upload.Success || string.IsNullOrWhiteSpace(upload.SecureUrl))
                    return BadRequest(new { message = upload.Error ?? "Cloudinary upload failed" });

                var updated = await _helper.UpdateImageAsync(foId, upload.SecureUrl);

                if (!updated)
                    return BadRequest(new { message = "DB update failed" });

                return Ok(new { message = "Uploaded", imageUrl = upload.SecureUrl, publicId = upload.PublicId });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("inspection-report/{procurementRequestId}")]
        public async Task<IActionResult> DownloadInspectionReport(int procurementRequestId)
        {
            try
            {
                var pdfBytes = await _helper.GenerateInspectionPdfAsync(procurementRequestId);
                return File(pdfBytes, "application/pdf", $"Inspection_{procurementRequestId}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ──               PAYMENT ENDPOINTS (NEW)                    ──
        // ══════════════════════════════════════════════════════════════

        // ── PROCESS ADVANCE PAYMENT (30%) ──────────────────────────────
        // Auto payment when FO clicks "Request 30% Payment"
        [HttpPost("payment/advance")]
        public async Task<IActionResult> ProcessAdvancePayment([FromBody] AdvancePaymentRequestDto request)
        {
            try
            {
                int procurementRequestId = request.procurementRequestId;
                int farmerId = request.farmerId;
                int inspectionId = request.inspectionId;
                decimal advanceAmount = request.advanceAmount;

                var hasPayment = await _helper.HasAdvancePayment(procurementRequestId);
                if (hasPayment)
                    return BadRequest(new { message = "Payment already processed for this inspection" });

                var result = await _helper.ProcessAdvancePayment(
                    procurementRequestId,
                    farmerId,
                    inspectionId,
                    advanceAmount
                );

                if (result?.success == false)
                    return BadRequest(result);

                // ✅ NOTIFICATION: Advance Payment Processed
                await _rabbitMqService.PublishToUserAsync(farmerId,
                    "Advance Payment Processed 💰",
                    $"Your advance payment of ₹{advanceAmount:N2} has been processed. UTR: {result?.utrReference}",
                    "payment");

                // ✅ NOTIFICATION to Admin
                await _rabbitMqService.PublishToRoleAsync("admin",
                    "Advance Payment Processed",
                    $"Advance payment of ₹{advanceAmount:N2} has been processed for farmer.",
                    "payment");

                await TryNotifyFarmerAsync(async () =>
                {
                    var farmer = await _helper.GetFarmerNotifyInfo(procurementRequestId);
                    var detail = await _helper.GetInspectionDetail(procurementRequestId);
                    if (farmer == null || detail == null || string.IsNullOrWhiteSpace(farmer.Email))
                    {
                        _logger.LogWarning(
                            "Payment email skipped: farmer/detail missing or no email for procurement {ProcurementRequestId}",
                            procurementRequestId);
                        return;
                    }

                    var utr = result != null ? Convert.ToString(result.utrReference) : null;
                    var farmerNm = Convert.ToString(detail.farmerName) ?? farmer.FullName;
                    var cropNm = Convert.ToString(detail.cropName) ?? farmer.CropName;
                    var totalVal = Convert.ToDecimal(detail.totalValue);
                    var remainingVal = Convert.ToDecimal(detail.remainingAmount);
                    var paymentId = 0;
                    if (result != null)
                    {
                        try { paymentId = Convert.ToInt32(result.paymentId); }
                        catch { /* dynamic payload */ }
                    }

                    await _emailService.SendFarmerAdvancePaymentEmailAsync(
                        farmer.Email,
                        farmerNm,
                        cropNm,
                        totalVal,
                        advanceAmount,
                        remainingVal,
                        utr,
                        paymentId);
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProcessAdvancePayment ERROR: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET FARMER PAYMENT HISTORY ─────────────────────────────────
        [HttpGet("payment-history/{farmerId}")]
        public async Task<IActionResult> GetPaymentHistory(int farmerId)
        {
            try
            {
                var payments = await _helper.GetFarmerPaymentHistory(farmerId);
                return Ok(new
                {
                    success = true,
                    totalPayments = payments.Count,
                    payments = payments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET PAYMENT DETAIL ─────────────────────────────────────────
        [HttpGet("payment/{paymentId}")]
        public async Task<IActionResult> GetPaymentDetail(int paymentId)
        {
            try
            {
                var payment = await _helper.GetPaymentDetail(paymentId);
                if (payment == null)
                    return NotFound(new { message = "Payment not found" });

                return Ok(payment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET FO PAYMENT SUMMARY ──────────────────────────────────────
        // Returns farmer payments that belong to procurement requests handled by this FO.
        [HttpGet("fo-payment-summary")]
        [HttpGet("fo-payment-summary/{foId}")]
        public async Task<IActionResult> GetFoPaymentSummary(int? foId = null)
        {
            try
            {
                var currentFoId = await GetFieldOfficerProfileIdAsync();
                var payments = await _helper.GetFoPaymentSummary(currentFoId);

                return Ok(new
                {
                    success = true,
                    totalPayments = payments.Count,
                    payments = payments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── CHECK IF ADVANCE PAYMENT DONE ──────────────────────────────
        [HttpGet("payment/check/{procurementRequestId}")]
        public async Task<IActionResult> CheckAdvancePayment(int procurementRequestId)
        {
            try
            {
                var hasPayment = await _helper.HasAdvancePayment(procurementRequestId);
                return Ok(new
                {
                    paymentDone = hasPayment,
                    message = hasPayment ? "Payment already processed" : "No payment processed yet"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        //Elastic Search - Method (Mansi)
        [HttpPost("search/qc-records")]
        public async Task<IActionResult> SearchQCRecords([FromBody] SearchRequestModel request)
        {
            var foId = await GetFieldOfficerProfileIdAsync(); // Your existing method
            request.FoId = foId;
            var results = await _elasticService.SearchQCRecordsForMVCAsync(request);
            return Ok(results);
        }
    }
}