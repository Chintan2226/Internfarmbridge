// using System;
// using System.IO;
// using System.Threading.Tasks;
// using API.Models.FieldOfficer;
// using API.Models.Settings;
// using MailKit.Net.Smtp;
// using MailKit.Security;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
// using MimeKit;

// namespace API.Services
// {
//     public class EmailService
//     {
//         private readonly EmailSettings _settings;
//         private readonly IWebHostEnvironment _env;
//         private readonly IConfiguration _config;

//         private readonly ILogger<EmailService> _logger;

//         public EmailService(
//             IOptions<EmailSettings> options,
//             IWebHostEnvironment env,
//             ILogger<EmailService> logger, IConfiguration config)
//         {
//             _settings = options.Value;
//             _env = env;
//             _logger = logger;
//             _config = config;
//         }

//         private string FarmerPortalUrl =>
//             string.IsNullOrWhiteSpace(_settings.FarmerPortalBaseUrl)
//                 ? "https://farmbridge.app"
//                 : _settings.FarmerPortalBaseUrl!.TrimEnd('/');

//         private static string NormalizePassword(string? p) =>
//             (p ?? string.Empty).Replace(" ", string.Empty);

//         private SecureSocketOptions ResolveSecureSocketOptions()
//         {
//             var mode = _settings.SmtpSslMode?.Trim();
//             if (!string.IsNullOrEmpty(mode)
//                 && Enum.TryParse<SecureSocketOptions>(mode, true, out var parsed))
//                 return parsed;

//             return _settings.Port == 465
//                 ? SecureSocketOptions.SslOnConnect
//                 : SecureSocketOptions.StartTls;
//         }

//         private async Task SendAsync(
//             string toEmail,
//             string subject,
//             string htmlContent,
//             byte[]? pdfAttachment = null,
//             string? pdfFileName = null)
//         {
//             if (_settings.Disabled)
//             {
//                 _logger.LogWarning("Email skipped: EmailSettings:Disabled is true (subject: {Subject})", subject);
//                 return;
//             }

//             var fromEmail = _settings.From ?? _settings.Username;
//             if (string.IsNullOrWhiteSpace(fromEmail))
//                 throw new InvalidOperationException("Configure EmailSettings:From or EmailSettings:Username.");

//             var host = _settings.Host?.Trim();
//             if (string.IsNullOrWhiteSpace(host) || host.Contains("example.com", StringComparison.OrdinalIgnoreCase))
//                 throw new InvalidOperationException(
//                     "Configure a real SMTP host in EmailSettings:Host (e.g. smtp.gmail.com for Gmail). " +
//                     "Placeholder smtp.example.com will not send mail.");

//             var port = _settings.Port > 0 ? _settings.Port : 587;
//             var user = _settings.Username ?? fromEmail;
//             var password = NormalizePassword(_settings.Password);
//             if (string.IsNullOrEmpty(password))
//                 _logger.LogWarning("EmailSettings:Password is empty — SMTP authentication may fail.");

//             var socketOptions = ResolveSecureSocketOptions();

//             var email = new MimeMessage();
//             email.From.Add(new MailboxAddress("FarmBridge", fromEmail));
//             email.To.Add(MailboxAddress.Parse(toEmail));
//             email.Subject = subject;

//             var body = new BodyBuilder { HtmlBody = htmlContent };
//             if (pdfAttachment is { Length: > 0 } && !string.IsNullOrWhiteSpace(pdfFileName))
//                 body.Attachments.Add(pdfFileName, pdfAttachment, ContentType.Parse("application/pdf"));

//             email.Body = body.ToMessageBody();

//             try
//             {
//                 using var smtp = new SmtpClient();
//                 if (smtp.Timeout < 120_000)
//                     smtp.Timeout = 120_000;

//                 _logger.LogInformation(
//                     "SMTP connect {Host}:{Port} mode {Mode} → {To}",
//                     host, port, socketOptions, toEmail);

//                 await smtp.ConnectAsync(host, port, socketOptions);
//                 await smtp.AuthenticateAsync(user, password);
//                 await smtp.SendAsync(email);
//                 await smtp.DisconnectAsync(true);

//                 _logger.LogInformation("Email sent OK: {To} — {Subject}", toEmail, subject);
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex,
//                     "SMTP failed (host {Host}, port {Port}, ssl {Mode}, user {User}, to {To})",
//                     host, port, socketOptions, user, toEmail);
//                 throw;
//             }
//         }

//         private async Task<string> LoadTemplate(string fileName)
//         {
//             var path = Path.Combine(_env.ContentRootPath, "Templates", fileName);
//             if (!File.Exists(path))
//                 throw new FileNotFoundException($"Template not found: {path}");
//             return await File.ReadAllTextAsync(path);
//         }

//         private static bool CanSend(string? email) =>
//             !string.IsNullOrWhiteSpace(email);

//         private void LogSkip(string context, string? to)
//         {
//             _logger.LogWarning("Email not sent ({Context}): missing or blank farmer address. To={To}", context, to ?? "(null)");
//         }

