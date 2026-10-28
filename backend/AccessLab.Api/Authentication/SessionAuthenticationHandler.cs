using System.Security.Claims;
using System.Text.Encodings.Web;
using AccessLab.Infrastructure.Authentication;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccessLab.Api.Authentication;

public sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApplicationDbContext _db;
    public SessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ApplicationDbContext db) : base(options, logger, encoder)
        => _db = db;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue("__Host-AccessLabSession", out var token))
            return AuthenticateResult.NoResult();
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit))
            return AuthenticateResult.Fail("Nieprawidłowa sesja.");

        var hash = SessionTokenHasher.Hash(token);
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.TokenHash == hash);
        var now = DateTimeOffset.UtcNow;
        if (session is null || session.RevokedAtUtc != null || session.ExpiresAtUtc <= now)
            return AuthenticateResult.Fail("Sesja wygasła.");
        var settings = await _db.SecuritySettings.AsNoTracking().FirstOrDefaultAsync();
        var idleMinutes = Math.Clamp(settings?.SessionIdleMinutes ?? 30, 1, 240);
        if (session.LastActivityAtUtc.AddMinutes(idleMinutes) <= now)
            return AuthenticateResult.Fail("Sesja wygasła z powodu bezczynności.");
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId);
        if (user is null || user.IsBlocked || (user.LockoutEnd.HasValue && user.LockoutEnd > now))
            return AuthenticateResult.Fail("Konto niedostępne.");
        
        if (user.MustChangePassword != session.IsPasswordChangeOnly)
            return AuthenticateResult.Fail("Stan sesji jest nieaktualny.");
        if (!session.IsPasswordChangeOnly && settings is { PasswordValidityDays: > 0 } &&
            (user.PasswordChangedAtUtc is null || user.PasswordChangedAtUtc.Value.AddDays(settings.PasswordValidityDays) <= now))
            return AuthenticateResult.Fail("Hasło wygasło. Zaloguj się ponownie.");

        var level = session.IsPasswordChangeOnly ? "password" :
            session.IsTwoFactorPending ? "totp" : "full";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new("session_id", session.Id.ToString()),
            new("session_level", level)
        };
        if (level == "full")
        {
            var roles = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .Where(n => n != null).ToListAsync();
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role!));
        }
        if (now - session.LastActivityAtUtc >= TimeSpan.FromMinutes(1))
        {
            session.LastActivityAtUtc = now;
            await _db.SaveChangesAsync();
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
