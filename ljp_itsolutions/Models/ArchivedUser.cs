using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    /// <summary>
    /// Cold Storage for deleted user accounts.
    /// RETENTION POLICY: Retained for 1825 days (5 years) before automated purging.
    /// </summary>
    [Table("Archived_Users")]
    public class ArchivedUser
    {
        [Key]
        public int ArchivedUserID { get; set; }

        public Guid OriginalUserID { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;

        public string? Reason { get; set; }
    }
}
