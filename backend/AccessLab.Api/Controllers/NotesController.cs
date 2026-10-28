using System.Security.Claims;
using AccessLab.Domain.Notes;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Api.Controllers;

public sealed record NoteInput(string Title, string Content);

[ApiController]
[Route("api/notes")]
[Authorize(Policy = "FullSession")]
public sealed class NotesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public NotesController(ApplicationDbContext db) => _db = db;
    private bool AdminOrManager => User.IsInRole("Administrator") || User.IsInRole("Kierownik");
    private bool Editor => User.IsInRole("Redaktor");
    private bool Worker => User.IsInRole("Pracownik");
    private bool CanWrite => AdminOrManager || Editor || Worker;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> All()
    {
        var notes = await _db.Notes.AsNoTracking().OrderByDescending(n => n.UpdatedAtUtc)
            .Select(n => new { n.Id, n.Title, n.Content, n.AuthorId, n.CreatedAtUtc, n.UpdatedAtUtc })
            .ToListAsync();
        return Ok(notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> One(Guid id)
    {
        var note = await _db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
        return note is null ? NotFound() : Ok(note);
    }

    [HttpPost]
    public async Task<IActionResult> Create(NoteInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!CanWrite) return Forbid();
        var error = Validate(input);
        if (error is not null) return BadRequest(new { message = error });
        var now = DateTimeOffset.UtcNow;
        var note = new Note { Title = input.Title.Trim(), Content = input.Content.Trim(),
            AuthorId = UserId, CreatedAtUtc = now, UpdatedAtUtc = now };
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(One), new { id = note.Id }, note);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, NoteInput input, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        var note = await _db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        if (!(AdminOrManager || Editor || (Worker && note.AuthorId == UserId))) return Forbid();
        var error = Validate(input);
        if (error is not null) return BadRequest(new { message = error });
        note.Title = input.Title.Trim(); note.Content = input.Content.Trim();
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(note);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken = null)
    {
        if (!AdminOrManager) return Forbid();
        var note = await _db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(NoteInput input) =>
        string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 150 ||
        string.IsNullOrWhiteSpace(input.Content) || input.Content.Length > 10000
            ? "Tytuł: 1–150 znaków, treść: 1–10000 znaków." : null;
}
