using System.Security.Cryptography;
using System.Text;
using AccessLab.Application.Authentication;

using AccessLab.Domain.Authentication;
using AccessLab.Infrastructure.Identity;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace AccessLab.Infrastructure.Authentication;

public class SessionService : ISessionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public SessionService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<CreatedSession> CreateAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);

        if (user is null ||
            user.IsBlocked ||
            await _users.IsLockedOutAsync(user))
        {
            throw new InvalidOperationException(
                "Nie można utworzyć sesji dla tego konta.");
        }

        var token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var now = DateTimeOffset.UtcNow;

        var expiresAt = user.MustChangePassword
            ? now.AddMinutes(10)
            : now.AddHours(8);

        var session = new UserSession
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            ExpiresAtUtc = expiresAt,
            IsPasswordChangeOnly = user.MustChangePassword
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();

        return new CreatedSession
        {
            Token = token,
            ExpiresAtUtc = expiresAt
        };
    }
}