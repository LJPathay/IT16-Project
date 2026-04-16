using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace ljp_itsolutions.Services
{
    public class OrderCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCleanupService> _logger;

        public OrderCleanupService(IServiceProvider serviceProvider, ILogger<OrderCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System Monitor Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupStaleOrders();
                    await ProcessAutomatedBackups();
                    await PurgeOldRecords();
                    
                    // Simple logic to run expiration check once every 24 hours
                    if (DateTime.UtcNow.Hour == 8 && DateTime.UtcNow.Minute < 15) // Run around 8am
                    {
                        await CheckExpiringIngredients();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during system monitoring cycle.");
                }

                // Check every 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private async Task CheckExpiringIngredients()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var threeDaysFromNow = DateTime.UtcNow.AddDays(3);
                
                var expiring = await db.Ingredients
                    .Where(i => i.ExpiryDate != null && i.ExpiryDate <= threeDaysFromNow && i.ExpiryDate >= DateTime.UtcNow)
                    .ToListAsync();

                foreach (var item in expiring)
                {
                    // Check if notification already exists to avoid spam
                    var exists = await db.Notifications.AnyAsync(n => n.Title == "Expiration Warning" && n.Message.Contains(item.Name) && n.CreatedAt > DateTime.UtcNow.AddDays(-1));
                    
                    if (!exists)
                    {
                        db.Notifications.Add(new Notification
                        {
                            Title = "Expiration Warning",
                            Message = $"{item.Name} is expiring on {item.ExpiryDate:MM/dd/yyyy}!",
                            Type = "danger",
                            IconClass = "fas fa-history",
                            CreatedAt = DateTime.UtcNow,
                            TargetUrl = "/Manager/Inventory"
                        });
                    }
                }
                await db.SaveChangesAsync();
            }
        }

        private async Task CleanupStaleOrders()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Find Pending digital orders older than 30 minutes
                var cutoff = DateTime.UtcNow.AddMinutes(-30);
                var staleOrders = await db.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductRecipes)
                                .ThenInclude(pr => pr.Ingredient)
                    .Where(o => o.PaymentMethod.Contains("Paymongo") && o.PaymentStatus == "Pending" && o.OrderDate < cutoff)
                    .AsSplitQuery()
                    .ToListAsync();

                if (staleOrders.Any())
                {
                    _logger.LogInformation("Found {Count} stale digital orders. Restoring inventory...", staleOrders.Count);

                    foreach (var order in staleOrders)
                    {
                        foreach (var detail in order.OrderDetails)
                        {
                            var product = detail.Product;
                            if (product == null) continue;

                            if (product.ProductRecipes != null && product.ProductRecipes.Any())
                            {
                                foreach (var recipe in product.ProductRecipes)
                                {
                                    recipe.Ingredient.StockQuantity += (recipe.QuantityRequired * detail.Quantity);
                                    
                                    db.InventoryLogs.Add(new InventoryLog
                                    {
                                        IngredientID = recipe.IngredientID,
                                        QuantityChange = (recipe.QuantityRequired * detail.Quantity),
                                        ChangeType = "Restoration (Cancelled Order)",
                                        LogDate = DateTime.UtcNow,
                                        Remarks = $"Restored from stale order #{order.OrderID.ToString().Substring(0, 8)}"
                                    });
                                }
                            }
                            else
                            {
                                product.StockQuantity += detail.Quantity;
                            }
                        }

                        order.PaymentStatus = "Expired/Cancelled";
                        await LogAudit(db, "Automatic Stale Order Cleanup", $"Order #{order.OrderID} timed out. Stock restored.");
                    }

                    await db.SaveChangesAsync();
                }
            }
        }

        private async Task ProcessAutomatedBackups()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "BackupSchedule");
                    if (setting == null || string.Equals(setting.SettingValue, "Manual", StringComparison.OrdinalIgnoreCase))
                        return;

                    var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    var files = new DirectoryInfo(path).GetFiles("*.json").OrderByDescending(f => f.CreationTimeUtc).ToList();
                    var latestFile = files.FirstOrDefault();

                    bool shouldRun = false;
                    if (latestFile == null)
                    {
                        shouldRun = true;
                    }
                    else
                    {
                        var timeSinceLastBackup = DateTime.UtcNow - latestFile.CreationTimeUtc;
                        if (string.Equals(setting.SettingValue, "Daily", StringComparison.OrdinalIgnoreCase) && timeSinceLastBackup.TotalHours >= 24)
                            shouldRun = true;
                        else if (string.Equals(setting.SettingValue, "Weekly", StringComparison.OrdinalIgnoreCase) && timeSinceLastBackup.TotalDays >= 7)
                            shouldRun = true;
                    }

                    if (shouldRun)
                    {
                        _logger.LogInformation("Starting automated system backup...");
                        var fileName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                        var fullPath = Path.Combine(path, fileName);
                        var backupData = new {
                            GeneratedAt = DateTime.UtcNow,
                            Users = await db.Users.AsNoTracking().ToListAsync(),
                            Products = await db.Products.AsNoTracking().ToListAsync(),
                            Orders = await db.Orders.AsNoTracking().Include(o => o.OrderDetails).ToListAsync(),
                            Settings = await db.SystemSettings.AsNoTracking().ToListAsync()
                        };

                        var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
                        await File.WriteAllTextAsync(fullPath, json);
                        await LogAudit(db, "Automated Backup", "Scheduled automated backup completed successfully.");
                        
                        // Delete older backups if there are too many (e.g., keep the last 5)
                        if (files.Count > 4)
                        {
                            foreach (var old in files.Skip(4))
                            {
                                old.Delete();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Automated backup generation failed.");
                }
            }
        }

        private async Task PurgeOldRecords()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    // 1. Audit / Security Log Retention
                    var retentionDaysSetting = await db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "LogRetentionDays");
                    if (retentionDaysSetting != null && int.TryParse(retentionDaysSetting.SettingValue, out int days) && days > 0)
                    {
                        var cutoff = DateTime.UtcNow.AddDays(-days);
                        
                        var oldAudit = db.AuditLogs.Where(a => a.Timestamp < cutoff);
                        var oldSecurity = db.SecurityLogs.Where(s => s.Timestamp < cutoff);

                        if (oldAudit.Any() || oldSecurity.Any())
                        {
                            db.AuditLogs.RemoveRange(oldAudit);
                            db.SecurityLogs.RemoveRange(oldSecurity);
                            _logger.LogInformation("Purged records older than {Days} days.", days);
                        }
                    }

                    // 2. Archived User Retention (Fixed at 5 years / 1825 days for IA compliance)
                    var userCutoff = DateTime.UtcNow.AddDays(-1825);
                    var staleArchivedUsers = db.ArchivedUsers.Where(u => u.ArchivedAt < userCutoff);
                    if (staleArchivedUsers.Any())
                    {
                        db.ArchivedUsers.RemoveRange(staleArchivedUsers);
                        _logger.LogInformation("Purged archived users older than 5 years.");
                    }

                    // NOTE: PAYMENTS ARE NEVER PURGED. 
                    // Financial records must maintain a permanent audit trail.

                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Data retention purge failed.");
                }
            }
        }

        private async Task LogAudit(ApplicationDbContext db, string action, string details)
        {
            try
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    UserID = null // System action
                });
            }
            catch { /* fail silent for log */ }
        }
    }
}
