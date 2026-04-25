using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "MarketingStaff,Admin,SuperAdmin")]
    public class MarketingController : BaseController
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAnalyticsService _analyticsService;

        public MarketingController(ApplicationDbContext db, IServiceScopeFactory scopeFactory, IAnalyticsService analyticsService)
            : base(db)
        {
            _scopeFactory = scopeFactory;
            _analyticsService = analyticsService;
        }

        //  Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var data = await _analyticsService.GetMarketingDashboardDataAsync();
            return View(data);
        }


        public class RewardRequest
        {
            public int? CustomerId { get; set; }
            public int? PointsToDeduct { get; set; }
            public decimal? DiscountValue { get; set; }
            public string RewardName { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReward([FromBody] RewardRequest req)
        {
            if (!ModelState.IsValid || req.CustomerId == null)
                return BadRequest("Invalid request.");

            var customer = await _db.Customers.FindAsync(req.CustomerId.Value);
            if (customer == null) return NotFound();

            var pointsToDeduct = req.PointsToDeduct > 0 ? req.PointsToDeduct.Value : 10;
            var discountValue = req.DiscountValue > 0 ? req.DiscountValue.Value : 15;
            var rewardName = !string.IsNullOrEmpty(req.RewardName) ? req.RewardName : "15% Loyalty Reward";

            if (customer.Points < pointsToDeduct)
                return Json(new { success = false, message = $"Customer needs at least {pointsToDeduct} points for a reward." });

            // Build a promo code
            var cleanName = customer.FullName.Replace(" ", "").ToUpper();
            if (cleanName.Length > 5) cleanName = cleanName.Substring(0, 5);
            var promoCode = $"REWARD-{cleanName}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

            // Create the promotion entry
            var promotion = new Promotion
            {
                PromotionName = promoCode,
                DiscountType  = "Percentage",
                DiscountValue = discountValue,
                StartDate     = DateTime.UtcNow,
                EndDate       = DateTime.UtcNow.AddDays(30),
                IsActive      = true,
                ApprovalStatus = "Approved",
                ApprovedDate  = DateTime.UtcNow,
                MaxRedemptions = 1,
                OneTimePerCustomer = true,
                IsOneTimeReward = true,
                TargetAudience = "Specific VIP"
            };
            _db.Promotions.Add(promotion);

            // Deduct points from customer
            customer.Points -= pointsToDeduct;

            // Log the redemption event
            _db.RewardRedemptions.Add(new RewardRedemption
            {
                CustomerID      = customer.CustomerID,
                RewardName      = rewardName,
                PointsRedeemed  = pointsToDeduct,
                RedemptionDate  = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await LogAudit("Generated VIP Loyalty Reward", $"Customer: {customer.FullName}, Code: {promoCode} for {rewardName}");

            // Send reward code to customer email in background
            if (!string.IsNullOrEmpty(customer.Email))
            {
                var capturedCustomer = customer;
                var capturedCode     = promoCode;
                var capturedDiscount = discountValue;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await svc.SendRedemptionCodeEmailAsync(capturedCustomer, capturedCode, capturedDiscount);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Email Failure - Reward Code]: {ex.Message}");
                    }
                });
            }

            bool emailSent = !string.IsNullOrEmpty(customer.Email);
            string emailNote = emailSent
                ? $" A copy has been sent to {customer.Email} with a scannable QR code."
                : " (No email on file — show code manually.)";

            return Json(new
            {
                success   = true,
                promoCode = promoCode,
                message   = $"Reward provsioned successfully for {customer.FullName}!{emailNote}"
            });
        }

        //  Promotions
        public async Task<IActionResult> Promotions()
        {
            var promotions = await _db.Promotions
                .Include(p => p.Orders)
                .ToListAsync();
            return View(promotions);
        }

        public IActionResult CreateCampaign()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCampaign(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                promotion.ApprovalStatus = "Pending";
                promotion.ApprovedDate = null;
                promotion.CreatedBy = GetCurrentUserId();
                
                _db.Promotions.Add(promotion);
                
                // Notify Management
                _db.Notifications.Add(new Notification
                {
                    Title = "Promotion Approval Needed",
                    Message = $"Campaign '{promotion.PromotionName}' was created by {User.Identity?.Name} and is pending approval.",
                    Type = "info",
                    IconClass = "fas fa-bullhorn",
                    CreatedAt = DateTime.UtcNow,
                    TargetUrl = "/Manager/Promotions"
                });

                await _db.SaveChangesAsync();
                await LogAudit($"Created Campaign: {promotion.PromotionName}");
                
                TempData[AppConstants.SessionKeys.SuccessMessage] = $"Campaign '{promotion.PromotionName}' created and submitted for manager approval.";
                return RedirectToAction(nameof(Promotions));
            }
            return View(promotion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCampaign(Promotion promotion)
        {
            if (!ModelState.IsValid) return View(promotion);

            var existing = await _db.Promotions.FindAsync(promotion.PromotionID);
            if (existing == null) return NotFound();

            existing.PromotionName = promotion.PromotionName;
            existing.DiscountType = promotion.DiscountType;
            existing.DiscountValue = promotion.DiscountValue;
            existing.StartDate = promotion.StartDate;
            existing.EndDate = promotion.EndDate;
            existing.IsActive = promotion.IsActive;
            existing.MaxRedemptions = promotion.MaxRedemptions;
            existing.OneTimePerCustomer = promotion.OneTimePerCustomer;
            
          
            existing.ApprovalStatus = "Pending";
            existing.ApprovedBy = null;
            existing.ApprovedDate = null;
            existing.CreatedBy = GetCurrentUserId();

            // Notify Management
            _db.Notifications.Add(new Notification
            {
                Title = "Promotion Approval Needed",
                Message = $"Campaign '{existing.PromotionName}' was edited and requires re-approval.",
                Type = "warning",
                IconClass = "fas fa-edit",
                CreatedAt = DateTime.UtcNow,
                TargetUrl = "/Manager/Promotions"
            });

            await _db.SaveChangesAsync();
            await LogAudit($"Edited Campaign: {promotion.PromotionName}");
            TempData[AppConstants.SessionKeys.SuccessMessage] = "Campaign updated and resubmitted for approval.";
            return RedirectToAction(nameof(Promotions));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCampaign(int id)
        {
            if (!ModelState.IsValid) return BadRequest();
            var promotion = await _db.Promotions.FindAsync(id);
            if (promotion != null)
            {
                var promoName = promotion.PromotionName;
                _db.Promotions.Remove(promotion);
                await _db.SaveChangesAsync();
                await LogAudit($"Deleted Campaign: {promoName}");
                TempData[AppConstants.SessionKeys.SuccessMessage] = "Campaign deleted successfully.";
            }
            return RedirectToAction(nameof(Promotions));
        }

        public class PromotionPerformanceViewModel
        {
            public int PromotionID { get; set; }
            public string? PromotionName { get; set; }
            public string? TargetAudience { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool IsActive { get; set; }
            public int UsageCount { get; set; }
            public decimal TotalSalesValue { get; set; }
            public decimal TotalDiscountGiven { get; set; }
        }

        public async Task<IActionResult> PromotionPerformance()
        {
            var performance = await _db.Promotions
                .Include(p => p.Orders)
                .Select(p => new PromotionPerformanceViewModel
                {
                    PromotionID = p.PromotionID,
                    PromotionName = p.PromotionName,
                    TargetAudience = p.TargetAudience,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    UsageCount = p.Orders.Count,
                    TotalSalesValue = p.Orders.Sum(o => o.FinalAmount),
                    TotalDiscountGiven = p.Orders.Sum(o => o.DiscountAmount)
                })
                .ToListAsync();
            return View(performance);
        }

        public async Task<IActionResult> CampaignAnalytics(int id)
        {
            if (!ModelState.IsValid) return BadRequest();
            var campaign = await _db.Promotions
                .Include(p => p.Orders)
                    .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(p => p.PromotionID == id);

            if (campaign == null) return NotFound();

            return View(campaign);
        }

        //  Customer Engagement
        public async Task<IActionResult> Customers()
        {
            var customers = await _db.Customers.ToListAsync();
            return View(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(Customer customer)
        {
            if (!ModelState.IsValid) return View(customer);
            if (ModelState.IsValid)
            {
                customer.Points = 0;
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();
                await LogAudit($"Enrolled Customer: {customer.FullName}");
                TempData[AppConstants.SessionKeys.SuccessMessage] = "Customer added successfully.";
            }
            return RedirectToAction(nameof(Customers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(Customer customer)
        {
            if (!ModelState.IsValid) return RedirectToAction(nameof(Customers));

            var existing = await _db.Customers.FindAsync(customer.CustomerID);
            if (existing == null) return NotFound();

            existing.FullName = customer.FullName;
            existing.PhoneNumber = customer.PhoneNumber;
            existing.Email = customer.Email;
            
            // Only Admins or SuperAdmins can manually adjust points
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
            {
                existing.Points = customer.Points;
            }

            await _db.SaveChangesAsync();
            await LogAudit($"Edited Customer: {customer.FullName}");
            TempData[AppConstants.SessionKeys.SuccessMessage] = "Customer updated successfully.";
            return RedirectToAction(nameof(Customers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            if (!ModelState.IsValid) return BadRequest();
            // Only Admins or SuperAdmins can delete customers
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Unauthorized. Only administrators can remove customer profiles.";
                return RedirectToAction(nameof(Customers));
            }

            var customer = await _db.Customers.Include(c => c.Orders).FirstOrDefaultAsync(c => c.CustomerID == id);
            if (customer == null) return NotFound();

            if (customer.Orders.Count > 0)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "Cannot delete customer with existing order history. Consider archiving or keeping the record for audit purposes.";
                return RedirectToAction(nameof(Customers));
            }

            var custName = customer.FullName;
            _db.Customers.Remove(customer);
            await _db.SaveChangesAsync();
            await LogAudit($"Deleted Customer: {custName}");
            TempData[AppConstants.SessionKeys.SuccessMessage] = "Customer removed successfully.";
            
            return RedirectToAction(nameof(Customers));
        }

        public async Task<IActionResult> LoyaltyOverview()
        {
            // Algorithm: Tier Distribution Logic
            var customers = await _db.Customers.ToListAsync();
            
            // Data for Tier Distribution Chart
            var tiers = new
            {
                Gold = customers.Count(c => c.Points >= 500),
                Silver = customers.Count(c => c.Points >= 300 && c.Points < 500),
                Bronze = customers.Count(c => c.Points >= 100 && c.Points < 300),
                Member = customers.Count(c => c.Points < 100)
            };

            var topHolders = customers.OrderByDescending(c => c.Points).Take(10).ToList();

            ViewBag.TierLabels = new[] { "Gold (500+)", "Silver (300+)", "Bronze (100+)", "Member (<100)" };
            ViewBag.TierData = new[] { tiers.Gold, tiers.Silver, tiers.Bronze, tiers.Member };

            return View(topHolders);
        }

        public async Task<IActionResult> RewardRedemptionLogs()
        {
            var logs = await _db.RewardRedemptions
                .Include(r => r.Customer)
                .OrderByDescending(r => r.RedemptionDate)
                .ToListAsync();
            return View(logs);
        }

        public class SalesTrendViewModel
        {
            public DateTime Date { get; set; }
            public decimal TotalSales { get; set; }
            public int Count { get; set; }
            public double AvgValue { get; set; }
        }

        //  Reports
        public async Task<IActionResult> SalesTrends(string type = "week", string value = "")
        {
            if (!ModelState.IsValid) return BadRequest();
            var (startDate, endDate, finalValue) = CalculateDateRange(type, value);

            var query = _db.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && 
                           (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed"));

            var orders = await query.ToListAsync();

            // Algorithm: Time-Series Aggregation Algorithm (Revenue Velocity)
            var viewModelList = orders
                .GroupBy(o => type == "year" ? new DateTime(o.OrderDate.Year, o.OrderDate.Month, 1, 0, 0, 0, DateTimeKind.Utc) : DateTime.SpecifyKind(o.OrderDate.Date, DateTimeKind.Utc))
                .Select(g => new SalesTrendViewModel { 
                    Date = g.Key, 
                    TotalSales = g.Sum(o => o.FinalAmount),
                    Count = g.Count(),
                    AvgValue = g.Any() ? (double)g.Average(o => o.FinalAmount) : 0
                })
                .OrderBy(g => g.Date)
                .ToList();

            ViewBag.SelectedType = type;
            ViewBag.SelectedValue = finalValue;
            return View(viewModelList);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTacticalROI()
        {
            byte[] buffer = await _analyticsService.GenerateTacticalROICSVAsync();
            return File(buffer, "text/csv", $"LJP_Marketing_ROI_{DateTime.Now:yyyyMMdd}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportSalesTrends(string type = "week", string value = "")
        {
            if (!ModelState.IsValid) return BadRequest();
            var (startDate, endDate, finalValue) = CalculateDateRange(type, value);
            string label = type switch
            {
                "year" => $"Year {finalValue}",
                "week" => $"Week {finalValue}",
                _ => startDate.ToString("MMMM yyyy")
            };

            byte[] buffer = await _analyticsService.GenerateSalesTrendsCSVAsync(startDate, endDate, label);
            return File(buffer, "text/csv", $"LJP_Sales_Trend_{type}_{finalValue}.csv");
        }

        private static (DateTime Start, DateTime End, string FinalValue) CalculateDateRange(string type, string value)
        {
            DateTime today = DateTime.Today;
            DateTime startDate;
            string finalValue = value;

            if (string.IsNullOrEmpty(finalValue))
            {
                finalValue = GetDefaultValueForType(type, today);
            }

            if (type == "year")
            {
                if (!int.TryParse(finalValue, out int year)) year = today.Year;
                startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (startDate, startDate.AddYears(1).AddDays(-1), year.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            
            if (type == "week")
            {
                startDate = ParseWeekStartDate(finalValue, today);
                while (startDate.DayOfWeek != DayOfWeek.Monday) startDate = startDate.AddDays(-1);
                
                var currentWeek = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(startDate, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                finalValue = $"{startDate.Year}-W{currentWeek:D2}";
                
                return (startDate, startDate.AddDays(7).AddSeconds(-1), finalValue);
            }

            // Month default
            if (!DateTime.TryParse(finalValue + "-01", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out startDate)) 
                startDate = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            else
                startDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            
            return (startDate, startDate.AddMonths(1).AddDays(-1), startDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string GetDefaultValueForType(string type, DateTime today)
        {
            if (type == "month") return today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
            if (type == "week") return today.ToString("yyyy-'W'WW", System.Globalization.CultureInfo.InvariantCulture);
            if (type == "year") return today.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static DateTime ParseWeekStartDate(string value, DateTime today)
        {
            if (!string.IsNullOrEmpty(value) && value.Contains("-W", StringComparison.OrdinalIgnoreCase))
            {
                var parts = value.Split("-W");
                if (parts.Length == 2 && int.TryParse(parts[0], out int y) && int.TryParse(parts[1], out int w))
                {
                    return new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays((w - 1) * 7);
                }
            }
            return today;
        }
    }
}
