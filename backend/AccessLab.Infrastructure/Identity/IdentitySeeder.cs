using Microsoft.AspNetCore.Identity;

namespace AccessLab.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        string adminPassword)
    {
        string[] roleNames =
        [
            "Administrator",
            "Kierownik",
            "Redaktor",
            "Pracownik",
            "Czytelnik"
        ];

        // 1. Utwórz tylko te role, których jeszcze nie ma.
        foreach (var roleName in roleNames)
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                var result = await roles.CreateAsync(
                    new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join("; ",
                            result.Errors.Select(error => error.Description)));
                }
            }
        }

        
        var admin = await users.FindByNameAsync("ADMIN");

        
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = "ADMIN",
                IsBlocked = false,
                MustChangePassword = true
            };

            var result = await users.CreateAsync(admin, adminPassword);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ",
                        result.Errors.Select(error => error.Description)));
            }
        }

       
        if (!await users.IsInRoleAsync(admin, "Administrator"))
        {
            var result = await users.AddToRoleAsync(
                admin, "Administrator");

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ",
                        result.Errors.Select(error => error.Description)));
            }
        }
    }
}