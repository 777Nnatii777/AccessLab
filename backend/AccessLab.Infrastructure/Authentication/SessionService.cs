using System.Security.Cryptography;
using AccessLab.Application.Authentication;
using AccessLab.Domain.Authentication;
using AccessLab.Infrastructure.Identity;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Infrastructure.Authentication;

public sealed class SessionService : ISessionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public SessionService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<CreatedSession> CreateAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null || user.IsBlocked || await _users.IsLockedOutAsync(user))
            throw new InvalidOperationException("Nie można utworzyć sesji.");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(user.MustChangePassword ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(8));
        var session = new UserSession
        {
            UserId = user.Id,
            TokenHash = SessionTokenHasher.Hash(token),
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            ExpiresAtUtc = expires,
            IsPasswordChangeOnly = user.MustChangePassword,
            IsTwoFactorPending = !user.MustChangePassword && user.TwoFactorEnabled
        };
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();
        return new CreatedSession { Token = token, ExpiresAtUtc = expires };
    }

    public async Task RevokeAllAsync(string userId)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.UserSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, now));
    }
}
