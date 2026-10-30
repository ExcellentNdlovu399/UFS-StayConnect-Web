using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class Role
    {
        [Key]
        public int RoleID { get; set; }

        [Required]
        [StringLength(50)]
        public string RoleName { get; set; } = string.Empty;

        // Navigation property
        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
