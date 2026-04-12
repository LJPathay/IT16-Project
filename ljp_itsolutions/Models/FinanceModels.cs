using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class Expense
    {
        [Key]
        [Required]
        public int? ExpenseID { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public decimal? Amount { get; set; }

        [Required]
        public DateTime? ExpenseDate { get; set; } = DateTime.UtcNow;

        [Required]
        public string Category { get; set; } = "General"; // Supplies, Utilities, Salary, etc.

        public string? ReferenceNumber { get; set; }
 
        public Guid? CreatedBy { get; set; }
        [ForeignKey("CreatedBy")]
        public virtual User? User { get; set; }

    }
}
