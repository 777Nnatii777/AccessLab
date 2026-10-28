using AccessLab.Api.Authentication;
using AccessLab.Application.Authentication;
using AccessLab.Infrastructure.Authentication;
using AccessLab.Infrastructure.Identity;
using AccessLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<CsrfValidationFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<CsrfValidationFilter>());
builder.Services.AddSwaggerGen();

var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Brak ConnectionStrings:DefaultConnection. Ustaw .NET User Secrets.");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequiredLength = 16;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<PasswordPolicyService>();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "__Host-AccessLab-Csrf";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.Path = "/";
});
builder.Services.AddAuthentication("AccessLabSession")
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("AccessLabSession", _ => { });
builder.Services.AddAuthorization(o =>
{
    var any = new AuthorizationPolicyBuilder("AccessLabSession").RequireAuthenticatedUser().Build();
    var full = new AuthorizationPolicyBuilder("AccessLabSession")
        .RequireAuthenticatedUser().RequireClaim("session_level", "full").Build();
    o.AddPolicy("SessionAny", any);
    o.AddPolicy("FullSession", full);
    o.DefaultPolicy = full;
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Migruj jawnie poleceniem dotnet ef database update; nie wykonujemy migracji automatycznie.
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var password = app.Configuration["Bootstrap:AdminPassword"]
        ?? throw new InvalidOperationException("Brak Bootstrap:AdminPassword w User Secrets.");
    await IdentitySeeder.SeedAsync(users, roles, password);
}
app.Run();
