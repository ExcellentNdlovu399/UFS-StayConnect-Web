using Microsoft.AspNetCore.Identity;

namespace UFSStayConnect.Infrastructure
{
    public class UserValidator : UserValidator<IdentityUser>
    {
        public override async Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
        {
            var result = await base.ValidateAsync(manager, user);
            var errors = result.Succeeded ? new List<IdentityError>() : result.Errors.ToList();
            var email = user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!(email.EndsWith("@ufs.ac.za") || email.EndsWith("@ufs4life.ac.za")))
            {
                errors.Add(new IdentityError { Code = "EmailStudentNumberError", Description = "Only ufs.ac.za or ufs4life.ac.za email address is allowed" });
            }
            return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
        }
    }
}
