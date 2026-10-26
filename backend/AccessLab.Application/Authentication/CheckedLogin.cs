namespace AccessLab.Application.Authentication;

public record CheckedLogin(
    LoginCheckResult Status,
    string? UserId = null);