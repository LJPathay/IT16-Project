using System.Threading.Tasks;
using ljp_itsolutions.Models;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Services
{
    public interface IInventoryService
    {
        Task<bool> IntakeStockAsync(int ingredientId, decimal quantity, DateTime date, string remarks, DateTime? expiryDate = null);
        Task<bool> UpdateThresholdAsync(int ingredientId, decimal threshold);
        Task RevertOrderInventoryAsync(Order order);
    }

    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _db;

        public InventoryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<bool> IntakeStockAsync(int ingredientId, decimal quantity, DateTime date, string remarks, DateTime? expiryDate = null)
        {
            var ingredient = await _db.Ingredients.FindAsync(ingredientId);
            if (ingredient == null) return false;

            ingredient.StockQuantity += quantity;
            ingredient.LastStockedDate = date;
            if (expiryDate.HasValue) ingredient.ExpiryDate = expiryDate.Value;

            _db.InventoryLogs.Add(new InventoryLog
            {
                IngredientID = ingredientId,
                QuantityChange = quantity,
                ChangeType = "Intake",
                LogDate = date,
                Remarks = remarks
            });

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateThresholdAsync(int ingredientId, decimal threshold)
        {
            var ingredient = await _db.Ingredients.FindAsync(ingredientId);
            if (ingredient == null) return false;

            ingredient.LowStockThreshold = threshold;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task RevertOrderInventoryAsync(Order order)
        {
            if (order == null || order.OrderDetails == null) return;

            foreach (var detail in order.OrderDetails)
            {
                var product = await _db.Products
                    .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                    .FirstOrDefaultAsync(p => p.ProductID == detail.ProductID);

                if (product != null)
                {
                    if (product.ProductRecipes != null && product.ProductRecipes.Any())
                    {
                        foreach (var recipe in product.ProductRecipes)
                        {
                            recipe.Ingredient.StockQuantity += (recipe.QuantityRequired * detail.Quantity);
                            
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                IngredientID = recipe.IngredientID,
                                QuantityChange = (recipe.QuantityRequired * detail.Quantity),
                                ChangeType = "Reversal",
                                LogDate = DateTime.UtcNow,
                                Remarks = $"Restored from Voided/Refunded Order #{order.OrderID.ToString().Substring(0, 8)} ({product.ProductName})"
                            });
                        }
                    }
                    else
                    {
                        product.StockQuantity += detail.Quantity;
                        
                        _db.InventoryLogs.Add(new InventoryLog
                        {
                            ProductID = product.ProductID,
                            QuantityChange = detail.Quantity,
                            ChangeType = "Reversal",
                            LogDate = DateTime.UtcNow,
                            Remarks = $"Stock restored from Order #{order.OrderID.ToString().Substring(0, 8)}"
                        });
                    }
                }
            }
            await _db.SaveChangesAsync();
        }
    }
}
