using Microsoft.AspNetCore.Identity;
using UFSStayConnect.Models;

namespace UFSStayConnect.Data
{
    public static class SeedIdentityData
    {
// =====================================================
// ALLOWED EMAIL DOMAINS
// =====================================================


    private static readonly string[] AllowedEmailDomains =
    {
        "@ufs.ac.za",
        "@ufs4life.ac.za"
    };



        // =====================================================
        // CHECK IF EMAIL IS ALLOWED
        // =====================================================

        public static bool IsAllowedEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim().ToLower();

            return AllowedEmailDomains.Any(domain =>
                email.EndsWith(domain));
        }


        // =====================================================
        // SEED ROLES AND DEFAULT ADMIN
        // =====================================================

        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                serviceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();


            // =================================================
            // CREATE ROLES
            // =================================================

            string[] roles =
            {
            "Admin",
            "FinancialOfficer",
            "Customer"
        };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }


            // =================================================
            // CREATE DEFAULT ADMIN
            // =================================================

            string adminEmail = "admin@ufs.ac.za";
            string adminPassword = "Admin@123456";

            // Make sure the default admin uses an allowed domain
            if (!IsAllowedEmail(adminEmail))
            {
                throw new Exception(
                    "The default administrator email domain is not allowed.");
            }


            var adminUser =
                await userManager.FindByEmailAsync(adminEmail);


            // =================================================
            // CREATE ADMIN IF IT DOES NOT EXIST
            // =================================================

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result =
                    await userManager.CreateAsync(
                        adminUser,
                        adminPassword);


                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(
                        adminUser,
                        "Admin");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine(
                            $"Admin creation error: {error.Description}");
                    }
                }
            }
            else
            {
                // Make sure existing admin has Admin role

                if (!await userManager.IsInRoleAsync(
                        adminUser,
                        "Admin"))
                {
                    await userManager.AddToRoleAsync(
                        adminUser,
                        "Admin");
                }
            }
        }
 }


}
