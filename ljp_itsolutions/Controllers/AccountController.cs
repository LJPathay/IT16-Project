using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ljp_itsolutions.Controllers
{
    public class AccountController : BaseController
    {
        private readonly ljp_itsolutions.Services.IEmailSender _emailSender;
        private readonly ljp_itsolutions.Services.IPhotoService _photoService;
        private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<ljp_itsolutions.Models.User> _hasher;

        public AccountController(
            ljp_itsolutions.Data.ApplicationDbContext db, 
            ljp_itsolutions.Services.IEmailSender emailSender, 
            ljp_itsolutions.Services.IPhotoService photoService,
            Microsoft.AspNetCore.Identity.IPasswordHasher<ljp_itsolutions.Models.User> hasher)
            : base(db)
        {
            _emailSender = emailSender;
            _photoService = photoService;
            _hasher = hasher;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var roleName = User.FindFirstValue(ClaimTypes.Role);
                if (string.Equals(roleName, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "SuperAdmin");
                if (string.Equals(roleName, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "Admin");
                if (string.Equals(roleName, UserRoles.Manager, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "Manager");
                if (string.Equals(roleName, UserRoles.Cashier, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Index", "POS");
                if (string.Equals(roleName, UserRoles.MarketingStaff, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "Marketing");

                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
 
            if (!ModelState.IsValid)
                return View(model);
 
            ljp_itsolutions.Models.User? user = null;
            if (!string.IsNullOrWhiteSpace(model.UsernameOrEmail))
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);
            }
 
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View(model);
            }
 
            // Check for lockout
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                var remainingMinutes = (int)Math.Ceiling((user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes);
                ModelState.AddModelError(string.Empty, $"Account is temporarily locked. Please try again in {remainingMinutes} minutes.");
                return View(model);
            }
 
            if (string.IsNullOrEmpty(user.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View(model);
            }
 
            var verify = _hasher.VerifyHashedPassword(user, user.Password, model.Password);
            if (verify == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
                    ModelState.AddModelError(string.Empty, "Account is temporarily locked due to too many failed attempts. Please try again in 5 minutes.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid credentials");
                }
                
                await _db.SaveChangesAsync();
                await LogAudit("Failed Login Attempt", null, user.UserID);
                return View(model);
            }
 
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Account is deactivated. Please contact administrator.");
                return View(model);
            }
 
            // Reset lockout on success
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await _db.SaveChangesAsync();
 
            var roleName = user.Role;
 
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Role, roleName)
            };
 
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            await LogAudit("User Login", null, user.UserID);
 
            // Set session variables for layout consistency
            HttpContext.Session.SetString("UserRole", roleName);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("IsHighContrast", user.IsHighContrast.ToString().ToLower());
            HttpContext.Session.SetString("FontSize", user.FontSize);
            HttpContext.Session.SetString("ReduceMotion", user.ReduceMotion.ToString().ToLower());
            HttpContext.Session.SetString("ProfilePictureUrl", user.ProfilePictureUrl ?? "");
 
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
 
            if (string.Equals(roleName, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard", "SuperAdmin");
            if (string.Equals(roleName, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard", "Admin");
            if (string.Equals(roleName, UserRoles.Manager, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard", "Manager");
            if (string.Equals(roleName, UserRoles.Cashier, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "POS");
            if (string.Equals(roleName, UserRoles.MarketingStaff, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard", "Marketing");
 
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPasswordSubmit(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Message"] = "Username is required.";
                return RedirectToAction("Login");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username || u.Email == username);
            if (user == null)
            {
                // To prevent user enumeration, we still show the same message
                TempData["Message"] = "If an account is associated with that username, instructions were sent.";
                return RedirectToAction("Login");
            }

            var token = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();

            var callback = Url.Action("ResetPassword", "Account", new { userId = user.UserID, token = token }, protocol: Request.Scheme);
            await _emailSender.SendEmailAsync(user.Email ?? string.Empty, "Password Reset Request - Coffee ERP", 
                $"Click here to reset your password: <a href='{callback}'>Reset Link</a>. This link expires in 1 hour.");

            TempData["Message"] = "If an account is associated with that username, instructions were sent.";
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            var model = new ljp_itsolutions.Models.ResetPasswordViewModel { UserId = userId, Token = token };
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ljp_itsolutions.Models.ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!Guid.TryParse(model.UserId, out var guid))
            {
                ModelState.AddModelError(string.Empty, "Invalid request.");
                return View(model);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == guid && u.PasswordResetToken == model.Token);

            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired token.");
                return View(model);
            }

            // Complexity Check
            if (model.NewPassword.Length < 8 || !model.NewPassword.Any(char.IsUpper) || !model.NewPassword.Any(char.IsDigit))
            {
                ModelState.AddModelError(string.Empty, "Password must be at least 8 characters long and contain at least one uppercase letter and one number.");
                return View(model);
            }

            user.Password = _hasher.HashPassword(user, model.NewPassword);
            user.PasswordResetToken = null;
            user.ResetTokenExpiry = null;
            user.AccessFailedCount = 0; // Reset lockout on password reset too
            user.LockoutEnd = null;

            await _db.SaveChangesAsync();
            await LogAudit("Password Reset Success", $"User: {user.Username} (Manual Reset via Email)");

            TempData["Message"] = "Success! Password has been updated.";
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await LogAudit("User Logout");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            return View(user);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = GetCurrentUserId();
            var userRole = HttpContext.Session.GetString("UserRole") ?? User.FindFirstValue(ClaimTypes.Role) ?? "-";

            var query = _db.Notifications
                .Where(n => n.UserID == userId || (n.UserID == null && (
                    (userRole == "Manager" && (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order"))) ||
                    (userRole == "MarketingStaff" && (n.Title.Contains("Approved") || n.Title.Contains("Rejected"))) ||
                    ((userRole == "Admin" || userRole == "SuperAdmin") && 
                     (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order") || n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                )))
                .OrderByDescending(n => n.CreatedAt);

            var notifications = await query.ToListAsync();
            return View(notifications);
        }

        [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string email, string profilePictureUrl, IFormFile? profilePictureFile)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound();

        // Handle File Upload to Cloudinary
        if (profilePictureFile != null)
        {
            var uploadResult = await _photoService.AddPhotoAsync(profilePictureFile);
            if (uploadResult.Error == null)
            {
                profilePictureUrl = uploadResult.SecureUrl.AbsoluteUri;
            }
            else
            {
                TempData["Error"] = "Cloudinary Upload Failed: " + uploadResult.Error.Message;
            }
        }

        user.FullName = fullName;
        user.Email = email;
        user.ProfilePictureUrl = profilePictureUrl;
        await _db.SaveChangesAsync();

        // Refresh session
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("ProfilePictureUrl", user.ProfilePictureUrl ?? "");
        
        TempData["SuccessMessage"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(bool isHighContrast, string fontSize, bool reduceMotion)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.IsHighContrast = isHighContrast;
            user.FontSize = fontSize;
            user.ReduceMotion = reduceMotion;
            await _db.SaveChangesAsync();

            // Update Session
            HttpContext.Session.SetString("IsHighContrast", user.IsHighContrast.ToString().ToLower());
            HttpContext.Session.SetString("FontSize", user.FontSize);
            HttpContext.Session.SetString("ReduceMotion", user.ReduceMotion.ToString().ToLower());

            TempData["SuccessMessage"] = "Accessibility settings saved.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide all required fields correctly.";
                return RedirectToAction(nameof(Profile));
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction(nameof(Profile));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            var verify = _hasher.VerifyHashedPassword(user, user.Password ?? "", model.CurrentPassword);
            if (verify == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Incorrect current password.";
                return RedirectToAction(nameof(Profile));
            }
            if (model.NewPassword.Length < 8 || !model.NewPassword.Any(char.IsUpper) || !model.NewPassword.Any(char.IsDigit))
            {
                TempData["Error"] = "Password must be at least 8 characters long and contain at least one uppercase letter and one number.";
                return RedirectToAction(nameof(Profile));
            }

            user.Password = _hasher.HashPassword(user, model.NewPassword);
            _db.Users.Update(user);
            var result = await _db.SaveChangesAsync();

            if (result > 0)
            {
                await LogAudit("Self-Service Password Change", null, user.UserID);
                TempData["SuccessMessage"] = "Password updated successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to update password in database.";
            }

            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = GetCurrentUserId();
            var userRole = HttpContext.Session.GetString("UserRole") ?? User.FindFirstValue(ClaimTypes.Role) ?? "-";

            var unread = await _db.Notifications.Where(n => !n.IsRead && (
                n.UserID == userId || (n.UserID == null && (
                    (userRole == "Manager" && (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order"))) ||
                    (userRole == "MarketingStaff" && (n.Title.Contains("Approved") || n.Title.Contains("Rejected"))) ||
                    ((userRole == "Admin" || userRole == "SuperAdmin") && 
                     (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order") || n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                ))
            )).ToListAsync();

            foreach (var n in unread) n.IsRead = true;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            var userRole = HttpContext.Session.GetString("UserRole") ?? User.FindFirstValue(ClaimTypes.Role) ?? "-";

            var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationID == id && (
                n.UserID == userId || (n.UserID == null && (
                    (userRole == "Manager" && (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order"))) ||
                    (userRole == "MarketingStaff" && (n.Title.Contains("Approved") || n.Title.Contains("Rejected"))) ||
                    ((userRole == "Admin" || userRole == "SuperAdmin") && 
                     (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order") || n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                ))
            ));

            if (notification != null)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }
            return Ok();
        }


    }
}