//         public async Task SendPasswordEmailAsync(string toEmail, string password, string userName)
//         {
//             if (!CanSend(toEmail)) { LogSkip(nameof(SendPasswordEmailAsync), toEmail); return; }
//             var html = await LoadTemplate("PasswordEmailTemplate.html");
//             var safeName = string.IsNullOrWhiteSpace(userName) ? "Farmer" : userName;
//             var initial = !string.IsNullOrEmpty(safeName) ? safeName.Substring(0, 1).ToUpperInvariant() : "F";
//             html = html
//                 .Replace("{{USERNAME}}", safeName)
//                 .Replace("{{INITIAL}}", initial)
//                 .Replace("{{PASSWORD}}", password)
//                 .Replace("{{LOGIN_URL}}", $"{FarmerPortalUrl}/login");
//             await SendAsync(toEmail, "Welcome to FarmBridge – Your Credentials", html);
//         }

//         public async Task SendFieldOfficerDeactivationEmailAsync(
//             string toEmail, string officerName, string assignedRegion)
//         {
//             if (!CanSend(toEmail)) { LogSkip(nameof(SendFieldOfficerDeactivationEmailAsync), toEmail); return; }
//             var html = await LoadTemplate("FieldOfficerAccountDeactivatedTemplate.html");
//             html = html
//                 .Replace("{{OFFICER_NAME}}", officerName ?? "Field Officer")
//                 .Replace("{{OFFICER_EMAIL}}", toEmail)
//                 .Replace("{{ASSIGNED_REGION}}", assignedRegion ?? "Not Assigned")
//                 .Replace("{{DEACTIVATED_AT}}", DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"))
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             await SendAsync(toEmail, "FarmBridge Account Deactivated", html);
//         }

//         public async Task SendFieldOfficerStatusChangedEmailAsync(
//             string toEmail, string? officerName, string? assignedRegion, bool isActive)
//         {
//             if (!CanSend(toEmail)) { LogSkip(nameof(SendFieldOfficerStatusChangedEmailAsync), toEmail); return; }
//             if (!isActive)
//             {
//                 await SendFieldOfficerDeactivationEmailAsync(
//                     toEmail, officerName ?? "Field Officer", assignedRegion ?? "Not Assigned");
//                 return;
//             }

//             var safeName = string.IsNullOrWhiteSpace(officerName) ? "Field Officer" : officerName;
//             var safeRegion = string.IsNullOrWhiteSpace(assignedRegion) ? "Not Assigned" : assignedRegion;
//             var html = await LoadTemplate("FieldOfficerAccountActivatedTemplate.html");
//             html = html
//                 .Replace("{{OFFICER_NAME}}", safeName)
//                 .Replace("{{OFFICER_EMAIL}}", toEmail)
//                 .Replace("{{ASSIGNED_REGION}}", safeRegion)
//                 .Replace("{{ACTIVATED_AT}}", DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"))
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             await SendAsync(toEmail, "FarmBridge Account Activated", html);
//         }

//         public async Task SendFarmerSlotRescheduledEmailAsync(RescheduleEmailData data)
//         {
//             if (!CanSend(data.FarmerEmail)) { LogSkip(nameof(SendFarmerSlotRescheduledEmailAsync), data.FarmerEmail); return; }
//             var html = await LoadTemplate("FarmerSlotRescheduledTemplate.html");
//             html = html
//                 .Replace("{{REQUEST_ID}}", data.ProcurementRequestId.ToString())
//                 .Replace("{{FARMER_NAME}}", data.FarmerName)
//                 .Replace("{{CROP_NAME}}", data.CropName)
//                 .Replace("{{WAREHOUSE_NAME}}", data.WarehouseName)
//                 .Replace("{{OLD_SLOT_DATE}}", data.OldSlotDateFormatted)
//                 .Replace("{{OLD_TIME_RANGE}}", data.OldTimeRange)
//                 .Replace("{{NEW_SLOT_DATE}}", data.NewSlotDateFormatted)
//                 .Replace("{{NEW_TIME_RANGE}}", data.NewTimeRange)
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             await SendAsync(data.FarmerEmail, "FarmBridge — Your warehouse slot was rescheduled", html);
//         }

//         public async Task SendFarmerRequestAcceptedEmailAsync(AcceptEmailData data)
//         {
//             if (!CanSend(data.FarmerEmail)) { LogSkip(nameof(SendFarmerRequestAcceptedEmailAsync), data.FarmerEmail); return; }
//             var html = await LoadTemplate("FarmerRequestAcceptedTemplate.html");
//             html = html
//                 .Replace("{{QC_REQUEST_ID}}", data.ProcurementRequestId.ToString())
//                 .Replace("{{FARMER_NAME}}", data.FarmerName)
//                 .Replace("{{CROP_NAME}}", data.CropName)
//                 .Replace("{{WAREHOUSE_NAME}}", data.WarehouseName)
//                 .Replace("{{QUANTITY}}", data.QuantityDisplay)
//                 .Replace("{{SLOT_DATE}}", data.SlotDateFormatted)
//                 .Replace("{{TIME_RANGE}}", data.TimeRange)
//                 .Replace("{{ACCEPTED_AT}}", data.AcceptedAtFormatted)
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             await SendAsync(data.FarmerEmail, "FarmBridge — Your procurement request was accepted", html);
//         }

