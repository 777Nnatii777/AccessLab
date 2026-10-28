using System.Security.Cryptography;
using AccessLab.Application.Authentication;
using AccessLab.Domain.Authentication;
using AccessLab.Infrastructure.Authentication;
using AccessLab.Infrastructure.Identity;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Api.Controllers;

public sealed record CreateUserInput(string Login, string Role);
public sealed record EditUserInput(string Login, string? Email, string Role);
public sealed record SettingsInput(int MinimumPasswordLength, bool RequirePasswordDistance,
    int PasswordValidityDays, int SessionIdleMinutes);

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "FullSession", Roles = "Administrator")]
public sealed class AdminController : ControllerBase
{
    private static readonly string[] ValidRoles = ["Administrator", "Kierownik", "Redaktor", "Pracownik", "Czytelnik"];
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly PasswordPolicyService _policy;
    private readonly ISessionService _sessions;

    public AdminController(UserManager<ApplicationUser> users, ApplicationDbContext db,
        PasswordPolicyService policy, ISessionService sessions)
    {
        _users = users; _db = db; _policy = policy;
        _sessions = sessions;
    }

    [HttpGet("users")]
    public async Task<IActionResult> AllUsers()
    {
        var list = await _users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var output = new List<object>();
        foreach (var u in list)
            output.Add(new { u.Id, u.UserName, u.Email, u.IsBlocked, u.MustChangePassword,
                u.TwoFactorEnabled, u.TemporaryPasswordExpiresAtUtc,
                Roles = await _users.GetRolesAsync(u) });
        return Ok(output);
    }

    [HttpPost("users")]
    public async Task<IActionResult> Create(CreateUserInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!ValidRoles.Contains(input.Role) || string.IsNullOrWhiteSpace(input.Login) ||
            input.Login.Length > 80 || !input.Login.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
            return BadRequest(new { message = "Nieprawidłowy login lub rola." });
        if (await _users.FindByNameAsync(input.Login) is not null)
            return Conflict(new { message = "Login jest już zajęty." });
        var password = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = input.Login.Trim(), MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4)
        };
        await using var tx = await _db.Database.BeginTransactionAsync();
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        var role = await _users.AddToRoleAsync(user, input.Role);
        if (!role.Succeeded) return BadRequest(new { errors = role.Errors.Select(e => e.Description) });
        await tx.CommitAsync();
        return Ok(new { user.Id, user.UserName, temporaryPassword = password,
            expiresAtUtc = user.TemporaryPasswordExpiresAtUtc, message = "Hasło tymczasowe pokaż tylko użytkownikowi (jednorazowo)." });
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> Edit(string id, EditUserInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!ValidRoles.Contains(input.Role) || string.IsNullOrWhiteSpace(input.Login) ||
            input.Login.Length > 80 || !input.Login.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
            return BadRequest(new { message = "Nieprawidłowy login lub rola." });
        if (IsPermanentAdmin(user) && (!string.Equals(input.Login, "ADMIN", StringComparison.OrdinalIgnoreCase)
            || input.Role != "Administrator"))
            return BadRequest(new { message = "Nie wolno zmienić loginu lub roli stałego ADMIN." });
        var current = (await _users.GetRolesAsync(user)).ToArray();
        await using var tx = await _db.Database.BeginTransactionAsync();
        user.UserName = input.Login.Trim(); user.Email = input.Email;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return BadRequest(new { errors = updated.Errors.Select(e => e.Description) });
        var removed = await _users.RemoveFromRolesAsync(user, current);
        if (!removed.Succeeded) return BadRequest(new { errors = removed.Errors.Select(e => e.Description) });
        var added = await _users.AddToRoleAsync(user, input.Role);
        if (!added.Succeeded) return BadRequest(new { errors = added.Errors.Select(e => e.Description) });
        await _sessions.RevokeAllAsync(user.Id);
        await tx.CommitAsync();
        return Ok(new { message = "Zapisano zmiany. Stare sesje unieważniono." });
    }

    [HttpPost("users/{id}/block")]
    public async Task<IActionResult> Block(string id, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        if (IsPermanentAdmin(u)) return BadRequest(new { message = "Nie wolno blokować ADMIN." });
        u.IsBlocked = true;
        var result = await _users.UpdateAsync(u);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        await _sessions.RevokeAllAsync(u.Id);
        return NoContent();
    }

    [HttpPost("users/{id}/unblock")]
    public async Task<IActionResult> Unblock(string id, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        u.IsBlocked = false;
        var result = await _users.UpdateAsync(u);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        await _users.SetLockoutEndDateAsync(u, null);
        await _users.ResetAccessFailedCountAsync(u);
        return NoContent();
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        var password = GenerateTemporaryPassword();
        var oldHash = u.PasswordHash;
        await using var tx = await _db.Database.BeginTransactionAsync();
        var token = await _users.GeneratePasswordResetTokenAsync(u);
        var changed = await _users.ResetPasswordAsync(u, token, password);
        if (!changed.Succeeded) return BadRequest(new { errors = changed.Errors.Select(e => e.Description) });
        _policy.RememberOldHash(u, oldHash);
        u.MustChangePassword = true;
        u.TemporaryPasswordExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4);
        u.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
        var updated = await _users.UpdateAsync(u);
        if (!updated.Succeeded) return StatusCode(500);
        await _db.SaveChangesAsync();
        await _sessions.RevokeAllAsync(u.Id);
        await tx.CommitAsync();
        return Ok(new { temporaryPassword = password, expiresAtUtc = u.TemporaryPasswordExpiresAtUtc });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> Delete(string id, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        if (IsPermanentAdmin(u)) return BadRequest(new { message = "Nie wolno usunąć ADMIN." });
        var result = await _users.DeleteAsync(u);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(e => e.Description) });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() => Ok(await _db.SecuritySettings.AsNoTracking()
        .FirstOrDefaultAsync(s => s.Id == 1) ?? new SecuritySettings());

    [HttpPut("settings")]
    public async Task<IActionResult> ChangeSettings(SettingsInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (input.MinimumPasswordLength is < 16 or > 128 ||
            input.PasswordValidityDays is < 0 or > 3650 ||
            input.SessionIdleMinutes is < 5 or > 240)
            return BadRequest(new { message = "Długość: 16–128; ważność: 0–3650 dni; bezczynność: 5–240 minut." });
        var settings = await _db.SecuritySettings.FindAsync(1);
        if (settings is null) { settings = new SecuritySettings(); _db.SecuritySettings.Add(settings); }
        settings.MinimumPasswordLength = input.MinimumPasswordLength;
        settings.RequirePasswordDistance = input.RequirePasswordDistance;
        settings.PasswordValidityDays = input.PasswordValidityDays;
        settings.SessionIdleMinutes = input.SessionIdleMinutes;
        await _db.SaveChangesAsync();
        return Ok(settings);
    }

    private static bool IsPermanentAdmin(ApplicationUser u) =>
        string.Equals(u.UserName, "ADMIN", StringComparison.OrdinalIgnoreCase);

    private static string GenerateTemporaryPassword() =>
        new(Enumerable.Range(0, 16).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
}
