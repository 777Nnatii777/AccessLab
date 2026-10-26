using AccessLab.Application.Authentication;

using Microsoft.AspNetCore.Identity;

namespace AccessLab.Infrastructure.Identity;

public class LoginService : ILoginService
{
    private readonly UserManager<ApplicationUser> _users;

    public LoginService(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task<CheckedLogin> CheckCredentialsAsync(
    LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrEmpty(request.Password))
        {
            return new CheckedLogin(LoginCheckResult.Failed);
        }

        var user = await _users.FindByNameAsync(request.Login);

        if (user is null)
        {
            return new CheckedLogin(LoginCheckResult.Failed);
        }


        var passwordCorrect = await _users.CheckPasswordAsync(user,request.Password);

        if (!passwordCorrect)
        {
            var failureResult = await _users.AccessFailedAsync(user);

            if (!failureResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Nie udało się zapisać nieudanej próby logowania.");
            }

            return new CheckedLogin(LoginCheckResult.Failed);
        }

        var resetResult = await _users.ResetAccessFailedCountAsync(user);

        if (!resetResult.Succeeded)
        {
            return new CheckedLogin(LoginCheckResult.Failed);
        }
        if (user.MustChangePassword)
        {
            return new CheckedLogin(
                LoginCheckResult.PasswordChangeRequired,
                user.Id);
        }

        return new CheckedLogin(
            LoginCheckResult.ReadyToSignIn,
            user.Id);

       


        
    }
}