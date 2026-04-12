using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class User
    {
        [Key]
        public Guid UserID { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }


        public string? Password { get; set; } 

        [Required]
        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Accessibility Settings
        public bool IsHighContrast { get; set; } = false;
        public string FontSize { get; set; } = "default"; 
        public bool ReduceMotion { get; set; } = false;

        public string? ProfilePictureUrl { get; set; }
        
        // Brute Force protection
        public int AccessFailedCount { get; set; } = 0;
        public DateTimeOffset? LockoutEnd { get; set; }

        // Password Reset
        public string? PasswordResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Advanced Security
        public DateTime LastPasswordChange { get; set; } = DateTime.UtcNow;
        public bool TwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public bool RequiresPasswordChange { get; set; } = false;

        // Legacy compatibility
    [NotMapped]
        public Guid Id { get => UserID; set => UserID = value; }
    }

    public class UserPasswordHistory
    {
        [Key]
        public int Id { get; set; }

        public Guid UserID { get; set; }
        [ForeignKey("UserID")]
        public virtual User User { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
 
    public class TrustedDevice
    {
        [Key]
        public int ID { get; set; }
 
        public Guid UserID { get; set; }
        [ForeignKey("UserID")]
        public virtual User User { get; set; } = null!;
 
        [Required]
        public string DeviceToken { get; set; } = string.Empty;
 
        [StringLength(50)]
        public string? IPAddress { get; set; }
 
        public DateTime Expiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