//         public async Task SendFarmerRequestCancelledEmailAsync(CancelEmailData data)
//         {
//             if (!CanSend(data.FarmerEmail)) { LogSkip(nameof(SendFarmerRequestCancelledEmailAsync), data.FarmerEmail); return; }
//             var html = await LoadTemplate("FarmerRequestCancelledTemplate.html");
//             html = html
//                 .Replace("{{BOOKING_ID}}", data.ProcurementRequestId.ToString())
//                 .Replace("{{FARMER_NAME}}", data.FarmerName)
//                 .Replace("{{CROP_NAME}}", data.CropName)
//                 .Replace("{{WAREHOUSE_NAME}}", data.WarehouseName)
//                 .Replace("{{SLOT_DATE}}", data.SlotDateDisplay)
//                 .Replace("{{CANCELLED_AT}}", data.CancelledAtFormatted)
//                 .Replace("{{CANCEL_REASON}}", data.CancelReason)
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             await SendAsync(data.FarmerEmail, "FarmBridge — Procurement request cancelled", html);
//         }

//         public async Task SendFarmerInspectionReportEmailAsync(
//             FarmerNotifyInfo farmer,
//             byte[] pdfBytes,
//             int procurementRequestId,
//             string grade,
//             bool passed)
//         {
//             if (!CanSend(farmer.Email)) { LogSkip(nameof(SendFarmerInspectionReportEmailAsync), farmer.Email); return; }
//             var html = await LoadTemplate("FarmerInspectionReportTemplate.html");
//             var resultText = passed ? "Passed" : "Did not pass";
//             html = html
//                 .Replace("{{PROCUREMENT_ID}}", procurementRequestId.ToString())
//                 .Replace("{{FARMER_NAME}}", farmer.FullName)
//                 .Replace("{{CROP_NAME}}", farmer.CropName)
//                 .Replace("{{GRADE}}", grade)
//                 .Replace("{{RESULT}}", resultText)
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             var fileName = $"FarmBridge_Inspection_{procurementRequestId}.pdf";
//             await SendAsync(
//                 farmer.Email,
//                 "FarmBridge — Your quality inspection report",
//                 html,
//                 pdfBytes,
//                 fileName);
//         }

//         public async Task SendFarmerAdvancePaymentEmailAsync(
//             string toEmail,
//             string farmerName,
//             string cropName,
//             decimal totalAmount,
//             decimal paidAmount,
//             decimal remainingAmount,
//             string? utrReference,
//             int paymentId)
//         {
//             if (!CanSend(toEmail)) { LogSkip(nameof(SendFarmerAdvancePaymentEmailAsync), toEmail); return; }
//             var html = await LoadTemplate("FarmerAdvancePaymentTemplate.html");
//             var utr = string.IsNullOrWhiteSpace(utrReference) ? "—" : utrReference;
//             var settledAt = DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt");
//             html = html
//                 .Replace("{{FARMER_NAME}}", farmerName)
//                 .Replace("{{CROP_NAME}}", cropName)
//                 .Replace("{{AMOUNT}}", paidAmount.ToString("N2"))
//                 .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
//                 .Replace("{{PAID_AMOUNT}}", paidAmount.ToString("N2"))
//                 .Replace("{{REMAINING_AMOUNT}}", remainingAmount.ToString("N2"))
//                 .Replace("{{PAYMENT_ID}}", paymentId.ToString())
//                 .Replace("{{UTR_REFERENCE}}", utr)
//                 .Replace("{{PAYMENT_MODE}}", "Bank transfer")
//                 .Replace("{{PAYMENT_NUMBER}}", "1 — Advance (30%)")
//                 .Replace("{{TRIGGER_EVENT}}", "QC passed — advance payment")
//                 .Replace("{{SETTLED_AT}}", settledAt)
//                 .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
//             await SendAsync(toEmail, "FarmBridge — Advance payment (30%) processed", html);
//         }

//         public async Task SendOtpEmailAsync(string toEmail, string sixDigitOtp)
//         {
//             if (!CanSend(toEmail)) { LogSkip(nameof(SendOtpEmailAsync), toEmail); return; }
//             var padded = (sixDigitOtp ?? "").PadLeft(6, '0');
//             if (padded.Length > 6) padded = padded.Substring(0, 6);
//             var html = await LoadTemplate("EmailTemplate.html");
//             for (int i = 0; i < 6; i++)
//                 html = html.Replace("{{D" + (i + 1) + "}}", padded[i].ToString());
//             await SendAsync(toEmail, "FarmBridge — Your verification code", html);
//         }


//         // vendor email -------------------------------------------

//         // Password Service
//         public async Task SendPasswordEmail(string toEmail, string password, string userName)
//         {
//             var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "PasswordEmailTemplate.html");

//             if (!File.Exists(templatePath))
//                 throw new Exception("Template not found");

//             var htmlContent = await File.ReadAllTextAsync(templatePath);

//             var safeName = string.IsNullOrEmpty(userName) ? "User" : userName;

//             htmlContent = htmlContent
//                 .Replace("{{PASSWORD}}", password)
//                 .Replace("{{USERNAME}}", safeName)
//                 .Replace("{{INITIAL}}", safeName.Substring(0, 1).ToUpper())
//                 .Replace("{{LOGIN_URL}}", "http://localhost:5080/Account/Login");

//             var email = new MimeMessage();
//             email.From.Add(MailboxAddress.Parse(_config["EmailSettings:Username"]));
//             email.To.Add(MailboxAddress.Parse(toEmail));
//             email.Subject = "Your FarmBridge Account Password";

//             email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
//             {
//                 Text = htmlContent
//             };

