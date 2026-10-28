using AccessLab.Application.Authentication;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Infrastructure.Identity;

public sealed class LoginService : ILoginService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public LoginService(UserManager<ApplicationUser> users, ApplicationDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<CheckedLogin> CheckCredentialsAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
            return new(LoginCheckResult.Failed);
        var user = await _users.FindByNameAsync(request.Login);
        if (user is null || user.IsBlocked || await _users.IsLockedOutAsync(user))
            return new(LoginCheckResult.Failed);
        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            var failed = await _users.AccessFailedAsync(user);
            if (!failed.Succeeded) throw new InvalidOperationException("Błąd licznika logowań.");
            return new(LoginCheckResult.Failed);
        }
        if (user.TemporaryPasswordExpiresAtUtc is { } expiry && expiry <= DateTimeOffset.UtcNow)
            return new(LoginCheckResult.Failed);
        if (!(await _users.ResetAccessFailedCountAsync(user)).Succeeded)
            return new(LoginCheckResult.Failed);

        var policy = await _db.SecuritySettings.AsNoTracking().FirstOrDefaultAsync();
        int days = policy?.PasswordValidityDays ?? 90;
        if (!user.MustChangePassword && days > 0 &&
            (user.PasswordChangedAtUtc is null || user.PasswordChangedAtUtc.Value.AddDays(days) <= DateTimeOffset.UtcNow))
        {
            user.MustChangePassword = true;
            if (!(await _users.UpdateAsync(user)).Succeeded)
                return new(LoginCheckResult.Failed);
        }
        return new(user.MustChangePassword ? LoginCheckResult.PasswordChangeRequired : LoginCheckResult.ReadyToSignIn, user.Id);
    }
}
