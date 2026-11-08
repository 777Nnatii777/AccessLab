namespace AccessLab.Domain.Authentication;

// Jeden wiersz = jedna próba logowania (udana albo zatrzymana przez któreś zabezpieczenie).
public sealed class SecurityEvent
{
    public long Id { get; set; }
    public DateTimeOffset AttemptedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    // Success, BadPassword, Locked, CaptchaFailed, Honeypot, CanaryAccount, IpLimited
    public string Type { get; set; } = string.Empty;
}
