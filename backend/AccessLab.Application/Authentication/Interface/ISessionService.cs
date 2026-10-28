namespace AccessLab.Application.Authentication;

public interface ISessionService
{
    Task<CreatedSession> CreateAsync(string userId);
    Task RevokeAllAsync(string userId);
}
