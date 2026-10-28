using System.Security.Claims;
using AccessLab.Infrastructure.Identity;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Api.Controllers;

public sealed record TotpCodeInput(string Code);

[ApiController]
[Route("api/auth/totp")]
[Authorize(Policy = "SessionAny")]
public sealed class TotpController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    public TotpController(UserManager<ApplicationUser> users, ApplicationDbContext db)
    {
        _users = users; _db = db;
    }
    private async Task<ApplicationUser?> CurrentUser() =>
        await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!User.HasClaim("session_level", "full")) return Forbid();
        var user = await CurrentUser();
        if (user is null) return Unauthorized();
        if (user.TwoFactorEnabled) return BadRequest(new { message = "TOTP jest już włączone." });
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            var result = await _users.ResetAuthenticatorKeyAsync(user);
            if (!result.Succeeded) return StatusCode(500);
            key = await _users.GetAuthenticatorKeyAsync(user);
        }
        var issuer = Uri.EscapeDataString("AccessLab");
        var account = Uri.EscapeDataString(user.UserName ?? "user");
        var uri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6";
        return Ok(new { secret = key, otpauthUri = uri, message = "Wprowadź sekret w aplikacji uwierzytelniającej, a potem potwierdź kodem." });
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(TotpCodeInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!User.HasClaim("session_level", "full")) return Forbid();
        var user = await CurrentUser();
        if (user is null) return Unauthorized();
        if (user.TwoFactorEnabled) return BadRequest(new { message = "TOTP już włączone." });
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new { message = "Najpierw skonfiguruj TOTP." });
        var sessionId = Guid.Parse(User.FindFirstValue("session_id")!);
        var currentSession = await _db.UserSessions.FindAsync(sessionId);
        if (currentSession is null || currentSession.RevokedAtUtc is not null) return Unauthorized();
        if (!await _users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, (input.Code ?? "").Replace(" ", "")))
        {
            currentSession.TwoFactorFailures++;
            if (currentSession.TwoFactorFailures >= 5) currentSession.RevokedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return BadRequest(new { message = "Nieprawidłowy kod. Po 5 próbach sesja jest unieważniana." });
        }
        currentSession.TwoFactorFailures = 0;
        await _db.SaveChangesAsync();
        var enabled = await _users.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded) return StatusCode(500);
        var codes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return Ok(new { message = "TOTP aktywny. Zapisz kody odzyskiwania; są wyświetlane tylko teraz.", recoveryCodes = codes });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(TotpCodeInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!User.HasClaim("session_level", "totp")) return Forbid();
        var user = await CurrentUser();
        if (user is null || !user.TwoFactorEnabled) return Unauthorized();
        var sessionId = Guid.Parse(User.FindFirstValue("session_id")!);
        var session = await _db.UserSessions.FindAsync(sessionId);
        if (session is null || !session.IsTwoFactorPending || session.RevokedAtUtc is not null) return Unauthorized();
        var code = (input.Code ?? "").Replace(" ", "").Replace("-", "");
        // The built-in recovery-code verifier also invalidates a used code.
        var valid = await _users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
        if (!valid && code.Length != 6)
            valid = (await _users.RedeemTwoFactorRecoveryCodeAsync(user, input.Code ?? "")).Succeeded;
        if (!valid)
        {
            session.TwoFactorFailures++;
            if (session.TwoFactorFailures >= 5) session.RevokedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Błędny kod. Po pięciu próbach sesja zostaje unieważniona." });
        }
        session.TwoFactorFailures = 0;
        session.IsTwoFactorPending = false;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Drugi składnik potwierdzony." });
    }

    [HttpPost("recovery-codes")]
    public async Task<IActionResult> NewRecoveryCodes(TotpCodeInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!User.HasClaim("session_level", "full")) return Forbid();
        var user = await CurrentUser();
        if (user is null || !user.TwoFactorEnabled) return BadRequest();
        if (!await _users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, input.Code.Replace(" ", "")))
            return Unauthorized(new { message = "Niepoprawny kod TOTP." });
        var codes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return Ok(new { recoveryCodes = codes });
    }
}