//             using var smtp = new SmtpClient();
//             await smtp.ConnectAsync(_config["EmailSettings:Host"], int.Parse(_config["EmailSettings:Port"]), false);
//             await smtp.AuthenticateAsync(_config["EmailSettings:Username"], _config["EmailSettings:Password"]);
//             await smtp.SendAsync(email);
//             await smtp.DisconnectAsync(true);
//         }

//         public async Task SendOtpEmail(string toEmail, string otp)
//         {
//             // ✅ Template Path
//             var path = Path.Combine(_env.ContentRootPath, "Templates", "EmailTemplate.html");

//             if (!File.Exists(path))
//                 throw new Exception("Template not found");

//             var html = await File.ReadAllTextAsync(path);

//             // ✅ Ensure OTP length
//             otp = otp.PadRight(6, '0');

//             // ✅ Replace OTP Digits
//             html = html
//                 .Replace("{{D1}}", otp[0].ToString())
//                 .Replace("{{D2}}", otp[1].ToString())
//                 .Replace("{{D3}}", otp[2].ToString())
//                 .Replace("{{D4}}", otp[3].ToString())
//                 .Replace("{{D5}}", otp[4].ToString())
//                 .Replace("{{D6}}", otp[5].ToString());

//             // ✅ Email Config
//             var fromEmail = _config["EmailSettings:From"] ?? _config["EmailSettings:Username"];
//             var host = _config["EmailSettings:Host"];
//             var port = int.Parse(_config["EmailSettings:Port"]);

//             var email = new MimeMessage();
//             email.From.Add(new MailboxAddress("FarmBridge", fromEmail));
//             email.To.Add(MailboxAddress.Parse(toEmail));
//             email.Subject = "Your OTP Code";

//             var builder = new BodyBuilder
//             {
//                 HtmlBody = html
//             };

//             email.Body = builder.ToMessageBody();

//             using var smtp = new SmtpClient();

//             await smtp.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);

//             await smtp.AuthenticateAsync(
//                 _config["EmailSettings:Username"],
//                 _config["EmailSettings:Password"]
//             );

//             await smtp.SendAsync(email);
//             await smtp.DisconnectAsync(true);
//         }

//         // ── VENDOR ORDER EMAILS ──────────────────────────────────────────

//         public async Task SendVendorOrderSuccessEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string deliveryAddress, string preferredDate, string status)
//         {
//             if (!CanSend(toEmail)) return;
//             var html = await LoadTemplate("VendorOrderSuccessTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
//                 .Replace("{{DELIVERY_ADDRESS}}", deliveryAddress ?? "—")
//                 .Replace("{{PREFERRED_DATE}}", preferredDate ?? "Standard Delivery")
//                 .Replace("{{STATUS}}", status ?? "Confirmed")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080"); // Change to your Vendor Portal URL
            
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Confirmed", html);
//         }

//         public async Task SendVendorOrderRescheduledEmailAsync(string toEmail, string vendorName, int orderId, string oldDate, string newDate, string deliveryAddress, string reason)
//         {
//             if (!CanSend(toEmail)) return;
//             var html = await LoadTemplate("VendorOrderRescheduledTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{OLD_DATE}}", oldDate ?? "—")
//                 .Replace("{{NEW_DATE}}", newDate ?? "—")
//                 .Replace("{{DELIVERY_ADDRESS}}", deliveryAddress ?? "—")
//                 .Replace("{{RESCHEDULE_REASON}}", reason ?? "Logistics update")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080");
            
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Rescheduled", html);
//         }

//         public async Task SendVendorOrderDeliveredEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string deliveredAt, string logisticsPartner, string trackingNumber)
//         {
//             if (!CanSend(toEmail)) return;
//             var html = await LoadTemplate("VendorOrderDeliveredTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
//                 .Replace("{{DELIVERED_AT}}", deliveredAt ?? DateTime.Now.ToString("MMM dd, yyyy"))
//                 .Replace("{{LOGISTICS_PARTNER}}", logisticsPartner ?? "FarmBridge Logistics")
//                 .Replace("{{TRACKING_NUMBER}}", trackingNumber ?? "—")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080");
            
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Delivered!", html);
//         }

//        // ── VENDOR ORDER EMAILS ──────────────────────────────────────────

//         public async Task SendVendorOrderCancelledEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string cancelReason)
//         {
//             if (!CanSend(toEmail)) return;
            
//             var html = await LoadTemplate("VendorOrderCancelledTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
//                 .Replace("{{CANCELLED_AT}}", DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"))
//                 .Replace("{{CANCEL_REASON}}", cancelReason ?? "No reason provided")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080"); 
                
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Cancelled", html);
//         }

//         public async Task SendVendorOrderSuccessEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string deliveryAddress, string preferredDate, string status)
//         {
//             if (!CanSend(toEmail)) return;
//             var html = await LoadTemplate("VendorOrderSuccessTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
//                 .Replace("{{DELIVERY_ADDRESS}}", deliveryAddress ?? "Registered Address")
//                 .Replace("{{PREFERRED_DATE}}", preferredDate ?? "Standard Delivery")
//                 .Replace("{{STATUS}}", status ?? "Confirmed")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080"); 
            
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Confirmed", html);
//         }

