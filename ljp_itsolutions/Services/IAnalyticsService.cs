using System.Threading.Tasks;
using System.Collections.Generic;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Services
{
    public interface IAnalyticsService
    {
        Task<List<ProductSalesSummary>> GetTopProductsAsync(int count = 10);
        Task<byte[]> GenerateSalesCSVAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateAuditLogsCSVAsync();
        Task<byte[]> GenerateTacticalROICSVAsync();
        Task<byte[]> GenerateTransactionsCSVAsync();
        Task<byte[]> GenerateUsersCSVAsync();
        Task<byte[]> GenerateSalesTrendsCSVAsync(DateTime start, DateTime end, string periodLabel);
        Task<FinanceData> GetFinanceDataAsync();
        Task<AdminDashboardData> GetAdminDashboardDataAsync();
        Task<AdminReportsData> GetAdminReportsDataAsync();
        Task<ManagerDashboardData> GetManagerDashboardDataAsync();
        Task<MarketingDashboardData> GetMarketingDashboardDataAsync();
        Task<SuperAdminDashboardData> GetSuperAdminDashboardDataAsync();
        Task<AdminManagerReportsData> GetAdminManagerReportsDataAsync();
        Task<AdminCashierReportsData> GetAdminCashierReportsDataAsync();
        Task<AdminMarketingReportsData> GetAdminMarketingReportsDataAsync();
    }

    public class SuperAdminDashboardData
    {
        public List<AuditLog> AuditLogs { get; set; } = new();
        public int UserCount { get; set; }
        public int ActiveUsers { get; set; }
        public int FailedLoginsCount { get; set; }
        public int LockedOutUsersCount { get; set; }
        public string SystemUptime { get; set; } = "99.9%";
        public string GrowthIndex { get; set; } = "+0.0%";
        public List<string> SecurityAlerts { get; set; } = new();
    }

    public class CustomerBasicInfo
    {
        public int CustomerID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Points { get; set; }
    }

    public class MarketingDashboardData
    {
        public int AudiencePenetration { get; set; } 
        public int ActiveCampaigns { get; set; } 
        public int ConversionVolume { get; set; } // Total Orders
        public long NetworkEquity { get; set; } // Total Points
        public double ConversionRate { get; set; } // Orders / Customers
        public double ReachGrowth { get; set; } // today vs yesterday customer count %
        public List<string> PerformanceLabels { get; set; } = new(); // chart labels
        public List<int> PerformanceData { get; set; } = new(); // chart data
        public int ReturningPatronsCount { get; set; }
        public int NewPatronsCount { get; set; }
        public List<string> TierLabels { get; set; } = new();
        public List<int> TierData { get; set; } = new();
        public List<CustomerBasicInfo> VIPPerformance { get; set; } = new(); // Top customers
    }


    public class AdminMarketingReportsData
    {
        public List<Promotion> ActivePromotions { get; set; } = new();
        public int ActivePromotionsCount { get; set; }
        public int TotalCustomers { get; set; }
        public int NewCustomersThisMonth { get; set; }
    }

    public class AdminManagerReportsData
    {
        public decimal InventoryValue { get; set; }
        public int LowStockCount { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal LastMonthRevenue { get; set; }
        public List<User> Staff { get; set; } = new();
        public List<string> RevenueLabels { get; set; } = new();
        public List<decimal> RevenueData { get; set; } = new();
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryData { get; set; } = new();
    }

    public class AdminCashierReportsData
    {
        public decimal TodaysSales { get; set; }
        public decimal YesterdaysSales { get; set; }
        public int TodaysTransactions { get; set; }
        public decimal AvgTransactionValue { get; set; }
        public string TopItemName { get; set; } = string.Empty;
        public int TopItemCount { get; set; }
        public List<Order> RecentTransactions { get; set; } = new();
        public List<Product> LowStockItems { get; set; } = new();
        public List<string> HourlyLabels { get; set; } = new();
        public List<int> HourlyData { get; set; } = new();
    }

    public class ManagerDashboardData
    {
        public int TotalProducts { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public double RevenueGrowth { get; set; }
        public double OrderGrowth { get; set; }
        public List<string> DailyRevenueLabels { get; set; } = new();
        public List<decimal> DailyRevenueData { get; set; } = new();
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryData { get; set; } = new();
        public List<Ingredient> LowStockIngredients { get; set; } = new();
        public List<Order> RecentOrders { get; set; } = new();
        public List<ProductSalesSummary> TopProducts { get; set; } = new();
        public List<string> PeakHoursLabels { get; set; } = new();
        public List<int> PeakHoursData { get; set; } = new();
        public decimal BurnRate { get; set; } // Daily Expenses vs Revenue
        public decimal AvgOrderValue { get; set; } 
    }

    public class FinanceData
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalRevenue - TotalExpenses;
        public List<string> TrendLabels { get; set; } = new();
        public List<decimal> IncomeTrend { get; set; } = new();
        public List<decimal> ExpenseTrend { get; set; } = new();
        public List<Expense> RecentExpenses { get; set; } = new();
        public List<Order> RecentTransactions { get; set; } = new();
    }

    public class AdminDashboardData
    {
        public int PortfolioValue { get; set; } // Total Products
        public int DailyThroughput { get; set; } // Today's transactions
        public double ThroughputGrowth { get; set; } // today vs yesterday %
        public decimal TotalGrossRevenue { get; set; }
        public List<string> SalesTrendLabels { get; set; } = new();
        public List<decimal> SalesTrendData { get; set; } = new();
        public List<RoleStat> RoleDistribution { get; set; } = new();
        public List<AuditLog> RecentActivityLogs { get; set; } = new();
        public List<string> RiskAlerts { get; set; } = new();
    }

    public class RoleStat
    {
        public string Role { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class AdminReportsData
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int ActivePromotions { get; set; }
        public List<Order> RecentTransactions { get; set; } = new();
    }
}
