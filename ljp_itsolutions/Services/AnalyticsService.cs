using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _db;

        public AnalyticsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<ProductSalesSummary>> GetTopProductsAsync(int count = 10)
        {
            return await _db.OrderDetails
                .GroupBy(od => od.Product.ProductName)
                .Select(g => new ProductSalesSummary
                {
                    ProductName = g.Key,
                    TotalSold = g.Sum(od => od.Quantity),
                    Revenue = g.Sum(od => od.Subtotal)
                })
                .OrderByDescending(s => s.TotalSold)
                .Take(count)
                .ToListAsync();
        }

        public async Task<byte[]> GenerateSalesCSVAsync(DateTime startDate, DateTime endDate)
        {
            var data = await _db.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.Product)
                    .ThenInclude(p => p.Category)
                .Where(od => od.Order.OrderDate >= startDate && od.Order.OrderDate <= endDate && 
                            (od.Order.PaymentStatus == "Paid" || od.Order.PaymentStatus == "Paid (Digital)" || od.Order.PaymentStatus == "Completed"))
                .GroupBy(od => new { od.Product.ProductName, Category = od.Product.Category != null ? od.Product.Category.CategoryName : "Uncategorized" })
                .Select(g => new
                {
                    Product = g.Key.ProductName,
                    Category = g.Key.Category,
                    TotalSold = g.Sum(od => od.Quantity),
                    Revenue = g.Sum(od => od.Subtotal)
                })
                .OrderByDescending(s => s.TotalSold)
                .ToListAsync();

            var totalRevenue = data.Sum(x => x.Revenue);
            var totalUnits = data.Sum(x => x.TotalSold);

            var csv = new StringBuilder();
            csv.Append('\uFEFF'); // UTF-8 BOM

            csv.AppendLine("LJP IT SOLUTIONS - COFFEE ERP");
            csv.AppendLine("SALES PERFORMANCE REPORT");
            csv.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            csv.AppendLine($"Generated: {DateTime.UtcNow:f}");
            csv.AppendLine();

            csv.AppendLine("EXECUTIVE SUMMARY");
            csv.AppendLine($"Total Gross Revenue,\"₱{totalRevenue:N2}\"");
            csv.AppendLine($"Total Inventory Units Sold,{totalUnits:N0}");
            csv.AppendLine($"Average Sale Value Per Product,\"₱{(totalUnits > 0 ? (totalRevenue / totalUnits) : 0):N2}\"");
            csv.AppendLine();

            csv.AppendLine("ITEMIZED PERFORMANCE");
            csv.AppendLine("Product Name,Category,Units Sold,Sales Mix %,Revenue (PHP)");

            foreach (var item in data)
            {
                var salesMix = totalUnits > 0 ? (decimal)item.TotalSold / totalUnits : 0;
                csv.AppendLine($"\"{item.Product}\",\"{item.Category}\",{item.TotalSold},{salesMix:P2},\"{item.Revenue:N2}\"");
            }

            csv.AppendLine();
            csv.AppendLine("--- END OF REPORT ---");

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateAuditLogsCSVAsync()
        {
            var logs = await _db.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            csv.AppendLine("Audit ID,Timestamp,User,Action");

            foreach (var log in logs)
            {
                csv.AppendLine($"{log.AuditID},\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.User?.FullName ?? "System"}\",\"{log.Action.Replace("\"", "\"\"")}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateTacticalROICSVAsync()
        {
            var campaigns = await _db.Promotions
                .Include(p => p.Orders)
                .Select(p => new
                {
                    p.PromotionName,
                    p.StartDate,
                    p.EndDate,
                    p.IsActive,
                    UsageCount = p.Orders.Count,
                    TotalRevenue = p.Orders.Sum(o => o.FinalAmount),
                    TotalDiscount = p.Orders.Sum(o => o.DiscountAmount)
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            csv.AppendLine("Campaign Name,Status,Start Date,End Date,Redemptions,Revenue,Discount (Equity Cost)");

            foreach (var c in campaigns)
            {
                var status = c.IsActive ? (c.EndDate < DateTime.UtcNow ? "Completed" : "Active") : "Terminated";
                csv.AppendLine($"\"{c.PromotionName}\",\"{status}\",\"{c.StartDate:yyyy-MM-dd}\",\"{c.EndDate:yyyy-MM-dd}\",{c.UsageCount},\"{c.TotalRevenue:N2}\",\"{c.TotalDiscount:N2}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateTransactionsCSVAsync()
        {
            var transactions = await _db.Orders
                .Include(o => o.Cashier)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            csv.AppendLine("Order ID,Date,Cashier,Total Amount,Status,Payment Method");

            foreach (var t in transactions)
            {
                csv.AppendLine($"\"{t.OrderID}\",\"{t.OrderDate:yyyy-MM-dd HH:mm}\",\"{t.Cashier?.FullName ?? "N/A"}\",\"{t.FinalAmount:N2}\",\"{t.PaymentStatus}\",\"{t.PaymentMethod}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateUsersCSVAsync()
        {
            var users = await _db.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            csv.AppendLine("User ID,Full Name,Username,Email,Role,Status,Joined Date");

            foreach (var u in users)
            {
                csv.AppendLine($"\"{u.UserID}\",\"{u.FullName}\",\"{u.Username}\",\"{u.Email}\",\"{u.Role}\",\"{(u.IsActive ? "Active" : "Archived")}\",\"{u.CreatedAt:yyyy-MM-dd}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateSalesTrendsCSVAsync(DateTime start, DateTime end, string periodLabel)
        {
            var salesData = await _db.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= end && 
                           (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed"))
                .ToListAsync();

            var groupedData = salesData
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { 
                    Date = g.Key, 
                    TotalSales = g.Sum(o => o.FinalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(g => g.Date)
                .ToList();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            csv.AppendLine($"SALES TREND REPORT - {periodLabel}");
            csv.AppendLine("Date,Order Count,Total Revenue");

            foreach (var s in groupedData)
            {
                csv.AppendLine($"{s.Date:yyyy-MM-dd},{s.OrderCount},\"{s.TotalSales:N2}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateInventoryReportCSVAsync()
        {
            var todayUtc = DateTime.UtcNow.Date;
            
            var ingredients = await _db.Ingredients
                .Where(i => !i.IsArchived)
                .OrderBy(i => i.Name)
                .ToListAsync();

            var logsToday = await _db.InventoryLogs
                .Where(l => l.LogDate >= todayUtc && l.IngredientID != null)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.Append('\uFEFF');
            
            csv.AppendLine("LJP IT SOLUTIONS - INVENTORY AUDIT");
            csv.AppendLine($"Date: {DateTime.Now:MMMM dd, yyyy}");
            csv.AppendLine($"Generated By: System");
            csv.AppendLine();
            
            csv.AppendLine("Ingredient Name,Current Stock,Unit,Stock In (Today),Used (Today),Status,Threshold");

            foreach (var ing in ingredients)
            {
                var ingLogs = logsToday.Where(l => l.IngredientID == ing.IngredientID).ToList();
                var stockedToday = ingLogs.Where(l => l.QuantityChange > 0).Sum(l => l.QuantityChange);
                var usedToday = Math.Abs(ingLogs.Where(l => l.QuantityChange < 0).Sum(l => l.QuantityChange));
                
                string status = "In Stock";
                if (ing.StockQuantity <= 0) status = "OUT OF STOCK";
                else if (ing.StockQuantity <= ing.LowStockThreshold) status = "LOW STOCK";

                csv.AppendLine($"\"{ing.Name}\",{ing.StockQuantity:N2},{ing.Unit},{stockedToday:N2},{usedToday:N2},{status},{ing.LowStockThreshold:N2}");
            }

            csv.AppendLine();
            csv.AppendLine("--- END OF INVENTORY REPORT ---");

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<FinanceData> GetFinanceDataAsync()
        {
            var revenue = await _db.Orders.Where(o => (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => o.FinalAmount);
            var expenses = await _db.Expenses.SumAsync(e => e.Amount);
            
            var last6Months = Enumerable.Range(0, 6).Select(i => DateTime.Today.AddMonths(-5 + i)).ToList();
            var financeData = new FinanceData
            {
                TotalRevenue = revenue,
                TotalExpenses = expenses ?? 0,
                RecentExpenses = await _db.Expenses.Include(e => e.User).OrderByDescending(e => e.ExpenseDate).Take(50).ToListAsync(),
                RecentTransactions = await _db.Orders.OrderByDescending(o => o.OrderDate).Take(50).ToListAsync()
            };

            foreach (var month in last6Months)
            {
                var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = start.AddMonths(1).AddSeconds(-1);
                
                financeData.TrendLabels.Add(month.ToString("MMM"));
                financeData.IncomeTrend.Add(await _db.Orders.Where(o => o.OrderDate >= start && o.OrderDate <= end).SumAsync(o => o.FinalAmount));
                var expenseSum = await _db.Expenses.Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end).SumAsync(e => e.Amount);
                financeData.ExpenseTrend.Add(expenseSum ?? 0);
            }

            return financeData;
        }

        public async Task<AdminDashboardData> GetAdminDashboardDataAsync()
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            var weekStart = today.AddDays(-6);
            
            var orders = await _db.Orders
                .Where(o => o.OrderDate >= weekStart && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .Select(o => new { o.OrderDate, o.FinalAmount })
                .ToListAsync();

            var todayOrdersCount = await _db.Orders.CountAsync(o => o.OrderDate.Date == today);
            var yesterdayOrdersCount = await _db.Orders.CountAsync(o => o.OrderDate.Date == yesterday);
            double growth = yesterdayOrdersCount > 0 ? (double)(todayOrdersCount - yesterdayOrdersCount) / yesterdayOrdersCount * 100 : 0;

            var last7Days = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToList();
            var trendLabels = new List<string>();
            var trendData = new List<decimal>();

            foreach (var day in last7Days)
            {
                trendLabels.Add(day.ToString("MMM dd"));
                var dailySum = orders.Where(o => o.OrderDate.Date == day).Sum(o => o.FinalAmount);
                trendData.Add(dailySum);
            }

            var lowStockCount = await _db.Ingredients.CountAsync(i => i.StockQuantity <= i.LowStockThreshold);
            var riskAlerts = new List<string>();
            if (lowStockCount > 3) riskAlerts.Add($"{lowStockCount} critical inventory items require immediate replenishment.");

            return new AdminDashboardData
            {
                PortfolioValue = await _db.Products.CountAsync(),
                DailyThroughput = todayOrdersCount,
                ThroughputGrowth = growth,
                TotalGrossRevenue = await _db.Orders.Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)").SumAsync(o => o.FinalAmount),
                SalesTrendLabels = trendLabels,
                SalesTrendData = trendData,
                RoleDistribution = await _db.Users
                    .GroupBy(u => u.Role)
                    .Select(g => new RoleStat { Role = g.Key, Count = g.Count() })
                    .ToListAsync(),
                RecentActivityLogs = await _db.AuditLogs
                    .Include(a => a.User)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(5)
                    .ToListAsync(),
                RiskAlerts = riskAlerts
            };
        }


        public async Task<AdminReportsData> GetAdminReportsDataAsync()
        {
            var totalRevenue = await _db.Orders
                .Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)")
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
            
            var totalOrders = await _db.Orders.CountAsync();
            var activePromotions = await _db.Promotions.CountAsync(p => p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);
            
            var recentTransactions = await _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            return new AdminReportsData
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                ActivePromotions = activePromotions,
                RecentTransactions = recentTransactions
            };
        }

        public async Task<ManagerDashboardData> GetManagerDashboardDataAsync()
        {
            var today = DateTime.Today;
            var sevenDaysAgo = today.AddDays(-6);
            var thirtyDaysAgo = today.AddDays(-29);
            var sixtyDaysAgo = thirtyDaysAgo.AddDays(-30);

            // Chart Data: Daily Revenue (Last 7 Days)
            var recentOrdersForChart = await _db.Orders
                .Where(o => o.OrderDate >= sevenDaysAgo && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed"))
                .Select(o => new { o.OrderDate, o.FinalAmount })
                .ToListAsync();

            var dailyRevenueLabels = new List<string>();
            var dailyRevenueData = new List<decimal>();

            for (int i = 0; i < 7; i++)
            {
                var date = sevenDaysAgo.AddDays(i);
                dailyRevenueLabels.Add(date.ToString("MMM dd"));
                dailyRevenueData.Add(recentOrdersForChart.Where(o => o.OrderDate.Date == date.Date).Sum(o => o.FinalAmount));
            }

            // Category Distribution
            var categoryData = await _db.OrderDetails
                .Include(od => od.Product)
                    .ThenInclude(p => p.Category)
                .GroupBy(od => od.Product.Category != null ? od.Product.Category.CategoryName : "Uncategorized")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            // Growth Calculations
            var currentRevenue = await _db.Orders.Where(o => o.OrderDate >= thirtyDaysAgo && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
            var previousRevenue = await _db.Orders.Where(o => o.OrderDate >= sixtyDaysAgo && o.OrderDate < thirtyDaysAgo && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
            double revenueGrowth = previousRevenue > 0 ? (double)((currentRevenue - previousRevenue) / previousRevenue * 100) : 0;

            var currentOrdersCount = await _db.Orders.CountAsync(o => o.OrderDate >= thirtyDaysAgo);
            var previousOrdersCount = await _db.Orders.CountAsync(o => o.OrderDate >= sixtyDaysAgo && o.OrderDate < thirtyDaysAgo);
            double orderGrowth = previousOrdersCount > 0 ? (double)(currentOrdersCount - previousOrdersCount) / (double)previousOrdersCount * 100 : 0;

            // Hourly Sales Performance
            var hourlyData = await _db.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo)
                .GroupBy(o => o.OrderDate.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            var peakHoursLabels = Enumerable.Range(0, 24).Select(h => DateTime.Today.AddHours(h).ToString("hh tt")).ToList();
            var peakHoursData = Enumerable.Range(0, 24).Select(h => hourlyData.FirstOrDefault(x => x.Hour == h)?.Count ?? 0).ToList();

            var dailyExpenses = await _db.Expenses.Where(e => e.ExpenseDate >= thirtyDaysAgo).SumAsync(e => e.Amount) / 30;
            var dailyRevenue = currentRevenue / 30;

            return new ManagerDashboardData
            {
                TotalProducts = await _db.Products.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalOrders = await _db.Orders.CountAsync(),
                TotalRevenue = currentRevenue,
                
                RevenueGrowth = revenueGrowth,
                OrderGrowth = orderGrowth,
                
                DailyRevenueLabels = dailyRevenueLabels,
                DailyRevenueData = dailyRevenueData,
                CategoryLabels = categoryData.Select(c => c.Name).ToList(),
                CategoryData = categoryData.Select(c => c.Count).ToList(),

                LowStockIngredients = await _db.Ingredients
                    .Where(i => i.StockQuantity > 0 && i.StockQuantity <= i.LowStockThreshold)
                    .OrderBy(i => i.StockQuantity)
                    .Take(5)
                    .ToListAsync(),
                RecentOrders = await _db.Orders
                    .Include(o => o.Cashier)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync(),
                TopProducts = await GetTopProductsAsync(5),
                PeakHoursLabels = peakHoursLabels,
                PeakHoursData = peakHoursData,
                BurnRate = dailyRevenue > 0 ? (dailyExpenses ?? 0) / dailyRevenue * 100 : 0,
                AvgOrderValue = currentOrdersCount > 0 ? currentRevenue / currentOrdersCount : 0
            };
        }


        public async Task<AdminManagerReportsData> GetAdminManagerReportsDataAsync()
        {
            var inventoryValue = await _db.Expenses
                .Where(e => e.Category == "Supplies")
                .SumAsync(e => e.Amount) ?? 0;
            
            var lowStockCount = await _db.Products.CountAsync(p => p.StockQuantity < 20);
            
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastMonthStart = startOfMonth.AddMonths(-1);
            var lastMonthEnd = startOfMonth.AddSeconds(-1);
            
            var monthlyRevenue = await _db.Orders
                .Where(o => o.OrderDate >= startOfMonth && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
                
            var lastMonthRevenue = await _db.Orders
                .Where(o => o.OrderDate >= lastMonthStart && o.OrderDate <= lastMonthEnd && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
            
            var staff = await _db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Role)
                .Take(10)
                .ToListAsync();

            var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-29);
            var dailyRevenue = await _db.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(o => o.FinalAmount) })
                .ToListAsync();

            var revenueLabels = new List<string>();
            var revenueValues = new List<decimal>();

            for (int i = 0; i < 30; i++)
            {
                var date = thirtyDaysAgo.AddDays(i);
                revenueLabels.Add(date.ToString("MMM dd"));
                var total = dailyRevenue.FirstOrDefault(d => d.Date == date)?.Total ?? 0;
                revenueValues.Add(total);
            }

            var categoryData = await _db.OrderDetails
                .Include(od => od.Product)
                    .ThenInclude(p => p.Category)
                .Where(od => od.Order.OrderDate >= thirtyDaysAgo)
                .GroupBy(od => od.Product.Category != null ? od.Product.Category.CategoryName : "Uncategorized")
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            return new AdminManagerReportsData
            {
                InventoryValue = inventoryValue,
                LowStockCount = lowStockCount,
                MonthlyRevenue = monthlyRevenue,
                LastMonthRevenue = lastMonthRevenue,
                Staff = staff,
                RevenueLabels = revenueLabels,
                RevenueData = revenueValues,
                CategoryLabels = categoryData.Select(c => c.Category).ToList(),
                CategoryData = categoryData.Select(c => c.Count).ToList()
            };
        }

        public async Task<AdminCashierReportsData> GetAdminCashierReportsDataAsync()
        {
            var today = DateTime.Today;
            var weekStart = today.AddDays(-6);
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var salesQuery = _db.Orders
                .Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)");

            var yesterdaysSales = await salesQuery
                .Where(o => o.OrderDate >= today.AddDays(-1) && o.OrderDate < today)
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;

            var weeklySales = await salesQuery
                .Where(o => o.OrderDate >= weekStart)
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;

            var monthlySales = await salesQuery
                .Where(o => o.OrderDate >= monthStart)
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;

            var todaysOrders = await salesQuery
                .Where(o => o.OrderDate >= today)
                .Select(o => new { o.FinalAmount, o.OrderDate })
                .ToListAsync();

            var todaysSales = todaysOrders.Sum(o => o.FinalAmount);
            var todaysTransactions = todaysOrders.Count;
            var avgTransactionValue = todaysTransactions > 0 ? todaysSales / todaysTransactions : 0;
            
            var topItem = await _db.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.Order.OrderDate >= today)
                .GroupBy(od => od.Product.ProductName)
                .Select(g => new { ProductName = g.Key, Count = g.Sum(od => od.Quantity) })
                .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

            var recentTransactions = await _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .Where(o => o.PaymentMethod == "Cash")
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();
                
            var lowStockItems = await _db.Products
                .Where(p => p.StockQuantity < 20)
                .OrderBy(p => p.StockQuantity)
                .Take(5)
                .ToListAsync();

            var hourlyData = await _db.Orders
                .Where(o => o.OrderDate >= today)
                .GroupBy(o => o.OrderDate.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            var hourlyLabels = new List<string>();
            var hourlyValues = new List<int>();

            for (int i = 0; i < 24; i++)
            {
                hourlyLabels.Add(i < 12 ? $"{i}am" : (i == 12 ? "12pm" : $"{i-12}pm"));
                var count = hourlyData.FirstOrDefault(h => h.Hour == i)?.Count ?? 0;
                hourlyValues.Add(count);
            }

            return new AdminCashierReportsData
            {
                TodaysSales = todaysSales,
                YesterdaysSales = yesterdaysSales,
                WeeklySales = weeklySales,
                MonthlySales = monthlySales,
                TodaysTransactions = todaysTransactions,
                AvgTransactionValue = avgTransactionValue,
                TopItemName = topItem?.ProductName ?? "N/A",
                TopItemCount = (int)(topItem?.Count ?? 0),
                RecentTransactions = recentTransactions,
                LowStockItems = lowStockItems,
                HourlyLabels = hourlyLabels,
                HourlyData = hourlyValues
            };
        }

        public async Task<AdminMarketingReportsData> GetAdminMarketingReportsDataAsync()
        {
            var activePromotionsList = await _db.Promotions
                .Where(p => p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            
            var totalCustomers = await _db.Customers.CountAsync();
            
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var newCustomersThisMonth = await _db.Customers
                .Where(c => c.Orders.Any())
                .Select(c => new { FirstOrderDate = c.Orders.Min(o => o.OrderDate) })
                .CountAsync(x => x.FirstOrderDate >= startOfMonth);

            return new AdminMarketingReportsData
            {
                ActivePromotions = activePromotionsList,
                ActivePromotionsCount = activePromotionsList.Count,
                TotalCustomers = totalCustomers,
                NewCustomersThisMonth = newCustomersThisMonth
            };
        }

        public async Task<MarketingDashboardData> GetMarketingDashboardDataAsync()
        {
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);
            
            var totalCustomers = await _db.Customers.Include(c => c.Orders).ToListAsync();
            var paidOrders = await _db.Orders
                .Where(o => o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")
                .ToListAsync();
            
            var activeMemberIds = paidOrders.Where(o => o.OrderDate >= sevenDaysAgo && o.CustomerID != null)
                .Select(o => o.CustomerID).Distinct().ToHashSet();
            
            var totalPointsRedeemed = await _db.RewardRedemptions.SumAsync(r => (long)r.PointsRedeemed);
            var marketingData = CalculateCoreMarketingMetrics(totalCustomers, paidOrders, totalPointsRedeemed);
            marketingData.ActiveMembersThisWeek = activeMemberIds.Count;

            // Retentions and VIP Performance
            var firstOrderDict = BuildFirstOrderDictionary(totalCustomers);
            marketingData.NewPatronsCount = firstOrderDict.Values.Count(d => d >= sevenDaysAgo);
            marketingData.ReturningPatronsCount = Math.Max(0, marketingData.ActiveMembersThisWeek - marketingData.NewPatronsCount);
            marketingData.RetentionRatePercent = marketingData.ActiveMembersThisWeek > 0 ? (double)marketingData.ReturningPatronsCount * 100 / marketingData.ActiveMembersThisWeek : 0;
            marketingData.VIPPerformance = BuildVIPPerformanceList(totalCustomers);

            // Tiers and Trends
            var activePromotions = await _db.Promotions.Where(p => p.IsActive && p.EndDate >= now).ToListAsync();
            PopulateTierAnalysis(marketingData, totalCustomers, paidOrders);
            await PopulateTrendAnalysis(marketingData, paidOrders, activePromotions);

            return marketingData;
        }

        private static MarketingDashboardData CalculateCoreMarketingMetrics(List<Customer> totalCustomers, List<Order> paidOrders, long totalPointsRedeemed)
        {
            var totalRevenue = paidOrders.Sum(o => o.FinalAmount);
            var loyaltyRevenue = paidOrders.Where(o => o.CustomerID != null).Sum(o => o.FinalAmount);
            var totalPointsLiability = totalCustomers.Sum(c => (long)c.Points);
            var promoOrders = paidOrders.Where(o => o.PromotionID != null).ToList();
            var promoRevenue = promoOrders.Sum(o => o.FinalAmount);
            var promoDiscount = promoOrders.Sum(o => o.DiscountAmount);

            return new MarketingDashboardData
            {
                LoyaltyRevenueContributionPercent = totalRevenue > 0 ? (loyaltyRevenue / totalRevenue) * 100 : 0,
                RedemptionRatePercent = totalPointsLiability + totalPointsRedeemed > 0 ? (double)totalPointsRedeemed / (totalPointsLiability + totalPointsRedeemed) * 100 : 0,
                AvgPointsPerCustomer = totalCustomers.Count > 0 ? (double)totalPointsLiability / totalCustomers.Count : 0,
                CampaignROI = promoDiscount > 0 ? (double)(promoRevenue / promoDiscount) : 0,
                CampaignRevenue = promoRevenue,
                TotalCampaignDiscount = promoDiscount,
                ConversionVolume = paidOrders.Count,
                NetworkEquity = totalPointsLiability,
                AudiencePenetration = totalCustomers.Count,
                ConversionRate = totalCustomers.Count > 0 ? (double)paidOrders.Count / totalCustomers.Count : 0
            };
        }

        private static Dictionary<int, DateTime> BuildFirstOrderDictionary(List<Customer> totalCustomers)
        {
            return totalCustomers
                .Where(c => c.Orders.Count > 0)
                .ToDictionary(c => c.CustomerID, c => c.Orders.Min(o => o.OrderDate));
        }

        private static List<CustomerBasicInfo> BuildVIPPerformanceList(List<Customer> totalCustomers)
        {
            return totalCustomers
                .Select(c => new CustomerBasicInfo
                {
                    CustomerID = c.CustomerID,
                    FullName = c.FullName,
                    Points = c.Points,
                    TotalSpend = c.Orders.Where(o => o.PaymentStatus == "Paid" || o.PaymentStatus == "Completed").Sum(o => o.FinalAmount),
                    TransactionCount = c.Orders.Count(o => o.PaymentStatus == "Paid" || o.PaymentStatus == "Completed"),
                    LastVisit = c.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefault()?.OrderDate
                })
                .OrderByDescending(x => x.TotalSpend)
                .Take(10)
                .ToList();
        }

        private static void PopulateTierAnalysis(MarketingDashboardData data, List<Customer> totalCustomers, List<Order> paidOrders)
        {
            var tiers = new[] {
                new { Name = "Gold", Min = 500, Max = int.MaxValue },
                new { Name = "Silver", Min = 300, Max = 499 },
                new { Name = "Bronze", Min = 100, Max = 299 },
                new { Name = "Member", Min = 0, Max = 99 }
            };

            data.TierLabels = tiers.Select(t => t.Name).ToList();
            data.TierData = new List<int>();
            data.TierRevenueData = new List<decimal>();

            foreach (var t in tiers)
            {
                var tierCustomers = totalCustomers.Where(c => c.Points >= t.Min && c.Points <= t.Max).Select(c => c.CustomerID).ToHashSet();
                data.TierData.Add(tierCustomers.Count);
                data.TierRevenueData.Add(paidOrders.Where(o => o.CustomerID != null && tierCustomers.Contains(o.CustomerID.Value)).Sum(o => o.FinalAmount));
            }
        }

        private async Task PopulateTrendAnalysis(MarketingDashboardData data, List<Order> paidOrders, List<Promotion> activePromotions)
        {
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.UtcNow.Date.AddDays(-6 + i)).ToList();
            var redemptionsHistory = await _db.RewardRedemptions.Where(r => r.RedemptionDate >= last7Days[0]).ToListAsync();

            data.PerformanceLabels = last7Days.Select(d => d.ToString("MMM dd")).ToList();
            data.PerformanceData = new List<int>();
            data.PointsTrendLabels = data.PerformanceLabels;
            data.PointsIssuedData = new List<int>();
            data.PointsRedeemedData = new List<int>();
            data.CampaignRevenueTrendLabels = data.PerformanceLabels;
            data.CampaignRevenueTrendData = new List<decimal>();

            var groupedPaidOrders = paidOrders.GroupBy(o => o.OrderDate.Date).ToDictionary(g => g.Key, g => g.ToList());
            var groupedRedemptions = redemptionsHistory.GroupBy(r => r.RedemptionDate.Date).ToDictionary(g => g.Key, g => g.ToList());

            data.PerformanceData = last7Days.Select(day => groupedPaidOrders.ContainsKey(day.Date) ? groupedPaidOrders[day.Date].Count : 0).ToList();
            data.PointsIssuedData = last7Days.Select(day => groupedPaidOrders.ContainsKey(day.Date) ? (int)Math.Floor(groupedPaidOrders[day.Date].Where(o => o.CustomerID != null).Sum(o => o.FinalAmount / 100m)) : 0).ToList();
            data.PointsRedeemedData = last7Days.Select(day => groupedRedemptions.ContainsKey(day.Date) ? groupedRedemptions[day.Date].Sum(r => r.PointsRedeemed) : 0).ToList();
            data.CampaignRevenueTrendData = last7Days.Select(day => groupedPaidOrders.ContainsKey(day.Date) ? groupedPaidOrders[day.Date].Where(o => o.PromotionID != null).Sum(o => o.FinalAmount) : 0).ToList();

            var campaignStats = paidOrders.Where(o => o.PromotionID != null).GroupBy(o => o.PromotionID)
                .Select(g => new { ID = g.Key, Revenue = g.Sum(o => o.FinalAmount) }).OrderByDescending(x => x.Revenue).ToList();

            data.ActiveCampaigns = activePromotions.Count;
            data.AvgRevenuePerCampaign = activePromotions.Count > 0 ? paidOrders.Where(o => o.PromotionID != null).Sum(o => o.FinalAmount) / activePromotions.Count : 0;
            
            if (campaignStats.Count > 0)
            {
                data.TopCampaignName = activePromotions.FirstOrDefault(p => p.PromotionID == campaignStats[0].ID)?.PromotionName ?? "N/A";
                data.BottomCampaignName = activePromotions.FirstOrDefault(p => p.PromotionID == campaignStats[campaignStats.Count-1].ID)?.PromotionName ?? "N/A";
            }
        }

        public async Task<SuperAdminDashboardData> GetSuperAdminDashboardDataAsync()
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);
            
            var auditLogs = await _db.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync();

            var securityLogs = await _db.SecurityLogs
                .Include(s => s.User)
                .OrderByDescending(s => s.Timestamp)
                .Take(5)
                .ToListAsync();

            var userCount = await _db.Users.CountAsync();
            var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
            
            // User Growth (last 30 days)
            var usersThirtyDaysAgo = await _db.Users.CountAsync(u => u.CreatedAt < thirtyDaysAgo);
            double growth = 0;
            if (usersThirtyDaysAgo > 0)
            {
                growth = ((double)userCount - usersThirtyDaysAgo) / usersThirtyDaysAgo * 100;
            }
            else if (userCount > 0)
            {
                growth = 100;
            }
            
            // Security Metrics - Pulling from SecurityLogs table
            var failedLoginsRecent = await _db.SecurityLogs.CountAsync(s => s.EventType == "LoginFailure" && s.Timestamp >= now.AddDays(-1));
            var lockedOutUsersCount = await _db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now);

            var securityAlerts = new List<string>();
            if (failedLoginsRecent > 15) securityAlerts.Add("Anomalous failed login volume detected in last 24h.");
            if (lockedOutUsersCount > 0) securityAlerts.Add($"{lockedOutUsersCount} accounts are restricted due to security lockouts.");

            // System Health / Uptime (Dynamic based on security events)
            double healthScore = 99.99;
            if (failedLoginsRecent > 20) healthScore -= 0.05;
            if (lockedOutUsersCount > 5) healthScore -= 0.10;
            if (securityAlerts.Any()) healthScore -= 0.02;

            return new SuperAdminDashboardData
            {
                AuditLogs = auditLogs,
                SecurityLogs = securityLogs,
                UserCount = userCount,
                ActiveUsers = activeUsers,
                FailedLoginsCount = failedLoginsRecent,
                LockedOutUsersCount = lockedOutUsersCount,
                SystemUptime = healthScore.ToString("F2") + "%",
                GrowthIndex = (growth >= 0 ? "+" : "") + growth.ToString("N1") + "%",
                SecurityAlerts = securityAlerts
            };
        }
    }
}
