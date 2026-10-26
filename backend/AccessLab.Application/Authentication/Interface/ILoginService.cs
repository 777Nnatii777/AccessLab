namespace AccessLab.Application.Authentication;

public interface ILoginService
{
    Task<CheckedLogin> CheckCredentialsAsync(
        LoginRequest request);
}