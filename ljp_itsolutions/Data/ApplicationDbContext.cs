using Microsoft.EntityFrameworkCore;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<InventoryLog> InventoryLogs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<ProductRecipe> ProductRecipes { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<RewardRedemption> RewardRedemptions { get; set; }
        public DbSet<CashShift> CashShifts { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RecipeTemplate> RecipeTemplates { get; set; }
        public DbSet<RecipeTemplateIngredient> RecipeTemplateIngredients { get; set; }
        public DbSet<ArchivedProduct> ArchivedProducts { get; set; }
        public DbSet<ArchivedUser> ArchivedUsers { get; set; }
        public DbSet<UserPasswordHistory> UserPasswordHistories { get; set; }
        public DbSet<SecurityLog> SecurityLogs { get; set; }
        public DbSet<TrustedDevice> TrustedDevices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.UserID);
                b.Property(u => u.Username).IsRequired().HasMaxLength(50);
                b.Property(u => u.FullName).IsRequired().HasMaxLength(100);
                b.Property(u => u.IsActive).HasDefaultValue(true);
                b.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                b.Property(u => u.IsHighContrast).HasDefaultValue(false);
                b.Property(u => u.FontSize).HasDefaultValue("default");
                b.Property(u => u.ReduceMotion).HasDefaultValue(false);
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(o => o.OrderID);
                b.Property(o => o.OrderDate).HasDefaultValueSql("GETUTCDATE()");
                b.HasOne(o => o.Cashier).WithMany().HasForeignKey(o => o.CashierID).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrderDetail>(b =>
            {
                b.HasKey(od => od.OrderDetailID);
                b.HasOne(od => od.Order).WithMany(o => o.OrderDetails).HasForeignKey(od => od.OrderID);
                b.Property(od => od.UnitPrice).HasPrecision(18, 2);
                b.Property(od => od.Subtotal).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Product>(b =>
            {
                b.Property(p => p.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<ArchivedProduct>(b =>
            {
                b.Property(p => p.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.Property(o => o.TotalAmount).HasPrecision(18, 2);
                b.Property(o => o.DiscountAmount).HasPrecision(18, 2);
                b.Property(o => o.FinalAmount).HasPrecision(18, 2);
                b.Property(o => o.RefundedAmount).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Payment>(b =>
            {
                b.Property(p => p.AmountPaid).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Promotion>(b =>
            {
                b.Property(p => p.DiscountValue).HasPrecision(18, 2);
            });
            
            modelBuilder.Entity<Ingredient>(b =>
            {
                b.Property(i => i.StockQuantity).HasPrecision(18, 3);
                b.Property(i => i.LowStockThreshold).HasPrecision(18, 3);
            });

            modelBuilder.Entity<ProductRecipe>(b =>
            {
                b.Property(pr => pr.QuantityRequired).HasPrecision(18, 5); // Allow small amounts like 0.005 kg
            });

            modelBuilder.Entity<Expense>(b =>
            {
                b.Property(e => e.Amount).HasPrecision(18, 2);
            });

            modelBuilder.Entity<InventoryLog>(b =>
            {
                b.Property(l => l.QuantityChange).HasPrecision(18, 3);
            });

            modelBuilder.Entity<RecipeTemplateIngredient>(b =>
            {
                b.Property(r => r.Quantity).HasPrecision(18, 3);
            });

            // Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { CategoryID = 1, CategoryName = "Coffee" },
                new Category { CategoryID = 2, CategoryName = "Tea" },
                new Category { CategoryID = 3, CategoryName = "Pastry" }
            );

            // Seed Admin User (Password is '123' - pre-hashed for Identity V3)
            var adminId = Guid.Parse("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f");
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserID = adminId,
                    Username = "admin",
                    FullName = "System Admin",
                    Email = "admin@coffee.local",
                    Role = UserRoles.Admin,
                    IsActive = true
                    // Password hash removed here as DbInitializer handles robust seeding/resetting to '123'
                }
            );
            // Seed Products
            modelBuilder.Entity<Product>().HasData(
                new Product { ProductID = 1, ProductName = "Espresso Blend", CategoryID = 1, Price = 3.50m, StockQuantity = 50, ImageURL = null, IsAvailable = true },
                new Product { ProductID = 2, ProductName = "Caramel Macchiato", CategoryID = 1, Price = 5.25m, StockQuantity = 30, ImageURL = null, IsAvailable = true },
                new Product { ProductID = 3, ProductName = "Earl Grey Tea", CategoryID = 2, Price = 4.00m, StockQuantity = 20, ImageURL = null, IsAvailable = true },
                new Product { ProductID = 4, ProductName = "Chocolate Croissant", CategoryID = 3, Price = 4.50m, StockQuantity = 15, ImageURL = null, IsAvailable = true },
                new Product { ProductID = 5, ProductName = "Blueberry Muffin", CategoryID = 3, Price = 3.75m, StockQuantity = 5, ImageURL = null, IsAvailable = true }
            );

            // Seed Ingredients
            modelBuilder.Entity<Ingredient>().HasData(
                new Ingredient { IngredientID = 1, Name = "Espresso Beans", StockQuantity = 10, Unit = "kg", LowStockThreshold = 2 },
                new Ingredient { IngredientID = 2, Name = "Fresh Milk", StockQuantity = 20, Unit = "L", LowStockThreshold = 5 },
                new Ingredient { IngredientID = 3, Name = "Caramel Syrup", StockQuantity = 5000, Unit = "ml", LowStockThreshold = 1000 },
                new Ingredient { IngredientID = 4, Name = "Fructose", StockQuantity = 5000, Unit = "ml", LowStockThreshold = 1000 },
                new Ingredient { IngredientID = 5, Name = "Pastry Flour", StockQuantity = 50, Unit = "kg", LowStockThreshold = 10 }
            );

            // Seed Product Recipes
            modelBuilder.Entity<ProductRecipe>().HasData(
                // Espresso Blend uses 18g beans
                new ProductRecipe { RecipeID = 1, ProductID = 1, IngredientID = 1, QuantityRequired = 0.018m },
                // Caramel Macchiato: 18g beans, 250ml milk, 30ml caramel, 10ml fructose
                new ProductRecipe { RecipeID = 2, ProductID = 2, IngredientID = 1, QuantityRequired = 0.018m },
                new ProductRecipe { RecipeID = 3, ProductID = 2, IngredientID = 2, QuantityRequired = 0.250m },
                new ProductRecipe { RecipeID = 4, ProductID = 2, IngredientID = 3, QuantityRequired = 30m },
                new ProductRecipe { RecipeID = 5, ProductID = 2, IngredientID = 4, QuantityRequired = 10m }
            );

            // Seed Expenses
            modelBuilder.Entity<Expense>().HasData(
                new Expense { ExpenseID = 1, Title = "Electricity Bill - Jan", Amount = 150.00m, Description = "Monthly power consumption", Category = "Utilities", ExpenseDate = new DateTime(2026, 1, 30, 0, 0, 0, DateTimeKind.Utc) },
                new Expense { ExpenseID = 2, Title = "Milk Supply Restock", Amount = 85.50m, Description = "50L Fresh Milk", Category = "Supplies", ExpenseDate = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc) },
                new Expense { ExpenseID = 3, Title = "Coffee Beans Cargo", Amount = 320.00m, Description = "20kg Arabica beans", Category = "Supplies", ExpenseDate = new DateTime(2026, 2, 8, 0, 0, 0, DateTimeKind.Utc) }
            );

            // Seed Promotions
            modelBuilder.Entity<Promotion>().HasData(
                new Promotion { PromotionID = 1, PromotionName = "Early Bird Discount", DiscountType = "Percentage", DiscountValue = 10, StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Promotion { PromotionID = 2, PromotionName = "Grand Opening Special", DiscountType = "Fixed", DiscountValue = 5, StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc), IsActive = true }
            );
        }
    }
}
