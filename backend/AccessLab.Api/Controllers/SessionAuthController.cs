using System.Security.Claims;
using AccessLab.Api.Authentication;
using AccessLab.Application.Authentication;
using AccessLab.Infrastructure.Authentication;
using AccessLab.Infrastructure.Identity;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class SessionAuthController : ControllerBase
{
    private readonly ILoginService _login;
    private readonly ISessionService _sessions;
    private readonly IAntiforgery _csrf;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly PasswordPolicyService _policy;

    public SessionAuthController(ILoginService login, ISessionService sessions, IAntiforgery csrf,
        ApplicationDbContext db, UserManager<ApplicationUser> users, PasswordPolicyService policy)
    {
        _login = login;
        _sessions = sessions;
        _csrf = csrf;
        _db = db;
        _users = users;
        _policy = policy;
    }

    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        var tokens = _csrf.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var result = await _login.CheckCredentialsAsync(request);
        if (result.Status == LoginCheckResult.Failed || string.IsNullOrEmpty(result.UserId))
            return Unauthorized(new { message = "Login lub hasło niepoprawne" });
        // Revoke any session carried by the browser before replacing its cookie.
        await RevokeCurrentSessionAsync();
        var session = await _sessions.CreateAsync(result.UserId);
        Response.Cookies.Append(SessionCookies.Name, session.Token, SessionCookies.Options(session.ExpiresAtUtc));
        return Ok(new
        {
            status = result.Status.ToString(),
            requiresPasswordChange = result.Status == LoginCheckResult.PasswordChangeRequired,
            requiresTwoFactor = result.Status == LoginCheckResult.ReadyToSignIn &&
                (await _users.FindByIdAsync(result.UserId))?.TwoFactorEnabled == true
        });
    }

    [Authorize(Policy = "SessionAny")]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        login = User.Identity?.Name,
        level = User.FindFirstValue("session_level"),
        requiresPasswordChange = User.HasClaim("session_level", "password"),
        requiresTwoFactor = User.HasClaim("session_level", "totp"),
        roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
    });

    [Authorize(Policy = "SessionAny")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        await RevokeCurrentSessionAsync();
        Response.Cookies.Delete(SessionCookies.Name, SessionCookies.Options());
        return NoContent();
    }

    [Authorize(Policy = "SessionAny")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(FirstPasswordChangeRequest request, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (User.HasClaim("session_level", "totp"))
            return StatusCode(403, new { message = "Najpierw potwierdź kod TOTP." });
        if (string.IsNullOrEmpty(request.CurrentPassword) ||
            request.NewPassword != request.ConfirmNewPassword)
            return BadRequest(new { message = "Hasła nie są zgodne lub są puste." });
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _users.FindByIdAsync(userId);
        if (user is null || user.IsBlocked) return Unauthorized();
        var issue = await _policy.ValidateAsync(user, request.NewPassword, request.CurrentPassword);
        if (issue is not null) return BadRequest(new { message = issue });
        await using var tx = await _db.Database.BeginTransactionAsync();
        var previousHash = user.PasswordHash;
        var result = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = "Zmiana hasła nieudana.", errors = result.Errors.Select(e => e.Description) });
        user.MustChangePassword = false;
        user.TemporaryPasswordExpiresAtUtc = null;
        user.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded) return StatusCode(500, new { message = "Nie udało się zapisać zmiany hasła." });
        _policy.RememberOldHash(user, previousHash);
        await _db.SaveChangesAsync();
        await _db.UserSessions.Where(s => s.UserId == user.Id && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow));
        var created = await _sessions.CreateAsync(user.Id);
        await tx.CommitAsync();
        Response.Cookies.Append(SessionCookies.Name, created.Token, SessionCookies.Options(created.ExpiresAtUtc));
        return Ok(new { message = "Hasło zmienione.", requiresTwoFactor = user.TwoFactorEnabled });
    }

    [Authorize(Policy = "SessionAny")]
    [HttpPost("complete-first-login")]
    public Task<IActionResult> CompleteFirstLogin(FirstPasswordChangeRequest request, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null) => ChangePassword(request);

    private async Task RevokeCurrentSessionAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionCookies.Name, out var token) ||
            token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) return;
        var hash = SessionTokenHasher.Hash(token);
        await _db.UserSessions.Where(s => s.TokenHash == hash && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, DateTimeOffset.UtcNow));
    }
}
