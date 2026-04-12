using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Cashier,Admin,Manager,SuperAdmin")]
    public class PosController : BaseController
    {
        public PosController(ljp_itsolutions.Data.ApplicationDbContext db) : base(db)
        {
        }

        public async Task<IActionResult> Index()
        {
            // Identity Verification. 
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Challenge();

            // Shift Validation. 
            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == userId && !s.IsClosed);
            if (currentShift == null)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "No open shift found. Please start a shift first.";
                return RedirectToAction("ShiftManagement", "Cashier");
            }

            // Content Pre-loading. 
            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                .Where(p => p.IsAvailable)
                .ToListAsync();

            // Real-Time Inventory Filtering. Ensures the menu only displays items that actually have enough raw ingredients available to fulfill at least 1 order.
            var availableProducts = products.Where(p => {
                if (p.ProductRecipes != null && p.ProductRecipes.Any())
                {
                    return p.ProductRecipes.All(pr => pr.Ingredient.StockQuantity >= pr.QuantityRequired);
                }
                return p.StockQuantity > 0;
            }).ToList();

            // Dynamic Tax Retrieval.
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
