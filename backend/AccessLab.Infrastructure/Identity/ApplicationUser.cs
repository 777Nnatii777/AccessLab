using Microsoft.AspNetCore.Identity;

namespace AccessLab.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public bool IsBlocked { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public DateTimeOffset? TemporaryPasswordExpiresAtUtc { get; set; }
    public DateTimeOffset? PasswordChangedAtUtc { get; set; }
}
