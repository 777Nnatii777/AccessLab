namespace AccessLab.Domain.Authentication;

public sealed class PasswordHistory
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
