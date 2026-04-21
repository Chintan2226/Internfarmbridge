using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using API.BAL;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationHelper _notificationHelper;

        public NotificationController(NotificationHelper notificationHelper)
        {
            _notificationHelper = notificationHelper;
        }

        private int GetCurrentUserId()
        {
            // Try multiple claim types for flexibility
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value
                        ?? User.FindFirst("userId")?.Value;

            return int.TryParse(claim, out var id) ? id : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value?.ToLower()
                   ?? User.FindFirst("role")?.Value?.ToLower()
                   ?? "";
        }

        [HttpGet("GetNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not found" });

            var notifications = await _notificationHelper.GetUserNotificationsAsync(userId);
            return Ok(new { success = true, data = notifications });
        }

        [HttpGet("UnreadCount")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var count = await _notificationHelper.GetUnreadCountAsync(userId);
            return Ok(new { success = true, count });
        }

        [HttpPost("MarkAllRead")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            await _notificationHelper.MarkAllAsReadAsync(userId);
            return Ok(new { success = true, message = "All notifications marked as read" });
        }

        [HttpPost("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead([FromBody] int notificationId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (notificationId <= 0)
                return BadRequest(new { success = false, message = "Invalid notification ID" });

            await _notificationHelper.MarkAsReadAsync(userId, notificationId);
            return Ok(new { success = true, message = "Notification marked as read" });
        }

        // Add this endpoint for compatibility with your frontend
        [HttpPost("DeleteNotification")]
        public async Task<IActionResult> DeleteNotification([FromBody] int notificationId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (notificationId <= 0)
                return BadRequest(new { success = false, message = "Invalid notification ID" });

            await _notificationHelper.DeleteOneAsync(userId, notificationId);
            return Ok(new { success = true, message = "Notification deleted" });
        }

        [HttpPost("ClearAllNotifications")]
        public async Task<IActionResult> ClearAllNotifications()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            await _notificationHelper.DeleteAllAsync(userId);
            return Ok(new { success = true, message = "All notifications cleared" });
        }


    }
}