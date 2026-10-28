using AccessLab.Domain.Authentication;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Infrastructure.Identity;

public sealed class PasswordPolicyService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "password1234", "password1234567890", "password12345678",
        "admin", "admin123", "administrator", "qwerty", "qwerty123", "qwertyuiop",
        "123456", "12345678", "123456789", "1234567890", "1234567890123456",
        "welcome", "welcome123", "welcome12345678", "letmein", "iloveyou", "monkey",
        "football", "accesslab", "accesslab123456", "changeme", "changeme12345678",
        "testtesttesttest", "aaaaaaaaaaaaaaaa", "1111111111111111", "0000000000000000",
        "haslo", "haslo123", "haslo1234567890", "passwordpassword", "adminadminadmin1"
    };

    public PasswordPolicyService(ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<string?> ValidateAsync(ApplicationUser user, string next, string? previous = null)
    {
        var settings = await _db.SecuritySettings.AsNoTracking().FirstOrDefaultAsync();
        int minimum = settings?.MinimumPasswordLength ?? 16;
        if (string.IsNullOrEmpty(next) || next.Length < minimum || next.Length > 128)
            return $"Hasło musi mieć od {minimum} do 128 znaków.";
        if (Forbidden.Contains(next) || next.Contains(user.UserName ?? "__no_username__", StringComparison.OrdinalIgnoreCase))
            return "To hasło jest niedozwolone.";
        if (settings?.RequirePasswordDistance == true && previous is not null && Distance(previous, next) < 4)
            return "Nowe hasło jest zbyt podobne do poprzedniego (odległość mniejsza niż 4).";
        if (user.PasswordHash is not null && _hasher.VerifyHashedPassword(user, user.PasswordHash, next)
            != PasswordVerificationResult.Failed)
            return "Nie można ponownie użyć wcześniejszego hasła.";
        var hashes = await _db.PasswordHistories.Where(h => h.UserId == user.Id)
            .Select(h => h.PasswordHash).ToListAsync();
        if (hashes.Any(hash => _hasher.VerifyHashedPassword(user, hash, next) != PasswordVerificationResult.Failed))
            return "Nie można ponownie użyć wcześniejszego hasła.";
        return null;
    }

    public void RememberOldHash(ApplicationUser user, string? oldHash)
    {
        if (!string.IsNullOrEmpty(oldHash))
            _db.PasswordHistories.Add(new PasswordHistory { UserId = user.Id, PasswordHash = oldHash });
    }

    private static int Distance(string a, string b)
    {
        var prev = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            var curr = new int[b.Length + 1];
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            prev = curr;
        }
        return prev[b.Length];
    }
}