//         public async Task SendVendorOrderRescheduledEmailAsync(string toEmail, string vendorName, int orderId, string oldDate, string newDate, string deliveryAddress, string reason)
//         {
//             if (!CanSend(toEmail)) return;
//             var html = await LoadTemplate("VendorOrderRescheduledTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{OLD_DATE}}", oldDate ?? "—")
//                 .Replace("{{NEW_DATE}}", newDate ?? "—")
//                 .Replace("{{DELIVERY_ADDRESS}}", deliveryAddress ?? "Registered Address")
//                 .Replace("{{RESCHEDULE_REASON}}", reason ?? "Logistics update")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080");
            
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Rescheduled", html);
//         }

//         public async Task SendVendorOrderDeliveredEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string deliveredAt, string logisticsPartner, string trackingNumber)
//         {
//             if (!CanSend(toEmail)) return;
//             var html = await LoadTemplate("VendorOrderDeliveredTemplate.html");
//             html = html
//                 .Replace("{{ORDER_ID}}", orderId.ToString())
//                 .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
//                 .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
//                 .Replace("{{DELIVERED_AT}}", deliveredAt ?? DateTime.Now.ToString("MMM dd, yyyy"))
//                 .Replace("{{LOGISTICS_PARTNER}}", logisticsPartner ?? "FarmBridge Logistics")
//                 .Replace("{{TRACKING_NUMBER}}", trackingNumber ?? "—")
//                 .Replace("{{PORTAL_URL}}", "http://localhost:5080");
            
//             await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Delivered!", html);
//         }
//     }
// }


