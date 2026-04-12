using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.IO;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Cashier,Admin,Manager,SuperAdmin")]
    public class CashierController : BaseController
    {
        private readonly IPayMongoService _payMongoService;
        private readonly ILogger<CashierController> _logger;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAnalyticsService _analyticsService;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IOrderService _orderService;

        public CashierController(ApplicationDbContext db, IPayMongoService payMongoService, ILogger<CashierController> logger, IReceiptService receiptService, IServiceScopeFactory scopeFactory, IAnalyticsService analyticsService, ICompositeViewEngine viewEngine, IOrderService orderService)
            : base(db)
        {
            _payMongoService = payMongoService;
            _logger = logger;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
            _analyticsService = analyticsService;
            _viewEngine = viewEngine;
            _orderService = orderService;
        }

        public class OrderRequest
        {
            public List<int>? ProductIds { get; set; }
            public string PaymentMethod { get; set; } = "Cash";
            public int? CustomerId { get; set; }
            public string? PromoCode { get; set; }
            public int RedemptionTier { get; set; }
            public decimal? CashReceived { get; set; }
        }

        public class CustomerRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
        }

        public IActionResult CreateOrder()
        {
            return RedirectToAction("Index", "POS");
        }

        [HttpGet]
        public async Task<IActionResult> ValidatePromoCode(string code, int? customerId)
        {
            if (string.IsNullOrWhiteSpace(code)) return Json(new { success = false, message = "Empty code" });

            var promotion = await _orderService.ValidatePromotionAsync(code, customerId);

            if (promotion == null)
            {
                return Json(new { success = false, message = "Invalid, expired, or fully redeemed promotion code." });
            }

            string discountLabel = promotion.DiscountType.ToLower() == "percentage" 
                ? $"{promotion.DiscountValue:0.##}% Off" 
                : $"₱{promotion.DiscountValue:N2} Off";

            return Json(new { 
                success = true, 
                message = $"Promo applied: {discountLabel}",
                discountType = promotion.DiscountType,
                discountValue = promotion.DiscountValue,
                promoId = promotion.PromotionID
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest request)
        {
            // Basic Validation - Ensure the cart isn't empty before proceeding.
            if (request.ProductIds == null || !request.ProductIds.Any())
                return Json(new { success = false, message = "No products selected." });

            // Identity Check 
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            // Shift Verification - We enforce that transactions can only be processed if the cashier has an open register.
            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            if (currentShift == null)
                return Json(new { success = false, message = "No open shift found. Please start a shift first." });

            // Prepare Service Payload 
            var svcRequest = new Services.OrderRequest
            {
                ProductIds = request.ProductIds,
                CustomerId = request.CustomerId,
                PaymentMethod = request.PaymentMethod,
                PromoCode = request.PromoCode,
                // Automatically differentiates the Status if it's a digital payment method
                PaymentStatus = request.PaymentMethod == "Paymongo" ? "Paid (Digital)" : "Paid",
                RedemptionTier = request.RedemptionTier
            };

            // Execute Business Logic 
            var result = await _orderService.ProcessOrderAsync(svcRequest, cashierId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            // Generate View - Render the receipt HTML
            var renderedReceipt = await RenderViewToStringAsync("ReceiptData", result.Order!);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { 
                    success = true, 
                    message = result.Message, 
                    orderId = result.Order!.OrderID,
                    receiptHtml = renderedReceipt,
                    warnings = result.Warnings
                });
            }
            return RedirectToAction("Receipt", new { id = result.Order?.OrderID });
        }

        public async Task<IActionResult> TransactionHistory()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var cashierId))
                return Challenge();

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.Cashier)
                .Where(o => o.CashierID == cashierId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayMongoOrder([FromBody] PayMongoOrderRequest request)
        {
            // Cart Validation - Protects against payload tampering.
            if (request.ProductIds == null || !request.ProductIds.Any())
                return BadRequest("No products selected.");

            // Identity & Shift Verification
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Unauthorized();

            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            if (currentShift == null)
                return BadRequest("No open shift found. Please start a shift first.");

            // Prepare Service Request - Notice PaymentStatus is initially set to "Pending" because they haven't scanned the QR yet.
            var svcRequest = new Services.OrderRequest
            {
                ProductIds = request.ProductIds,
                CustomerId = request.CustomerId,
                PaymentMethod = "E-Wallet (Paymongo)",
                PromoCode = request.PromoCode,
                PaymentStatus = "Pending",
                RedemptionTier = request.RedemptionTier
            };

            // Process Initial Order Database Entry
            var result = await _orderService.ProcessOrderAsync(svcRequest, cashierId);

            if (!result.Success)
                return BadRequest(result.Message);

            // Connect to Payment Gateway 
            // Create real PayMongo QR Ph code
            var qrCodeUrl = await _payMongoService.CreateQrPhPaymentAsync(result.Order!.FinalAmount, $"Order #{result.Order.OrderID.ToString().Substring(0, 8)}", result.Order.OrderID.ToString());

            if (string.IsNullOrEmpty(qrCodeUrl))
                return StatusCode(500, "Failed to generate PayMongo QR code.");

            // Return response to POS to display the QR Code.
            return Ok(new { qrCodeUrl, orderId = result.Order.OrderID, warnings = result.Warnings });
        }

        public class PayMongoOrderRequest
        {
            public List<int> ProductIds { get; set; } = new();
            public string? PromoCode { get; set; }
            public int? CustomerId { get; set; }
            public int RedemptionTier { get; set; }
        }

        public async Task<IActionResult> Receipt(Guid id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .Include(o => o.Cashier)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        public async Task<IActionResult> GetReceiptPartial(Guid id)
        {
            try 
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                    .Include(o => o.Cashier)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.OrderID == id);

                if (order == null)
                    return NotFound();

                return PartialView("_ReceiptPartial", order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReceiptPartial for Order ID {OrderId}", id);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchCustomers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            var customers = await _db.Customers
                .Where(c => c.FullName.Contains(query) || (c.PhoneNumber != null && c.PhoneNumber.Contains(query)))
                .Take(5)
                .Select(c => new { c.CustomerID, c.FullName, c.PhoneNumber, c.Points, c.Email })
                .ToListAsync();

            return Json(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCustomer([FromBody] CustomerRequest request)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid data." });

            if (string.IsNullOrWhiteSpace(request.FullName))
                return Json(new { success = false, message = "Name is required." });

            var customer = new Customer
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Points = 0
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            await LogAudit($"Registered Customer: {customer.FullName}");

            return Json(new { success = true, customerId = customer.CustomerID, fullName = customer.FullName });
        }

        public IActionResult ProcessPayment(Guid orderId, decimal amount)
        {
            // Fully integrated into PlaceOrder for the POS flow
            return RedirectToAction("TransactionHistory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReceipt(Guid orderId, string email)
        {
            if (string.IsNullOrEmpty(email)) return Json(new { success = false, message = "Email is required." });

            bool sent = await _receiptService.SendOrderReceiptAsync(orderId, email);
            
            if (sent) 
                return Json(new { success = true });
            else
                return Json(new { success = false, message = "Failed to send email. Ensure the order exists and email is valid." });
        }

        [HttpGet]
        public async Task<IActionResult> ShiftManagement()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            
            if (currentShift != null)
            {
                var cashOrders = await _db.Orders
                    .Where(o => o.CashierID == cashierId && o.OrderDate >= currentShift.StartTime && o.PaymentMethod == "Cash" && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Completed" || o.PaymentStatus == "Partially Refunded"))
                    .ToListAsync();
                
                decimal sales = cashOrders.Sum(o => o.FinalAmount - o.RefundedAmount);
                decimal expected = currentShift.StartingCash + sales;
                
                ViewBag.CashSales = sales;
                ViewBag.ExpectedCash = expected;
            }
            
            return View(currentShift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartShift(decimal startingCash)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            var existing = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            if(existing != null)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "You already have an open shift.";
                return RedirectToAction(AppConstants.Actions.ShiftManagement);
            }

            var shift = new CashShift
            {
                CashierID = cashierId,
                StartTime = DateTime.UtcNow,
                StartingCash = startingCash,
                IsClosed = false
            };

            _db.CashShifts.Add(shift);
            await _db.SaveChangesAsync();
            await LogAudit($"Started Shift", $"Float: ₱{startingCash:N2}");

            TempData["SuccessMessage"] = "Shift started successfully! Register is Open.";
            return RedirectToAction("Index", "POS");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseShift(decimal actualEndingCash)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            var shift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            if(shift == null)
            {
                TempData[AppConstants.SessionKeys.ErrorMessage] = "No open shift found.";
                return RedirectToAction(AppConstants.Actions.ShiftManagement);
            }

            var cashOrders = await _db.Orders
                .Where(o => o.CashierID == cashierId && o.OrderDate >= shift.StartTime && o.PaymentMethod == "Cash" && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Completed" || o.PaymentStatus == "Partially Refunded"))
                .ToListAsync();

            decimal expected = shift.StartingCash;
            foreach(var o in cashOrders)
                expected += (o.FinalAmount - o.RefundedAmount);

            shift.ExpectedEndingCash = expected;
            shift.ActualEndingCash = actualEndingCash;
            shift.Difference = actualEndingCash - expected;
            shift.EndTime = DateTime.UtcNow;
            shift.IsClosed = true;

            await _db.SaveChangesAsync();
            
            // Send Shift Report in background to avoid blocking the UI
            _ = Task.Run(async () => {
                try {
                    using (var scope = _scopeFactory.CreateScope()) {
                        var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await scopedReceiptService.SendShiftReportAsync(shift.CashShiftID);
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Background shift report sending failed for shift {ShiftId}", shift.CashShiftID);
                }
            });

            await LogAudit($"Closed Shift", $"Expected: ₱{expected:N2}, Actual: ₱{actualEndingCash:N2}, Difference: ₱{shift.Difference:N2}");

            TempData[AppConstants.SessionKeys.SuccessMessage] = $"Shift closed successfully. Difference: ₱{shift.Difference:N2}";
            return RedirectToAction(AppConstants.Actions.ShiftManagement);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTransactions()
        {
            byte[] buffer = await _analyticsService.GenerateTransactionsCSVAsync();
            return File(buffer, "text/csv", $"LJP_Transactions_{DateTime.Now:yyyyMMdd}.csv");
        }
        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            ViewData.Model = model;
            using (var sw = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
                if (!viewResult.Success)
                {
                    viewResult = _viewEngine.GetView(null, viewName, false);
                    if (!viewResult.Success) return string.Empty;
                }

                var viewContext = new Microsoft.AspNetCore.Mvc.Rendering.ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    sw,
                    new Microsoft.AspNetCore.Mvc.ViewFeatures.HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
        }
    }
}
