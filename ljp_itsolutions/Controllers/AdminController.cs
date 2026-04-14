using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")] 
    public class AdminController : BaseController
    {
        private readonly IPasswordHasher<ljp_itsolutions.Models.User> _hasher;
        private readonly IInventoryService _inventoryService;
        private readonly IAnalyticsService _analyticsService;

        public AdminController(ljp_itsolutions.Data.ApplicationDbContext db, 
            IPasswordHasher<ljp_itsolutions.Models.User> hasher,
            IInventoryService inventoryService, IAnalyticsService analyticsService)
            : base(db)
        {
            _hasher = hasher;
            _inventoryService = inventoryService;
            _analyticsService = analyticsService;
        }



        [HttpGet]
        public IActionResult Users(bool showArchived = false)
        {
            var query = _db.Users.AsQueryable();
            
            query = query.Where(u => u.Role != "SuperAdmin");

            if (showArchived)
                query = query.Where(u => !u.IsActive);
            else
                query = query.Where(u => u.IsActive);

            var users = query.OrderByDescending(u => u.CreatedAt).ToList();
            ViewBag.Roles = UserRoles.AllExceptSuper;
            ViewBag.ShowArchived = showArchived;
            return View(users);
        }

        [HttpGet]
        public IActionResult ManageUsers() => RedirectToAction(AppConstants.Actions.Users);

        public IActionResult Index()
        {
            return RedirectToAction(AppConstants.Actions.Dashboard);
        }

        public async Task<IActionResult> Reports()
        {
            var data = await _analyticsService.GetAdminReportsDataAsync();
            return View(data);
        }

        public async Task<IActionResult> Reports_Cashier()
        {
            var data = await _analyticsService.GetAdminCashierReportsDataAsync();
            return View(AppConstants.Actions.Reports_Cashier, data);
        }

        public async Task<IActionResult> Reports_Marketing()
        {
            var data = await _analyticsService.GetAdminMarketingReportsDataAsync();
            return View("Reports_Marketing", data);
        }

        public async Task<IActionResult> Reports_Manager()
        {
            var data = await _analyticsService.GetAdminManagerReportsDataAsync();
            return View("Reports_Manager", data);
        }

        public async Task<IActionResult> Dashboard()
        {
            var data = await _analyticsService.GetAdminDashboardDataAsync();
            return View(data);
        }

        public IActionResult Transactions()
        {
            var transactions = _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .ToList();
            return View(transactions);
        }

        public IActionResult InventoryOverview(bool showArchived = false)
        {
            var query = _db.Ingredients.AsQueryable();
            
            if (showArchived)
                query = query.Where(i => i.IsArchived);
            else
                query = query.Where(i => !i.IsArchived);

            var ingredients = query.OrderBy(i => i.Name).ToList();

            ViewBag.LowStockCount = _db.Ingredients.Count(i => !i.IsArchived && i.StockQuantity < i.LowStockThreshold && i.StockQuantity > 0);
            ViewBag.OutOfStockCount = _db.Ingredients.Count(i => !i.IsArchived && i.StockQuantity <= 0);
            ViewBag.TotalCount = ingredients.Count;
            ViewBag.ShowArchived = showArchived;

            return View(ingredients);
        }

        public IActionResult AuditLogs()
        {
            var logs = _db.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(200)
                .ToList();
            return View(logs);
        }

        public IActionResult SecurityLogs()
        {
            var logs = _db.SecurityLogs
                .Include(s => s.User)
                .OrderByDescending(s => s.Timestamp)
                .Take(200)
                .ToList();
            return View(logs);
        }

        // API Endpoints for Modals
        [HttpGet]
        public async Task<IActionResult> GetUser(string id)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var guidId)) return BadRequest();
            
            var user = await _db.Users
                .Where(u => u.UserID == guidId)
                .Select(u => new 
                {
                    u.UserID,
                    u.Username,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt,
                    u.ProfilePictureUrl
                })
                .FirstOrDefaultAsync();
                
            if (user == null) return NotFound();
            return Json(user);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .Where(o => o.OrderID == id)
                .Select(o => new
                {
                    o.OrderID,
                    OrderDate = o.OrderDate.ToString("MMM dd, yyyy HH:mm"),
                    CustomerName = o.Customer != null ? o.Customer.FullName : "Walk-in Customer",
                    CashierName = o.Cashier != null ? o.Cashier.FullName : "N/A",
                    o.PaymentMethod,
                    o.PaymentStatus,
                    TotalAmount = o.FinalAmount,
                    Items = o.OrderDetails.Select(od => new
                    {
                        ProductName = od.Product.Name,
                        od.Quantity,
                        od.UnitPrice,
                        Total = od.Quantity * od.UnitPrice
                    })
                })
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();
            return Json(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Creating a User
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Username))
                return BadRequest("Username is required.");

            if (_db.Users.Any(u => u.Username == user.Username))
                return BadRequest("Username already exists.");

            // Prevent creating a SuperAdmin from this endpoint
            if (user.Role == UserRoles.SuperAdmin)
            {
                await LogSecurity("AbnormalActivity", $"Admin {User.Identity?.Name} attempted to create a SuperAdmin", "Critical");
                return Forbid("Admin cannot create a SuperAdmin account.");
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
            await _db.SaveChangesAsync();

            await LogSecurity("UserCreated", "Created User: " + user.Username, "Info", user.UserID);

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Edit User
        public async Task<IActionResult> EditUser([FromBody] JsonElement data)
        {
            try
            {
                var jsonId = data.GetProperty("UserID").GetString();
                if (string.IsNullOrEmpty(jsonId)) return BadRequest("Invalid UserID");
                var id = Guid.Parse(jsonId);
                var existingUser = await _db.Users.FindAsync(id);
                if (existingUser == null) return NotFound();

                if (existingUser.Role == "SuperAdmin") return Forbid();

                existingUser.FullName = data.GetProperty("FullName").GetString() ?? existingUser.FullName;
                existingUser.Email = data.GetProperty("Email").GetString() ?? existingUser.Email;
                
                string newRole = data.GetProperty("Role").GetString() ?? existingUser.Role;
                // SECURITY: Prevent promotion to SuperAdmin
                if (newRole == UserRoles.SuperAdmin && existingUser.Role != UserRoles.SuperAdmin)
                {
                    return Forbid("Cannot promote a user to SuperAdmin.");
                }
                existingUser.Role = newRole;

                _db.Users.Update(existingUser);
                await _db.SaveChangesAsync();
                await LogSecurity("UserUpdated", "Updated User: " + existingUser.Username, "Info", existingUser.UserID);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(Guid id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role == "SuperAdmin") return Forbid();

            user.IsActive = !user.IsActive;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            await LogSecurity("UserStatusToggled", (user.IsActive ? "Restored" : "Archived") + " User: " + user.Username, "Warning", user.UserID);

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleIngredientArchive(int id)
        {
            var ingredient = await _db.Ingredients.FindAsync(id);
            if (ingredient == null) return NotFound();

            ingredient.IsArchived = !ingredient.IsArchived;
            await _db.SaveChangesAsync();
            await LogAudit((ingredient.IsArchived ? "Archived" : "Restored") + " Ingredient: " + ingredient.Name);

            return Ok();
        }
        // Inventory Actions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIngredient([FromBody] Ingredient ingredient)
        {
            if (string.IsNullOrEmpty(ingredient.Name)) return BadRequest("Name is required.");

            _db.Ingredients.Add(ingredient);
            await _db.SaveChangesAsync();

            if (ingredient.StockQuantity > 0)
            {
                ingredient.LastStockedDate = DateTime.UtcNow;
                _db.InventoryLogs.Add(new InventoryLog
                {
                    IngredientID = ingredient.IngredientID,
                    QuantityChange = ingredient.StockQuantity,
                    ChangeType = "Initial",
                    LogDate = DateTime.UtcNow,
                    Remarks = "Initial stock upon ingredient creation"
                });
                await _db.SaveChangesAsync();
            }

            await LogAudit("Added Ingredient: " + ingredient.Name);
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IntakeStock(int IngredientID, decimal Quantity, string Remarks, DateTime? IntakeDate, DateTime? ExpiryDate)
        {
            if (!ModelState.IsValid)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Invalid data submitted.";
                return RedirectToAction(AppConstants.Actions.InventoryOverview);
            }

            if (Quantity <= 0)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Intake quantity must be greater than zero.";
                return RedirectToAction(AppConstants.Actions.InventoryOverview);
            }

            var actualDate = IntakeDate ?? DateTime.UtcNow;
            string logRemarks = string.IsNullOrEmpty(Remarks) ? "Manual stock intake (Admin)" : Remarks;
            if (ExpiryDate.HasValue) logRemarks += $" (Expiry: {ExpiryDate.Value:yyyy-MM-dd})";

            var success = await _inventoryService.IntakeStockAsync(IngredientID, Quantity, actualDate, logRemarks, ExpiryDate);
            if (!success) return NotFound();

            var ingredient = await _db.Ingredients.FindAsync(IngredientID);
            if (ingredient != null)
            {
                await LogAudit($"Stock Intake (Admin): Added {Quantity} {ingredient.Unit} to {ingredient.Name} on {actualDate:yyyy-MM-dd}");
                TempData[AppConstants.SessionKeys.SuccessMessage] = $"Stock updated! Added {Quantity} {ingredient.Unit} to {ingredient.Name}.";
            }
            
            return RedirectToAction(AppConstants.Actions.InventoryOverview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int id, decimal quantity, decimal? threshold, DateTime? expiryDate, string remarks)
        {
            var ingredient = await _db.Ingredients.FindAsync(id);
            if (ingredient == null) return NotFound();

            var diff = quantity - ingredient.StockQuantity;
            if (Math.Abs(diff) > 0.0001m)
            {
                await _inventoryService.IntakeStockAsync(id, diff, DateTime.UtcNow, remarks ?? "Manual inventory adjustment (Admin)", expiryDate);
            }
            
            if (threshold.HasValue)
            {
                await _inventoryService.UpdateThresholdAsync(id, threshold.Value);
            }

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> ExportAuditLogs()
        {
            byte[] buffer = await _analyticsService.GenerateAuditLogsCSVAsync();
            return File(buffer, "text/csv", $"LJP_Audit_Logs_{DateTime.Now:yyyyMMdd}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportUsers()
        {
            byte[] buffer = await _analyticsService.GenerateUsersCSVAsync();
            return File(buffer, "text/csv", $"LJP_User_Directory_{DateTime.Now:yyyyMMdd}.csv");
        }


    }
}
