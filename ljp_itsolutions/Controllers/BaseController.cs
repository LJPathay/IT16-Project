using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ApplicationDbContext _db;

        protected BaseController(ApplicationDbContext db)
        {
            _db = db;
        }
        /// Audit Logging
        protected async Task LogAudit(string action, string? details = null, Guid? userId = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    UserID = userId ?? GetCurrentUserId(),
                    IpAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext?.Request?.Headers.UserAgent.ToString()
                };
                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch { /* Fail silently */ }
        }

        protected async Task LogSecurity(string eventType, string description, string severity = "Info", Guid? userId = null)
        {
            try
            {
                var securityLog = new SecurityLog
                {
                    EventType = eventType,
                    Description = description,
                    Severity = severity,
                    Timestamp = DateTime.UtcNow,
                    UserID = userId ?? GetCurrentUserId(),
                    IpAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext?.Request?.Headers.UserAgent.ToString()
                };
                _db.SecurityLogs.Add(securityLog);
                await _db.SaveChangesAsync();
            }
            catch { /* Fail silently */ }
        }

        protected string GetSetting(string key, string defaultVal = "")
        {
            return _db.SystemSettings.FirstOrDefault(s => s.SettingKey == key)?.SettingValue ?? defaultVal;
        }

        protected bool GetBoolSetting(string key)
        {
            var val = GetSetting(key);
            return !string.IsNullOrEmpty(val) && 
                   (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(val, "on", StringComparison.OrdinalIgnoreCase));
        }

        protected Guid? GetCurrentUserId()
        {
            if (User.Identity?.IsAuthenticated != true) return null;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdStr, out var userId)) return userId;

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                return _db.Users.FirstOrDefault(u => u.Username == username)?.UserID;
            }
            return null;
        }
    }
}
