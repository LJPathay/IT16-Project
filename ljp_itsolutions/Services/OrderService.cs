using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ljp_itsolutions.Services
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _db;
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<OrderService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public OrderService(
            ApplicationDbContext db,
            IInventoryService inventoryService,
            ILogger<OrderService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _db = db;
            _inventoryService = inventoryService;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task<OrderResult> ProcessOrderAsync(OrderRequest request, Guid cashierId)
        {
            var result = new OrderResult();
            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    // 1. Create Order base
                    var order = new Order
                    {
                        OrderID = Guid.NewGuid(),
                        OrderDate = DateTime.UtcNow,
                        CashierID = cashierId,
                        CustomerID = request.CustomerId,
                        PaymentMethod = request.PaymentMethod ?? "Cash",
                        PaymentStatus = request.PaymentStatus ?? "Paid"
                    };

                    // 2. Resolve Promotion
                    Promotion? promotion = null;
                    if (!string.IsNullOrEmpty(request.PromoCode))
                    {
                        promotion = await ValidatePromotionAsync(request.PromoCode, request.CustomerId);
                        if (promotion != null)
                        {
                            order.PromotionID = promotion.PromotionID;
                        }
                    }

                    // 3. Process Products and Inventory
                    var productGroups = request.ProductIds.GroupBy(id => id);
                    decimal subtotal = 0;
                    
                    foreach (var group in productGroups)
                    {
                        var product = await _db.Products
                            .Include(p => p.ProductRecipes)
                            .ThenInclude(pr => pr.Ingredient)
                            .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                        if (product == null) continue;
                        int qty = group.Count();
                        
                        // Inventory Check
                        if (product.ProductRecipes != null && product.ProductRecipes.Any())
                        {
                            foreach (var recipe in product.ProductRecipes)
                            {
                                if (recipe.Ingredient.StockQuantity < (recipe.QuantityRequired * qty))
                                {
                                    result.Success = false;
                                    result.Message = $"Insufficient stock for {recipe.Ingredient.Name}. Required: {recipe.QuantityRequired * qty}, Available: {recipe.Ingredient.StockQuantity}";
                                    await transaction.RollbackAsync();
                                    return result;
                                }
                                
                                // Deduct
                                recipe.Ingredient.StockQuantity -= (recipe.QuantityRequired * qty);
                                
                                // Log
                                _db.InventoryLogs.Add(new InventoryLog
                                {
                                    IngredientID = recipe.IngredientID,
                                    QuantityChange = -(recipe.QuantityRequired * qty),
                                    ChangeType = "Sale",
                                    LogDate = DateTime.UtcNow,
                                    Remarks = $"Deducted for Order #{order.OrderID.ToString().Substring(0, 8)} ({product.ProductName})"
                                });

                                // Threshold check
                                if (recipe.Ingredient.StockQuantity <= recipe.Ingredient.LowStockThreshold)
                                {
                                    _db.Notifications.Add(new Notification
                                    {
                                        Title = "Low Stock Alert",
                                        Message = $"{recipe.Ingredient.Name} is running low ({recipe.Ingredient.StockQuantity:0.##} {recipe.Ingredient.Unit} left).",
                                        Type = "warning",
                                        CreatedAt = DateTime.UtcNow,
                                        TargetUrl = "/Admin/InventoryOverview"
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Standalone product
                            if (product.StockQuantity < qty)
                            {
                                result.Success = false;
                                result.Message = $"Insufficient stock for {product.ProductName}. Required: {qty}, Available: {product.StockQuantity}";
                                await transaction.RollbackAsync();
                                return result;
                            }
                            
                            product.StockQuantity -= qty;
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                ProductID = product.ProductID,
                                QuantityChange = -qty,
                                ChangeType = "Sale",
                                LogDate = DateTime.UtcNow,
                                Remarks = $"Deducted for Order #{order.OrderID.ToString().Substring(0, 8)}"
                            });

                            if (product.StockQuantity <= product.LowStockThreshold)
                            {
                                _db.Notifications.Add(new Notification
                                {
                                    Title = "Low Stock Alert",
                                    Message = $"{product.ProductName} is running low ({product.StockQuantity} left).",
                                    Type = "warning",
                                    CreatedAt = DateTime.UtcNow,
                                    TargetUrl = "/Admin/InventoryOverview"
                                });
                            }
                        }

                        var itemSubtotal = product.Price * qty;
                        subtotal += itemSubtotal;

                        order.OrderDetails.Add(new OrderDetail
                        {
                            ProductID = product.ProductID,
                            Quantity = qty,
                            UnitPrice = product.Price,
                            Subtotal = itemSubtotal
                        });
                    }

                    // 4. Calculate Totals and Discounts
                    order.TotalAmount = subtotal;
                    decimal discount = 0;
                    if (promotion != null)
                    {
                        if (promotion.DiscountType == "Percentage")
                            discount = order.TotalAmount * (promotion.DiscountValue / 100m);
                        else
                            discount = promotion.DiscountValue;
                    }

                    // Elite Patron Logic (Centralized here)
                    if (request.CustomerId.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(request.CustomerId.Value);
                        if (customer != null && customer.Points >= 1000)
                        {
                            var eliteDiscount = order.TotalAmount * 0.05m;
                            if (eliteDiscount > discount)
                            {
                                discount = eliteDiscount;
                                result.Warnings.Add("Elite Patron 5% discount applied (exceeded promo discount).");
                            }
                        }
                    }
 
                    // 5. Point Redemption Logic (Tiered Reward Selection)
                    if (request.RedemptionTier > 0 && request.CustomerId.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(request.CustomerId.Value);
                        if (customer != null)
                        {
                            int ptsRequired = 0;
                            decimal rewardValue = 0;
                            string tierLabel = "";

                            switch (request.RedemptionTier)
                            {
                                case 1: ptsRequired = 5; rewardValue = 50m; tierLabel = "Bronze"; break;
                                case 2: ptsRequired = 10; rewardValue = 110m; tierLabel = "Silver"; break; // +10 bonus
                                case 3: ptsRequired = 20; rewardValue = 250m; tierLabel = "Gold"; break; // +50 bonus
                            }

                            if (customer.Points >= ptsRequired)
                            {
                                customer.Points -= ptsRequired;
                                discount += rewardValue;
                                result.Warnings.Add($"{tierLabel} Reward Applied: {ptsRequired} points redeemed for ₱{rewardValue:N0} discount.");
                                await LogAuditAsync(cashierId, "Loyalty Redemption", $"Deducted {ptsRequired} points ({tierLabel} Tier) from Customer {customer.FullName}.");
                            }
                            else
                            {
                                result.Warnings.Add($"Loyalty Redemption Failed: Customer has insufficient points for {tierLabel} reward (Required: {ptsRequired}).");
                            }
                        }
                    }

                    order.DiscountAmount = discount;
                    order.FinalAmount = Math.Max(0, order.TotalAmount - order.DiscountAmount);

                    // Loyalty Points handling (Earn points for current purchase)
                    if (request.CustomerId.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(request.CustomerId.Value);
                        if (customer != null)
                        {
                            // Tier-based multipliers (Centralized from hardcoded values in controllers)
                            decimal multiplier = 1.0m;
                            if (customer.Points >= 500) multiplier = 1.5m;
                            else if (customer.Points >= 300) multiplier = 1.25m;
                            else if (customer.Points >= 100) multiplier = 1.1m;
                            
                            int earnedPoints = (int)Math.Floor((order.FinalAmount / 100m) * multiplier);
                            if (earnedPoints > 0)
                            {
                                customer.Points += earnedPoints;
                            }
                        }
                    }

                    // Notifications for High Value Orders
                    if (order.FinalAmount >= 1000)
                    {
                         _db.Notifications.Add(new Notification
                        {
                            Title = "High Value Order",
                            Message = $"Order #{order.OrderID.ToString().Substring(0, 8)} for ₱{order.FinalAmount:N2} was placed.",
                            Type = "info",
                            IconClass = "fas fa-award",
                            CreatedAt = DateTime.UtcNow,
                            TargetUrl = $"/Admin/Transactions"
                        });
                    }

                    // Save
                    _db.Orders.Add(order);
                    await _db.SaveChangesAsync();
                    
                    // Log Audit
                    await LogAuditAsync(cashierId, "Place Order", $"Placed order #{order.OrderID} for ₱{order.FinalAmount:N2}");

                    await transaction.CommitAsync();

                    // Trigger Background Receipt
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                            await receiptService.SendOrderReceiptAsync(order.OrderID);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background receipt generation failed for Order {OrderId}", order.OrderID);
                        }
                    });

                    result.Success = true;
                    result.Order = order;
                    result.Message = "Order processed successfully.";
                    return result;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Order processing failed.");
                    result.Success = false;
                    result.Message = "An internal error occurred while processing the order.";
                    return result;
                }
            });
        }

        public async Task<Promotion?> ValidatePromotionAsync(string promoCode, int? customerId)
        {
            if (string.IsNullOrEmpty(promoCode)) return null;

            var cleanPromo = promoCode.Replace(" ", "").Trim().ToLower();
            var allPromos = await _db.Promotions
                .Include(p => p.Orders)
                .Where(p => p.IsActive && p.ApprovalStatus == "Approved")
                .ToListAsync();

            var promotion = allPromos.FirstOrDefault(p =>
                (p.PromotionName ?? "").Replace(" ", "").Equals(cleanPromo, StringComparison.OrdinalIgnoreCase) &&
                p.StartDate.Date <= DateTime.Today &&
                p.EndDate.Date >= DateTime.Today);

            if (promotion == null) return null;

            // Check redemption caps
            if (promotion.MaxRedemptions.HasValue && promotion.Orders.Count >= promotion.MaxRedemptions.Value)
                return null;

            // Check one-time per customer
            if (promotion.OneTimePerCustomer && customerId.HasValue)
            {
                if (promotion.Orders.Any(o => o.CustomerID == customerId.Value))
                    return null;
            }

            return promotion;
        }

        public async Task<decimal> GetTaxRateAsync()
        {
            var taxSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "TaxRate");
            if (taxSetting != null && decimal.TryParse(taxSetting.SettingValue, out var parsedTax))
            {
                return parsedTax / 100m;
            }
            return 0.05m; // Default
        }

        public async Task<bool> VoidOrderAsync(Guid orderId, string remarks)
        {
             var order = await _db.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null || order.PaymentStatus == "Voided") return false;

            order.PaymentStatus = "Voided";
            await _inventoryService.RevertOrderInventoryAsync(order);
            
            // Revert points if possible? (Optional, but good practice)
            if (order.CustomerID.HasValue)
            {
                var customer = await _db.Customers.FindAsync(order.CustomerID.Value);
                if (customer != null)
                {
                     decimal basePoints = order.FinalAmount / 100m;
                     customer.Points -= (int)Math.Floor(basePoints); 
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RefundOrderAsync(Guid orderId, string remarks)
        {
             var order = await _db.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null || order.PaymentStatus == "Refunded") return false;

            order.PaymentStatus = "Refunded";
            await _inventoryService.RevertOrderInventoryAsync(order);
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task LogAuditAsync(Guid userId, string action, string details)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserID = userId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
