using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    // DEFENSE NOTE: Role Authorization. This attribute strictly enforces access control. Only authenticated users mapped to one of these explicit strings in UserRoles.cs can load the POS page.
    [Authorize(Roles = "Cashier,Admin,Manager,SuperAdmin")]
    public class POSController : Controller
    {
        private readonly ljp_itsolutions.Data.ApplicationDbContext _db;

        public POSController(ljp_itsolutions.Data.ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            // DEFENSE NOTE: Step 1 - Identity Verfication. Read the securely signed session cookie to extract the active User's unique ID.
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Challenge();

            // DEFENSE NOTE: Step 2 - Shift Validation. The POS application checks against the database to confirm the user has an active shift before allowing them to access the cart.
            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == userId && !s.IsClosed);
            if (currentShift == null)
            {
                TempData["ErrorMessage"] = "No open shift found. Please start a shift first.";
                return RedirectToAction("ShiftManagement", "Cashier");
            }

            // DEFENSE NOTE: Step 3 - Content Pre-loading. We don't just load products; we actively include their specific Recipes and backend Ingredients to analyze inventory.
            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                .Where(p => p.IsAvailable)
                .ToListAsync();

            // DEFENSE NOTE: Step 4 - Real-Time Inventory Filtering. This loop ensures our menu only displays items that actually have enough raw ingredients available to fulfill at least 1 order.
            var availableProducts = products.Where(p => {
                if (p.ProductRecipes != null && p.ProductRecipes.Any())
                {
                    return p.ProductRecipes.All(pr => pr.Ingredient.StockQuantity >= pr.QuantityRequired);
                }
                return p.StockQuantity > 0;
            }).ToList();

            // DEFENSE NOTE: Step 5 - Dynamic Tax Retrieval. Rather than hardcoding 5% into the POS calculation, we fetch it securely from the globally managed System Settings table.
            var taxSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "TaxRate");
            decimal taxRate = 0.05m;
            if (taxSetting != null && decimal.TryParse(taxSetting.SettingValue, out var parsedTax))
            {
                taxRate = parsedTax / 100m; 
            }
            ViewBag.TaxRate = taxRate;

            return View(availableProducts);
        }

        public IActionResult CreateOrder()
        {
            return RedirectToAction("Index");
        }
    }
}
