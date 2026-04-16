using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public class SuperAdminController : BaseController
    {
        private readonly IPasswordHasher<ljp_itsolutions.Models.User> _hasher;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAnalyticsService _analyticsService;

        public SuperAdminController(ljp_itsolutions.Data.ApplicationDbContext db, IPasswordHasher<ljp_itsolutions.Models.User> hasher, IReceiptService receiptService, IServiceScopeFactory scopeFactory, IAnalyticsService analyticsService)
            : base(db)
        {
            _hasher = hasher;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
            _analyticsService = analyticsService;
        }



        public async Task<IActionResult> Dashboard()
        {
            var data = await _analyticsService.GetSuperAdminDashboardDataAsync();
            return View(data);
        }


        // --- User Management ---
        public async Task<IActionResult> Users(bool showArchived = false)
        {
            var query = _db.Users.AsQueryable();
            if (showArchived) query = query.Where(u => !u.IsActive);
            else query = query.Where(u => u.IsActive);

            var users = query.OrderByDescending(u => u.CreatedAt).ToList();
            
            // SECURITY: Log that an admin is viewing PII/sensitive user data
            await LogAudit("Accessed protected personnel list (PII View)");
            
            ViewBag.Roles = UserRoles.AllExceptSuper;
            ViewBag.ShowArchived = showArchived;
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Username))
                return BadRequest("Username is required.");

            if (_db.Users.Any(u => u.Username == user.Username))
            {
                return BadRequest("Username already exists.");
            }

            // SECURITY: Prevent creating another SuperAdmin
            if (user.Role == UserRoles.SuperAdmin)
            {
                await LogSecurity("AbnormalActivity", $"Unauthorized attempt to create SuperAdmin by {User.Identity?.Name}", "Critical");
                return Forbid("Unauthorized role assignment.");
            }

            var rawPassword = string.IsNullOrEmpty(user.Password) ? "Default123!@#$Initial" : user.Password;
            if (!ValidatePasswordComplexity(rawPassword, user, out string error))
            {
                return BadRequest(error);
            }

            user.UserID = Guid.NewGuid();
            user.Password = _hasher.HashPassword(user, rawPassword);
            user.CreatedAt = DateTime.UtcNow;
            user.LastPasswordChange = DateTime.UtcNow;
            user.RequiresPasswordChange = true; // Force change on first login
            user.IsActive = true;

            _db.Users.Add(user);
            
            // Record initial password in history to prevent immediate reuse after forced change
            _db.UserPasswordHistories.Add(new UserPasswordHistory
            {
                UserID = user.UserID,
                PasswordHash = user.Password,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            await LogSecurity("UserCreated", $"Created user: {user.Username} as {user.Role}", "Info", user.UserID);
            
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User updatedUser)
        {
            var user = await _db.Users.FindAsync(updatedUser.UserID);
            if (user == null) return NotFound();

            if (user.Role == UserRoles.SuperAdmin || updatedUser.Role == UserRoles.SuperAdmin)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Restricted access: SuperAdmin accounts cannot be modified here.";
                return RedirectToAction(AppConstants.Actions.Users);
            }

            user.FullName = updatedUser.FullName;
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            user.Role = updatedUser.Role;
            
            // If reactivating, clear security lockouts
            if (!user.IsActive && updatedUser.IsActive)
            {
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
            }
            
            user.IsActive = updatedUser.IsActive;
            await _db.SaveChangesAsync();
            await LogSecurity("UserUpdated", $"Updated user: {user.Username}", "Info", user.UserID);
            TempData[AppConstants.SessionKeys.SuccessMessage] = "User updated successfully.";
            return RedirectToAction(AppConstants.Actions.Users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(Guid userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.Role == UserRoles.SuperAdmin)
            {
                return BadRequest("SuperAdmin passwords cannot be reset here.");
            }

            // Realistic "Forgot/Force Change" policy
            // Instead of setting a password, we generate a reset token and force a change
            user.RequiresPasswordChange = true;
            user.PasswordResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            await _db.SaveChangesAsync();
            await LogSecurity("AdminPasswordResetTriggered", $"SuperAdmin {User.Identity?.Name} triggered a password reset for {user.Username}", "Warning", user.UserID);

            return Ok(new { message = "Password reset has been triggered. The user will be forced to change their password on next login." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction(AppConstants.Actions.Users);
            
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                if (user.Role == UserRoles.SuperAdmin)
                {
                    TempData[AppConstants.SessionKeys.ErrorMessage] = "SuperAdmin accounts cannot be archived.";
                    return RedirectToAction(AppConstants.Actions.Users);
                }

                var archivedUser = new ArchivedUser
                {
                    OriginalUserID = user.UserID,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email ?? "",
                    Role = user.Role,
                    ArchivedAt = DateTime.UtcNow,
                    Reason = "User requested archive"
                };

                _db.ArchivedUsers.Add(archivedUser);
                _db.Users.Remove(user);
                
                await _db.SaveChangesAsync();
                await LogSecurity("UserArchived", $"Archived user: {user.Username}", "Warning", user.UserID);
                TempData[AppConstants.SessionKeys.SuccessMessage] = "User moved to archives successfully.";
            }
            return RedirectToAction(AppConstants.Actions.Users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction(AppConstants.Actions.Users);
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                user.IsActive = true;
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
                await _db.SaveChangesAsync();
                await LogSecurity("UserRestored", $"Restored user: {user.Username}", "Info", user.UserID);
                TempData[AppConstants.SessionKeys.SuccessMessage] = "User restored successfully.";
            }
            return RedirectToAction(AppConstants.Actions.Users, new { showArchived = true });
        }

        // --- Audit Logs ---
        public IActionResult AuditLogs()
        {
            var logs = _db.AuditLogs.Include(a => a.User).OrderByDescending(a => a.Timestamp).ToList();
            ViewBag.Users = _db.Users.OrderBy(u => u.FullName).Select(u => new { u.UserID, u.FullName }).ToList();
            return View(logs);
        }

        // --- System Settings ---
        public IActionResult SystemSettings()
        {
            var settings = _db.SystemSettings.ToDictionary(s => s.SettingKey, s => s.SettingValue);
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(IFormCollection form)
        {
            try {
                var keys = new[] { 
                    "SystemName", "Timezone", "Currency", "DateFormat", 
                    "SessionTimeout", "PasswordMinLength", "RequireSpecialChars", "RequireNumbers", "TwoFactorAuth",
                    "CompanyName", "TaxRate", "LowStockThreshold", "CriticalStockThreshold", "SystemTagline",
                    "SmtpServer", "SmtpPort", "EmailNotifications", "LowStockAlerts", "DailyReports",
                    "MaintenanceMode", "LogRetentionDays", "AllowPublicRegistration", "AllowPaymentBypass"
                };

                var toggleKeys = new HashSet<string> { "MaintenanceMode", "TwoFactorAuth", "RequireNumbers", "RequireSpecialChars", "EmailNotifications", "LowStockAlerts", "DailyReports", "AllowPublicRegistration", "AllowPaymentBypass" };

                // Handle regular keys
                foreach (var key in keys) {
                    string value = GetSettingValueFromForm(form, key, toggleKeys);
                    await UpdateSingleSetting(key, value);
                }

                // Handle Role Maintenance List
                await UpdateMaintenanceRoles(form);

                await _db.SaveChangesAsync();
                await LogAudit("Updated system settings");
                TempData[AppConstants.SessionKeys.SuccessMessage] = "System configuration updated successfully.";
            } catch (Exception) { 
                TempData[AppConstants.SessionKeys.ErrorMessage] = "A system error occurred while updating settings. Please contact your administrator."; 
            }
            return RedirectToAction(AppConstants.Actions.SystemSettings);
        }

        private static string GetSettingValueFromForm(IFormCollection form, string key, HashSet<string> toggleKeys)
        {
            if (!form.ContainsKey(key))
            {
                return toggleKeys.Contains(key) ? "false" : string.Empty;
            }
            string rawValue = form[key].ToString();
            if (rawValue == "on" || rawValue == "true") return "true";
            return rawValue;
        }

        private async Task UpdateSingleSetting(string key, string value)
        {
            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null) {
                _db.SystemSettings.Add(new SystemSetting { SettingKey = key, SettingValue = value });
                await LogAudit($"Created system setting: {key} = {value}");
            } else if (setting.SettingValue != value) {
                var oldValue = setting.SettingValue;
                setting.SettingValue = value;
                await LogAudit($"Updated system setting: {key}", $"Changed from '{oldValue}' to '{value}'");
            }
        }

        private async Task UpdateMaintenanceRoles(IFormCollection form)
        {
            var maintRolesValue = string.Join(",", form.Keys.Where(k => k.StartsWith("MaintRole_") && form[k] == "true").Select(k => k.Replace("MaintRole_", "")));
            var maintSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "MaintenanceRoles");
            if (maintSetting == null) {
                _db.SystemSettings.Add(new SystemSetting { SettingKey = "MaintenanceRoles", SettingValue = maintRolesValue });
            } else {
                maintSetting.SettingValue = maintRolesValue;
            }
        }

        // --- Backups ---
        public IActionResult Backups()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var files = Directory.GetFiles(path, "*.json").Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTime).ToList();
            ViewBag.BackupSchedule = _db.SystemSettings.FirstOrDefault(s => s.SettingKey == "BackupSchedule")?.SettingValue ?? "Manual";
            return View(files);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBackupSchedule(string schedule)
        {
            try {
                var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "BackupSchedule");
                if (setting == null) {
                    _db.SystemSettings.Add(new SystemSetting { SettingKey = "BackupSchedule", SettingValue = schedule });
                } else {
                    setting.SettingValue = schedule;
                }
                await _db.SaveChangesAsync();
                await LogAudit($"Updated automated backup schedule to: {schedule}");
                TempData[AppConstants.SessionKeys.SuccessMessage] = $"Automated backup schedule updated to: {schedule}";
            } catch (Exception) { 
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Failed to update schedule. Please contact your administrator."; 
            }
            return RedirectToAction(AppConstants.Actions.Backups);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBackup()
        {
            try {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                var fileName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var fullPath = Path.Combine(path, fileName);
                var backupData = new {
                    GeneratedAt = DateTime.UtcNow,
                    Users = await _db.Users.AsNoTracking().ToListAsync(),
                    Products = await _db.Products.AsNoTracking().ToListAsync(),
                    Orders = await _db.Orders.AsNoTracking().Include(o => o.OrderDetails).ToListAsync(),
                    Settings = await _db.SystemSettings.AsNoTracking().ToListAsync()
                };

                // Algorithm: JSON Serialization Algorithm
                var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
                await System.IO.File.WriteAllTextAsync(fullPath, json);
                await LogAudit("Created system backup");
                TempData[AppConstants.SessionKeys.SuccessMessage] = "Backup created.";
            } catch (Exception) { 
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Backup generation failed. Please contact your administrator."; 
            }
            return RedirectToAction(AppConstants.Actions.Backups);
        }

        [HttpGet]
        public IActionResult DownloadBackup(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return BadRequest();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups", fileName);
            if (!System.IO.File.Exists(path)) return NotFound();

            var bytes = System.IO.File.ReadAllBytes(path);
            return File(bytes, "application/json", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBackup(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return BadRequest();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups", fileName);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                await LogAudit($"Deleted backup snapshot: {fileName}");
                TempData[AppConstants.SessionKeys.SuccessMessage] = "Snapshot deleted successfully.";
            }
            return RedirectToAction(AppConstants.Actions.Backups);
        }
        // --- Security Logs ---
        public IActionResult SecurityLogs()
        {
            var logs = _db.SecurityLogs.Include(s => s.User).OrderByDescending(s => s.Timestamp).ToList();
            return View(logs);
        }


    }
}
