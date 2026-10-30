using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string UserType { get; set; } = string.Empty;

        public string? StudentNumber { get; set; }

        public string? EmployeeNumber { get; set; }

        [StringLength(20)]
        public string AccountStatus { get; set; } = "Active";

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool PasswordChangeRequired { get; set; } = false;
    }
}