using System;
using System.IO;
using System.Threading.Tasks;
using API.Models.FieldOfficer;
using API.Models.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace API.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailSettings> options,
            IWebHostEnvironment env,
            ILogger<EmailService> logger, 
            IConfiguration config)
        {
            _settings = options.Value;
            _env = env;
            _logger = logger;
            _config = config;
        }

        private string FarmerPortalUrl =>
            string.IsNullOrWhiteSpace(_settings.FarmerPortalBaseUrl)
                ? "https://farmbridge.app"
                : _settings.FarmerPortalBaseUrl!.TrimEnd('/');

        private static string NormalizePassword(string? p) =>
            (p ?? string.Empty).Replace(" ", string.Empty);

        private SecureSocketOptions ResolveSecureSocketOptions()
        {
            var mode = _settings.SmtpSslMode?.Trim();
            if (!string.IsNullOrEmpty(mode)
                && Enum.TryParse<SecureSocketOptions>(mode, true, out var parsed))
                return parsed;

            return _settings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
        }

        private async Task SendAsync(
            string toEmail,
            string subject,
            string htmlContent,
            byte[]? pdfAttachment = null,
            string? pdfFileName = null)
        {
            if (_settings.Disabled)
            {
                _logger.LogWarning("Email skipped: EmailSettings:Disabled is true (subject: {Subject})", subject);
                return;
            }

            var fromEmail = _settings.From ?? _settings.Username;
            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("Configure EmailSettings:From or EmailSettings:Username.");

            var host = _settings.Host?.Trim();
            if (string.IsNullOrWhiteSpace(host) || host.Contains("example.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Configure a real SMTP host in EmailSettings:Host (e.g. smtp.gmail.com for Gmail). " +
                    "Placeholder smtp.example.com will not send mail.");

            var port = _settings.Port > 0 ? _settings.Port : 587;
            var user = _settings.Username ?? fromEmail;
            var password = NormalizePassword(_settings.Password);
            if (string.IsNullOrEmpty(password))
                _logger.LogWarning("EmailSettings:Password is empty — SMTP authentication may fail.");

            var socketOptions = ResolveSecureSocketOptions();

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("FarmBridge", fromEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var body = new BodyBuilder { HtmlBody = htmlContent };
            if (pdfAttachment is { Length: > 0 } && !string.IsNullOrWhiteSpace(pdfFileName))
                body.Attachments.Add(pdfFileName, pdfAttachment, ContentType.Parse("application/pdf"));

            email.Body = body.ToMessageBody();

            try
            {
                using var smtp = new SmtpClient();
                if (smtp.Timeout < 120_000)
                    smtp.Timeout = 120_000;

                _logger.LogInformation(
                    "SMTP connect {Host}:{Port} mode {Mode} → {To}",
                    host, port, socketOptions, toEmail);

                await smtp.ConnectAsync(host, port, socketOptions);
                await smtp.AuthenticateAsync(user, password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                _logger.LogInformation("Email sent OK: {To} — {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SMTP failed (host {Host}, port {Port}, ssl {Mode}, user {User}, to {To})",
                    host, port, socketOptions, user, toEmail);
                throw;
            }
        }

        private async Task<string> LoadTemplate(string fileName)
        {
            var path = Path.Combine(_env.ContentRootPath, "Templates", fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Template not found: {path}");
            return await File.ReadAllTextAsync(path);
        }

        private static bool CanSend(string? email) =>
            !string.IsNullOrWhiteSpace(email);

        private void LogSkip(string context, string? to)
        {
            _logger.LogWarning("Email not sent ({Context}): missing or blank address. To={To}", context, to ?? "(null)");
        }

        // ── FARMER & FO EMAILS ──────────────────────────────────────────

        public async Task SendFarmerWelcomeEmailAsync(string toEmail, string farmerName)
        {
            if (!CanSend(toEmail)) { LogSkip(nameof(SendFarmerWelcomeEmailAsync), toEmail); return; }
            
            var html = await LoadTemplate("FarmerWelcomeTemplate.html");
            
            var safeName = string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName;
            
            html = html
                .Replace("{{FARMER_NAME}}", safeName)
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
                
            await SendAsync(toEmail, "Welcome to FarmBridge!", html);
        }

        public async Task SendPasswordEmailAsync(string toEmail, string password, string userName)
        {
            if (!CanSend(toEmail)) { LogSkip(nameof(SendPasswordEmailAsync), toEmail); return; }
            var html = await LoadTemplate("PasswordEmailTemplate.html");
            var safeName = string.IsNullOrWhiteSpace(userName) ? "Farmer" : userName;
            var initial = !string.IsNullOrEmpty(safeName) ? safeName.Substring(0, 1).ToUpperInvariant() : "F";
            html = html
                .Replace("{{USERNAME}}", safeName)
                .Replace("{{INITIAL}}", initial)
                .Replace("{{PASSWORD}}", password)
                .Replace("{{LOGIN_URL}}", $"{FarmerPortalUrl}/login");
            await SendAsync(toEmail, "Welcome to FarmBridge – Your Credentials", html);
        }

        public async Task SendFieldOfficerDeactivationEmailAsync(string toEmail, string officerName, string assignedRegion)
        {
            if (!CanSend(toEmail)) { LogSkip(nameof(SendFieldOfficerDeactivationEmailAsync), toEmail); return; }
            var html = await LoadTemplate("FieldOfficerAccountDeactivatedTemplate.html");
            html = html
                .Replace("{{OFFICER_NAME}}", officerName ?? "Field Officer")
                .Replace("{{OFFICER_EMAIL}}", toEmail)
                .Replace("{{ASSIGNED_REGION}}", assignedRegion ?? "Not Assigned")
                .Replace("{{DEACTIVATED_AT}}", DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"))
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            await SendAsync(toEmail, "FarmBridge Account Deactivated", html);
        }

        public async Task SendFieldOfficerStatusChangedEmailAsync(string toEmail, string? officerName, string? assignedRegion, bool isActive)
        {
            if (!CanSend(toEmail)) { LogSkip(nameof(SendFieldOfficerStatusChangedEmailAsync), toEmail); return; }
            if (!isActive)
            {
                await SendFieldOfficerDeactivationEmailAsync(toEmail, officerName ?? "Field Officer", assignedRegion ?? "Not Assigned");
                return;
            }

            var safeName = string.IsNullOrWhiteSpace(officerName) ? "Field Officer" : officerName;
            var safeRegion = string.IsNullOrWhiteSpace(assignedRegion) ? "Not Assigned" : assignedRegion;
            var html = await LoadTemplate("FieldOfficerAccountActivatedTemplate.html");
            html = html
                .Replace("{{OFFICER_NAME}}", safeName)
                .Replace("{{OFFICER_EMAIL}}", toEmail)
                .Replace("{{ASSIGNED_REGION}}", safeRegion)
                .Replace("{{ACTIVATED_AT}}", DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"))
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            await SendAsync(toEmail, "FarmBridge Account Activated", html);
        }

        public async Task SendFarmerSlotRescheduledEmailAsync(RescheduleEmailData data)
        {
            if (!CanSend(data.FarmerEmail)) { LogSkip(nameof(SendFarmerSlotRescheduledEmailAsync), data.FarmerEmail); return; }
            var html = await LoadTemplate("FarmerSlotRescheduledTemplate.html");
            html = html
                .Replace("{{REQUEST_ID}}", data.ProcurementRequestId.ToString())
                .Replace("{{FARMER_NAME}}", data.FarmerName)
                .Replace("{{CROP_NAME}}", data.CropName)
                .Replace("{{WAREHOUSE_NAME}}", data.WarehouseName)
                .Replace("{{OLD_SLOT_DATE}}", data.OldSlotDateFormatted)
                .Replace("{{OLD_TIME_RANGE}}", data.OldTimeRange)
                .Replace("{{NEW_SLOT_DATE}}", data.NewSlotDateFormatted)
                .Replace("{{NEW_TIME_RANGE}}", data.NewTimeRange)
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            await SendAsync(data.FarmerEmail, "FarmBridge — Your warehouse slot was rescheduled", html);
        }

        public async Task SendFarmerRequestAcceptedEmailAsync(AcceptEmailData data)
        {
            if (!CanSend(data.FarmerEmail)) { LogSkip(nameof(SendFarmerRequestAcceptedEmailAsync), data.FarmerEmail); return; }
            var html = await LoadTemplate("FarmerRequestAcceptedTemplate.html");
            html = html
                .Replace("{{QC_REQUEST_ID}}", data.ProcurementRequestId.ToString())
                .Replace("{{FARMER_NAME}}", data.FarmerName)
                .Replace("{{CROP_NAME}}", data.CropName)
                .Replace("{{WAREHOUSE_NAME}}", data.WarehouseName)
                .Replace("{{QUANTITY}}", data.QuantityDisplay)
                .Replace("{{SLOT_DATE}}", data.SlotDateFormatted)
                .Replace("{{TIME_RANGE}}", data.TimeRange)
                .Replace("{{ACCEPTED_AT}}", data.AcceptedAtFormatted)
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            await SendAsync(data.FarmerEmail, "FarmBridge — Your procurement request was accepted", html);
        }

        public async Task SendFarmerRequestCancelledEmailAsync(CancelEmailData data)
        {
            if (!CanSend(data.FarmerEmail)) { LogSkip(nameof(SendFarmerRequestCancelledEmailAsync), data.FarmerEmail); return; }
            var html = await LoadTemplate("FarmerRequestCancelledTemplate.html");
            html = html
                .Replace("{{BOOKING_ID}}", data.ProcurementRequestId.ToString())
                .Replace("{{FARMER_NAME}}", data.FarmerName)
                .Replace("{{CROP_NAME}}", data.CropName)
                .Replace("{{WAREHOUSE_NAME}}", data.WarehouseName)
                .Replace("{{SLOT_DATE}}", data.SlotDateDisplay)
                .Replace("{{CANCELLED_AT}}", data.CancelledAtFormatted)
                .Replace("{{CANCEL_REASON}}", data.CancelReason)
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            await SendAsync(data.FarmerEmail, "FarmBridge — Procurement request cancelled", html);
        }

        public async Task SendFarmerInspectionReportEmailAsync(FarmerNotifyInfo farmer, byte[] pdfBytes, int procurementRequestId, string grade, bool passed)
        {
            if (!CanSend(farmer.Email)) { LogSkip(nameof(SendFarmerInspectionReportEmailAsync), farmer.Email); return; }
            var html = await LoadTemplate("FarmerInspectionReportTemplate.html");
            var resultText = passed ? "Passed" : "Did not pass";
            html = html
                .Replace("{{PROCUREMENT_ID}}", procurementRequestId.ToString())
                .Replace("{{FARMER_NAME}}", farmer.FullName)
                .Replace("{{CROP_NAME}}", farmer.CropName)
                .Replace("{{GRADE}}", grade)
                .Replace("{{RESULT}}", resultText)
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            var fileName = $"FarmBridge_Inspection_{procurementRequestId}.pdf";
            await SendAsync(farmer.Email, "FarmBridge — Your quality inspection report", html, pdfBytes, fileName);
        }

        public async Task SendFarmerAdvancePaymentEmailAsync(string toEmail, string farmerName, string cropName, decimal totalAmount, decimal paidAmount, decimal remainingAmount, string? utrReference, int paymentId)
        {
            if (!CanSend(toEmail)) { LogSkip(nameof(SendFarmerAdvancePaymentEmailAsync), toEmail); return; }
            var html = await LoadTemplate("FarmerAdvancePaymentTemplate.html");
            var utr = string.IsNullOrWhiteSpace(utrReference) ? "—" : utrReference;
            var settledAt = DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt");
            html = html
                .Replace("{{FARMER_NAME}}", farmerName)
                .Replace("{{CROP_NAME}}", cropName)
                .Replace("{{AMOUNT}}", paidAmount.ToString("N2"))
                .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
                .Replace("{{PAID_AMOUNT}}", paidAmount.ToString("N2"))
                .Replace("{{REMAINING_AMOUNT}}", remainingAmount.ToString("N2"))
                .Replace("{{PAYMENT_ID}}", paymentId.ToString())
                .Replace("{{UTR_REFERENCE}}", utr)
                .Replace("{{PAYMENT_MODE}}", "Bank transfer")
                .Replace("{{PAYMENT_NUMBER}}", "1 — Advance (30%)")
                .Replace("{{TRIGGER_EVENT}}", "QC passed — advance payment")
                .Replace("{{SETTLED_AT}}", settledAt)
                .Replace("{{PORTAL_URL}}", FarmerPortalUrl);
            await SendAsync(toEmail, "FarmBridge — Advance payment (30%) processed", html);
        }

        public async Task SendOtpEmailAsync(string toEmail, string sixDigitOtp)
        {
            if (!CanSend(toEmail)) { LogSkip(nameof(SendOtpEmailAsync), toEmail); return; }
            var padded = (sixDigitOtp ?? "").PadLeft(6, '0');
            if (padded.Length > 6) padded = padded.Substring(0, 6);
            var html = await LoadTemplate("EmailTemplate.html");
            for (int i = 0; i < 6; i++)
                html = html.Replace("{{D" + (i + 1) + "}}", padded[i].ToString());
            await SendAsync(toEmail, "FarmBridge — Your verification code", html);
        }

        // ── VENDOR EMAILS ──────────────────────────────────────────────

        public async Task SendPasswordEmail(string toEmail, string password, string userName)
        {
            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "PasswordEmailTemplate.html");
            if (!File.Exists(templatePath)) throw new Exception("Template not found");
            var htmlContent = await File.ReadAllTextAsync(templatePath);
            var safeName = string.IsNullOrEmpty(userName) ? "User" : userName;
            htmlContent = htmlContent
                .Replace("{{PASSWORD}}", password)
                .Replace("{{USERNAME}}", safeName)
                .Replace("{{INITIAL}}", safeName.Substring(0, 1).ToUpper())
                .Replace("{{LOGIN_URL}}", "http://localhost:5080/Account/Login");
            
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config["EmailSettings:Username"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = "Your FarmBridge Account Password";
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlContent };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_config["EmailSettings:Host"], int.Parse(_config["EmailSettings:Port"]), false);
            await smtp.AuthenticateAsync(_config["EmailSettings:Username"], _config["EmailSettings:Password"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendOtpEmail(string toEmail, string otp)
        {
            var path = Path.Combine(_env.ContentRootPath, "Templates", "EmailTemplate.html");
            if (!File.Exists(path)) throw new Exception("Template not found");
            var html = await File.ReadAllTextAsync(path);
            otp = otp.PadRight(6, '0');
            html = html
                .Replace("{{D1}}", otp[0].ToString())
                .Replace("{{D2}}", otp[1].ToString())
                .Replace("{{D3}}", otp[2].ToString())
                .Replace("{{D4}}", otp[3].ToString())
                .Replace("{{D5}}", otp[4].ToString())
                .Replace("{{D6}}", otp[5].ToString());

            var fromEmail = _config["EmailSettings:From"] ?? _config["EmailSettings:Username"];
            var host = _config["EmailSettings:Host"];
            var port = int.Parse(_config["EmailSettings:Port"]);

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("FarmBridge", fromEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = "Your OTP Code";
            var builder = new BodyBuilder { HtmlBody = html };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_config["EmailSettings:Username"], _config["EmailSettings:Password"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        // ── VENDOR ORDER EMAILS ────────────────────────────────────────

        public async Task SendVendorOrderCancelledEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string cancelReason)
        {
            if (!CanSend(toEmail)) return;
            var html = await LoadTemplate("VendorOrderCancelledTemplate.html");
            html = html
                .Replace("{{ORDER_ID}}", orderId.ToString())
                .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
                .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
                .Replace("{{CANCELLED_AT}}", DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt"))
                .Replace("{{CANCEL_REASON}}", cancelReason ?? "No reason provided")
                .Replace("{{PORTAL_URL}}", "http://localhost:5080"); 
            await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Cancelled", html);
        }

        public async Task SendVendorOrderSuccessEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string deliveryAddress, string preferredDate, string status)
        {
            if (!CanSend(toEmail)) return;
            var html = await LoadTemplate("VendorOrderSuccessTemplate.html");
            html = html
                .Replace("{{ORDER_ID}}", orderId.ToString())
                .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
                .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
                .Replace("{{DELIVERY_ADDRESS}}", deliveryAddress ?? "Registered Address")
                .Replace("{{PREFERRED_DATE}}", preferredDate ?? "Standard Delivery")
                .Replace("{{STATUS}}", status ?? "Confirmed")
                .Replace("{{PORTAL_URL}}", "http://localhost:5080"); 
            await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Confirmed", html);
        }

        public async Task SendVendorOrderRescheduledEmailAsync(string toEmail, string vendorName, int orderId, string oldDate, string newDate, string deliveryAddress, string reason)
        {
            if (!CanSend(toEmail)) return;
            var html = await LoadTemplate("VendorOrderRescheduledTemplate.html");
            html = html
                .Replace("{{ORDER_ID}}", orderId.ToString())
                .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
                .Replace("{{OLD_DATE}}", oldDate ?? "—")
                .Replace("{{NEW_DATE}}", newDate ?? "—")
                .Replace("{{DELIVERY_ADDRESS}}", deliveryAddress ?? "Registered Address")
                .Replace("{{RESCHEDULE_REASON}}", reason ?? "Logistics update")
                .Replace("{{PORTAL_URL}}", "http://localhost:5080");
            await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Rescheduled", html);
        }

        public async Task SendVendorOrderDeliveredEmailAsync(string toEmail, string vendorName, int orderId, decimal totalAmount, string deliveredAt, string logisticsPartner, string trackingNumber)
        {
            if (!CanSend(toEmail)) return;
            var html = await LoadTemplate("VendorOrderDeliveredTemplate.html");
            html = html
                .Replace("{{ORDER_ID}}", orderId.ToString())
                .Replace("{{VENDOR_NAME}}", vendorName ?? "Vendor")
                .Replace("{{TOTAL_AMOUNT}}", totalAmount.ToString("N2"))
                .Replace("{{DELIVERED_AT}}", deliveredAt ?? DateTime.Now.ToString("MMM dd, yyyy"))
                .Replace("{{LOGISTICS_PARTNER}}", logisticsPartner ?? "FarmBridge Logistics")
                .Replace("{{TRACKING_NUMBER}}", trackingNumber ?? "—")
                .Replace("{{PORTAL_URL}}", "http://localhost:5080");
            await SendAsync(toEmail, $"FarmBridge — Order #{orderId} Delivered!", html);
        }

        // ── VENDOR ACCOUNT EMAILS (Registration & Approval) ─────────────────────────

        public async Task SendVendorWelcomeEmailAsync(string toEmail, string businessName)
        {
            if (!CanSend(toEmail)) return;
            
            // ✅ NOW LOADS THE REAL HTML FILE!
            var html = await LoadTemplate("VendorWelcomeTemplate.html");
            html = html
                .Replace("{{BUSINESS_NAME}}", businessName ?? "Vendor")
                .Replace("{{PORTAL_URL}}", "http://localhost:5080");
                
            await SendAsync(toEmail, "Welcome to FarmBridge — Registration Received", html);
        }

        public async Task SendVendorApprovedEmailAsync(string toEmail, string businessName)
        {
            if (!CanSend(toEmail)) return;

            // ✅ NOW LOADS THE REAL HTML FILE!
            var html = await LoadTemplate("VendorApprovedTemplate.html");
            html = html
                .Replace("{{BUSINESS_NAME}}", businessName ?? "Vendor")
                .Replace("{{PORTAL_URL}}", "http://localhost:5080");
                
            await SendAsync(toEmail, "FarmBridge — Account Approved!", html);
        }
    }
}