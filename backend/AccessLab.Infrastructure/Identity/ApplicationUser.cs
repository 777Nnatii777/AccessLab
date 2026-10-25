using Microsoft.AspNetCore.Identity;

namespace AccessLab.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public bool IsBlocked { get; set; } = false;
    public bool MustChangePassword { get; set; } = true;
}