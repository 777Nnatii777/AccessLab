using AccessLab.Domain.Authentication;
using AccessLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Api.Security;

// Zapis prób logowania, limit porażek z jednego IP i konta-pułapki (canarytokens).
public sealed class SecurityEventService
{
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(15);
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SecurityEventService> _log;
    private readonly IConfiguration _config;

    public SecurityEventService(ApplicationDbContext db, ILogger<SecurityEventService> log, IConfiguration config)
    {
        _db = db; _log = log; _config = config;
    }

    public async Task AddAsync(string type, string ip, string? login)
    {
        var value = (login ?? string.Empty).Trim();
        if (value.Length > 200) value = value[..200];
        _db.SecurityEvents.Add(new SecurityEvent { Type = type, IpAddress = ip, Login = value });
        await _db.SaveChangesAsync();
    }

    // Password spraying: liczymy porażki z IP niezależnie od loginu.
    // Zwraca czas końca blokady albo null, gdy IP nie jest zablokowane.
    public async Task<DateTimeOffset?> IpBlockedUntilAsync(string ip)
    {
        var settings = await _db.SecuritySettings.AsNoTracking().FirstOrDefaultAsync();
        int limit = settings?.MaxFailedAttemptsPerIp ?? 20;
        var since = DateTimeOffset.UtcNow - IpWindow;
        var lastFailures = await _db.SecurityEvents.AsNoTracking()
            .Where(e => e.IpAddress == ip && e.AttemptedAtUtc > since && e.Type != "Success" && e.Type != "IpLimited")
            .OrderByDescending(e => e.AttemptedAtUtc)
            .Take(limit)
            .Select(e => e.AttemptedAtUtc)
            .ToListAsync();
        if (lastFailures.Count < limit) return null;
        // Blokada trwa, dopóki najstarsza z ostatnich N porażek nie wypadnie z okna 15 minut.
        return lastFailures[^1] + IpWindow;
    }

    // Zwraca opis źródła przynęty, jeśli login jest kontem-pułapką.
    public string? TrapSource(string? login)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        var traps = _config.GetSection("Canary:TrapLogins").GetChildren();
        return traps.FirstOrDefault(t => string.Equals(t.Key, login.Trim(), StringComparison.OrdinalIgnoreCase))?.Value;
    }

    public async Task CanaryAlertAsync(string ip, string login, string source)
    {
        _log.LogWarning("ALERT CANARY: ktoś użył konta-pułapki {Login} (źródło: {Source}) z IP {Ip}", login, source, ip);
        await AddAsync("CanaryAccount", ip, $"{login} ({source})");
    }
}
