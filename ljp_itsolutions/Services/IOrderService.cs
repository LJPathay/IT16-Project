using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Services
{
    public interface IOrderService
    {
        Task<OrderResult> ProcessOrderAsync(OrderRequest request, Guid cashierId);
        Task<Promotion?> ValidatePromotionAsync(string promoCode, int? customerId);
        Task<decimal> GetTaxRateAsync();
        Task<bool> VoidOrderAsync(Guid orderId, string remarks);
        Task<bool> RefundOrderAsync(Guid orderId, string remarks);
    }

    public class OrderRequest
    {
        public List<int> ProductIds { get; set; } = new();
        public string? PaymentMethod { get; set; }
        public int? CustomerId { get; set; }
        public string? PromoCode { get; set; }
        public string? PaymentStatus { get; set; }
        public string? ReferenceNumber { get; set; }
        public int RedemptionTier { get; set; }
    }

    public class OrderResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Order? Order { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
