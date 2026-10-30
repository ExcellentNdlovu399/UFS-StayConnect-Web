using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Models;

namespace UFSStayConnect.Data
{
    public class ApplicationIdentityDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationIdentityDbContext(
            DbContextOptions<ApplicationIdentityDbContext> options)
            : base(options)
        {
        }
    }
}