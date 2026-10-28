namespace AccessLab.Domain.Authentication;

public sealed class SecuritySettings
{
    public int Id { get; set; } = 1;
    public int MinimumPasswordLength { get; set; } = 16;
    public bool RequirePasswordDistance { get; set; } = false;
    public int PasswordValidityDays { get; set; } = 90;
    public int SessionIdleMinutes { get; set; } = 30;
}
