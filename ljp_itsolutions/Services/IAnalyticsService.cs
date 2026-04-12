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
        Task<byte[]> GenerateInventoryReportCSVAsync();
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
        public List<SecurityLog> SecurityLogs { get; set; } = new();
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
        public decimal TotalSpend { get; set; }
        public DateTime? LastVisit { get; set; }
        public int TransactionCount { get; set; }
    }

    public class MarketingDashboardData
    {
        // Top KPIs
        public decimal LoyaltyRevenueContributionPercent { get; set; }
        public int ActiveMembersThisWeek { get; set; }
        public double RedemptionRatePercent { get; set; }
        public double AvgPointsPerCustomer { get; set; }
        public double CampaignROI { get; set; }
        public decimal CampaignRevenue { get; set; }
        public double RetentionRatePercent { get; set; }
        public decimal AvgRevenuePerCampaign { get; set; }
        public decimal TotalCampaignDiscount { get; set; }
        
        public string TopCampaignName { get; set; } = string.Empty;
        public string BottomCampaignName { get; set; } = string.Empty;
        
        public int AudiencePenetration { get; set; } 

        // Core Metrics
        public int ActiveCampaigns { get; set; } 
        public int ConversionVolume { get; set; } 
        public long NetworkEquity { get; set; } 
        public double ConversionRate { get; set; } 
        public double ReachGrowth { get; set; } 
        
        // Retention & Share
        public int ReturningPatronsCount { get; set; }
        public int NewPatronsCount { get; set; }
        
        // Charting
        public List<string> PerformanceLabels { get; set; } = new(); 
        public List<int> PerformanceData { get; set; } = new(); 
        
        // Tier Matrix
        public List<string> TierLabels { get; set; } = new();
        public List<int> TierData { get; set; } = new();
        public List<decimal> TierRevenueData { get; set; } = new();
        
        // Points Benchmark
        public List<string> PointsTrendLabels { get; set; } = new();
        public List<int> PointsIssuedData { get; set; } = new();
        public List<int> PointsRedeemedData { get; set; } = new();

        // Strategic Trends
        public List<string> CampaignRevenueTrendLabels { get; set; } = new();
        public List<decimal> CampaignRevenueTrendData { get; set; } = new();

        // VIP / Leaderboard
        public List<CustomerBasicInfo> VIPPerformance { get; set; } = new(); 
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
        public decimal WeeklySales { get; set; }
        public decimal MonthlySales { get; set; }
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
        public int PortfolioValue { get; set; } 
        public int DailyThroughput { get; set; }
        public double ThroughputGrowth { get; set; } 
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
