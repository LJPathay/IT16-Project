using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class OrderDetail
    {
        [Key]
        public int OrderDetailID { get; set; }

        public Guid OrderID { get; set; }
        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; } = null!;

        public int ProductID { get; set; }
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Subtotal { get; set; }
        
        public int RefundedQuantity { get; set; }
    }

    /// <summary>
    /// Financial Record: Immutable audit trail for all incoming payments.
    /// IA POLICY: Payments must NEVER be deleted. Use Void/Refund status instead.
    /// </summary>
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        public Guid OrderID { get; set; }
        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; } = null!;

        public decimal AmountPaid { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Completed";
    }

    public class InventoryLog
    {
        [Key]
        public int LogID { get; set; }

        public int? ProductID { get; set; }
        [ForeignKey("ProductID")]
        public virtual Product? Product { get; set; }

        public int? IngredientID { get; set; }
        [ForeignKey("IngredientID")]
        public virtual Ingredient? Ingredient { get; set; }

        public decimal QuantityChange { get; set; }

        [Required]
        [StringLength(50)]
        public string ChangeType { get; set; } = string.Empty;

        public DateTime LogDate { get; set; } = DateTime.Now;

        [StringLength(1000)]
        public string? Remarks { get; set; }
    }

    public class AuditLog
    {
        [Key]
        public int AuditID { get; set; }

        public Guid? UserID { get; set; }
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty;

        public string? Details { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }
    }

    public class SecurityLog
    {
        [Key]
        public int SecurityID { get; set; }

        public Guid? UserID { get; set; }
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }

        [Required]
        public string EventType { get; set; } = "Generic"; // e.g., LoginFailure, MFAChange, PasswordReset

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? Severity { get; set; } = "Info"; // Info, Warning, Critical

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }
    }
}
