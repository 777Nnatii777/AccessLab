namespace AccessLab.Application.Authentication;

public class CreatedSession
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}