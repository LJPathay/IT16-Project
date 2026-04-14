using Microsoft.EntityFrameworkCore;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Identity;

namespace ljp_itsolutions.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var strategy = db.Database.CreateExecutionStrategy();

            strategy.Execute(() =>
            {
                try
                {
                    Console.WriteLine("Applying pending migrations...");
                    db.Database.Migrate();

                    Console.WriteLine("Normalizing database schema and records...");
                    
                    string createTableSql = @"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SystemSettings]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [dbo].[SystemSettings](
                                [SettingKey] [nvarchar](450) NOT NULL,
                                [SettingValue] [nvarchar](max) NOT NULL,
                                CONSTRAINT [PK_SystemSettings] PRIMARY KEY CLUSTERED ([SettingKey] ASC)
                            )
                        END";
                    db.Database.ExecuteSqlRaw(createTableSql);

                    // Normalize roles
                    var usersToFix = db.Users.ToList();
                    foreach (var u in usersToFix)
                    {
                        var normalizedRole = u.Role?.Trim();
                        if (string.IsNullOrEmpty(normalizedRole)) continue;

                        string? targetRole = null;
                        if (string.Equals(normalizedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.Admin;
                        else if (string.Equals(normalizedRole, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.SuperAdmin;
                        else if (string.Equals(normalizedRole, UserRoles.Manager, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.Manager;
                        else if (string.Equals(normalizedRole, UserRoles.Cashier, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.Cashier;
                        else if (string.Equals(normalizedRole, UserRoles.MarketingStaff, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.MarketingStaff;

                        if (targetRole != null && u.Role != targetRole)
                        {
                            u.Role = targetRole;
                        }
                    }

                    // Seed Categories
                    if (!db.Categories.Any())
                    {
                        Console.WriteLine("Seeding categories...");
                        db.Categories.AddRange(
                            new Category { CategoryName = "Coffee" },
                            new Category { CategoryName = "Tea" },
                            new Category { CategoryName = "Pastry" }
                        );
                    }

                    // Seed Users
                    var hasher = new PasswordHasher<User>();
                    var rolesToSeed = new[] { 
                        (Username: "superadmin", FullName: "System SuperAdmin", Email: "superadmin@coffee.local", Role: UserRoles.SuperAdmin),
                        (Username: "admin", FullName: "System Admin", Email: "admin@coffee.local", Role: UserRoles.Admin),
                        (Username: "manager", FullName: "Store Manager", Email: "manager@coffee.local", Role: UserRoles.Manager),
                        (Username: "cashier", FullName: "Store Cashier", Email: "cashier@coffee.local", Role: UserRoles.Cashier),
                        (Username: "marketing", FullName: "Marketing Staff", Email: "marketing@coffee.local", Role: UserRoles.MarketingStaff)
                    };

                    foreach (var s in rolesToSeed)
                    {
                        var user = db.Users.FirstOrDefault(u => u.Username == s.Username);
                        if (user == null)
                        {
                            Console.WriteLine($"Seeding user: {s.Username}");
                            var newUser = new User
                            {
                                UserID = Guid.NewGuid(),
                                Username = s.Username,
                                FullName = s.FullName,
                                Email = s.Email,
                                Role = s.Role,
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            newUser.Password = hasher.HashPassword(newUser, "123");
                            db.Users.Add(newUser);
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(user.Role) || !string.Equals(user.Role, s.Role, StringComparison.OrdinalIgnoreCase))
                            {
                                user.Role = s.Role;
                            }
                            if (string.IsNullOrEmpty(user.Password))
                            {
                                user.Password = hasher.HashPassword(user, "123");
                            }
                        }
                    }

                    db.SaveChanges();

                    // Seed Products
                    if (!db.Products.Any())
                    {
                        Console.WriteLine("Seeding products...");
                        var coffeeCat = db.Categories.FirstOrDefault(c => c.CategoryName == "Coffee");
                        if (coffeeCat != null)
                        {
                            db.Products.AddRange(
                                new Product { ProductName = "Espresso", Price = 2.5m, StockQuantity = 100, CategoryID = coffeeCat.CategoryID, IsAvailable = true },
                                new Product { ProductName = "Latte", Price = 3.5m, StockQuantity = 80, CategoryID = coffeeCat.CategoryID, IsAvailable = true }
                            );
                            db.SaveChanges();
                        }
                    }

                    // Seed Recipe Templates (only once)
                    if (!db.RecipeTemplates.Any())
                    {
                        Console.WriteLine("Seeding recipe templates...");

                        RecipeTemplate T(string p, params (string n, decimal q, string u)[] ing) => new() { 
                            ProductName = p, 
                            Ingredients = ing.Select(i => new RecipeTemplateIngredient { IngredientName = i.n, Quantity = i.q, Unit = i.u }).ToList() 
                        };

                        var templates = new List<RecipeTemplate>
                        {
                            T("Espresso", ("Coffee Beans", 18m, "g")),
                            T("Americano", ("Coffee Beans", 18m, "g"), ("Hot Water", 150m, "ml")),
                            T("Cappuccino", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 150m, "ml"), ("Milk Foam", 30m, "ml")),
                            T("Latte", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 200m, "ml"), ("Milk Foam", 20m, "ml")),
                            T("Caramel Macchiato", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 200m, "ml"), ("Caramel Syrup", 20m, "ml"), ("Vanilla Syrup", 10m, "ml")),
                            T("Mocha", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 180m, "ml"), ("Chocolate Syrup", 25m, "ml")),
                            T("Flat White", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 160m, "ml")),
                            T("Vanilla Latte", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 200m, "ml"), ("Vanilla Syrup", 20m, "ml")),
                            T("Hazelnut Latte", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 200m, "ml"), ("Hazelnut Syrup", 20m, "ml")),
                            T("Spanish Latte", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 180m, "ml"), ("Condensed Milk", 30m, "ml")),
                            T("Iced Americano", ("Coffee Beans", 18m, "g"), ("Water", 120m, "ml"), ("Ice", 100m, "g")),
                            T("Iced Latte", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 200m, "ml"), ("Ice", 120m, "g")),
                            T("Iced Caramel Macchiato", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 200m, "ml"), ("Caramel Syrup", 20m, "ml"), ("Ice", 120m, "g")),
                            T("Iced Mocha", ("Coffee Beans", 18m, "g"), ("Fresh Milk", 180m, "ml"), ("Chocolate Syrup", 25m, "ml"), ("Ice", 120m, "g")),
                            T("Cold Brew Coffee", ("Coffee Beans", 25m, "g"), ("Water", 250m, "ml")),
                            T("Hot Chocolate", ("Cocoa Powder", 25m, "g"), ("Fresh Milk", 200m, "ml"), ("Sugar", 15m, "g")),
                            T("Matcha Latte", ("Matcha Powder", 15m, "g"), ("Fresh Milk", 200m, "ml"), ("Sugar", 10m, "g")),
                            T("Iced Matcha Latte", ("Matcha Powder", 15m, "g"), ("Fresh Milk", 200m, "ml"), ("Ice", 120m, "g"), ("Sugar", 10m, "g")),
                            T("Chai Latte", ("Chai Powder", 20m, "g"), ("Fresh Milk", 200m, "ml")),
                            T("Iced Chocolate", ("Cocoa Powder", 25m, "g"), ("Fresh Milk", 200m, "ml"), ("Ice", 120m, "g")),
                            T("Strawberry Milk", ("Strawberry Syrup", 30m, "ml"), ("Fresh Milk", 200m, "ml")),
                            T("Vanilla Milkshake", ("Fresh Milk", 200m, "ml"), ("Vanilla Syrup", 20m, "ml"), ("Ice Cream", 1m, "scoop")),
                            T("Cookies and Cream Milkshake", ("Fresh Milk", 200m, "ml"), ("Ice Cream", 1m, "scoop"), ("Crushed Cookies", 30m, "g")),
                            T("Mango Smoothie", ("Mango Puree", 150m, "ml"), ("Ice", 120m, "g"), ("Sugar Syrup", 15m, "ml")),
                            T("Strawberry Smoothie", ("Strawberry Puree", 150m, "ml"), ("Ice", 120m, "g"), ("Sugar Syrup", 15m, "ml")),
                        };

                        db.RecipeTemplates.AddRange(templates);
                        db.SaveChanges();
                        Console.WriteLine($"Seeded {templates.Count} recipe templates.");
                    }

                    Console.WriteLine("Database initialization completed.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Seeding error: {ex.Message}");
                    if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
                    throw; // Rethrow to let the strategy handle retries
                }
            });
        }

        public static void ImportFromBackup(IServiceProvider serviceProvider, string jsonPath)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"Backup file not found at: {jsonPath}");
                return;
            }

            try
            {
                var json = File.ReadAllText(jsonPath);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var backup = System.Text.Json.JsonSerializer.Deserialize<BackupData>(json, options);

                if (backup == null) return;

                // 1. Import Users
                if (backup.Users != null)
                {
                    foreach (var u in backup.Users)
                    {
                        if (!db.Users.Any(existing => existing.UserID == u.UserID || existing.Username == u.Username))
                        {
                            db.Users.Add(u);
                        }
                    }
                    db.SaveChanges();
                }

                // 2. Import Products
                if (backup.Products != null)
                {
                    // For products, we might need IDENTITY_INSERT if we want to keep IDs
                    // But for now, we'll just add them if they don't exist by name
                    foreach (var p in backup.Products)
                    {
                        if (!db.Products.Any(existing => existing.ProductName == p.ProductName))
                        {
                            // Resetting ID to 0 to let DB generate it if identity is on
                            // unless we want to keep the exact IDs
                            var newProduct = new Product
                            {
                                ProductName = p.ProductName,
                                CategoryID = p.CategoryID,
                                Price = p.Price,
                                StockQuantity = p.StockQuantity,
                                LowStockThreshold = p.LowStockThreshold,
                                ImageURL = p.ImageURL,
                                IsAvailable = p.IsAvailable,
                                IsArchived = p.IsArchived
                            };
                            db.Products.Add(newProduct);
                        }
                    }
                    db.SaveChanges();
                }

                // 3. Import Orders (requires existing Users)
                if (backup.Orders != null)
                {
                    foreach (var o in backup.Orders)
                    {
                        if (!db.Orders.Any(existing => existing.OrderID == o.OrderID))
                        {
                            // Ensure the cashier exists (fallback to superadmin if not)
                            if (!db.Users.Any(u => u.UserID == o.CashierID))
                            {
                                var defaultAdmin = db.Users.FirstOrDefault(u => u.Role == UserRoles.SuperAdmin);
                                if (defaultAdmin != null) o.CashierID = defaultAdmin.UserID;
                            }

                            // Remove navigation properties that might cause tracking issues
                            o.Cashier = null!;
                            o.Customer = null;
                            o.Promotion = null;
                            
                            db.Orders.Add(o);
                        }
                    }
                    db.SaveChanges();
                }

                Console.WriteLine("Backup import completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import error: {ex.Message}");
            }
        }
    }

    public class BackupData
    {
        public string? GeneratedAt { get; set; }
        public List<User>? Users { get; set; }
        public List<Product>? Products { get; set; }
        public List<Order>? Orders { get; set; }
    }
}
