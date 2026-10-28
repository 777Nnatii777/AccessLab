using AccessLab.Domain.Authentication;
using AccessLab.Domain.Notes;
using AccessLab.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AccessLab.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<SecuritySettings> SecuritySettings => Set<SecuritySettings>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<UserSession>(e =>
        {
            e.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(s => s.TokenHash).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Note>(e =>
        {
            e.Property(n => n.Title).HasMaxLength(150).IsRequired();
            e.Property(n => n.Content).HasMaxLength(10000).IsRequired();
            e.HasIndex(n => n.AuthorId);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PasswordHistory>(e =>
        {
            e.Property(p => p.PasswordHash).IsRequired();
            e.HasIndex(p => p.UserId);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SecuritySettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
        });
    }
}
