namespace AccessLab.Api.Authentication;

public static class SessionCookies
{
    public const string Name = "__Host-AccessLabSession";
    public static CookieOptions Options(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
        Path = "/", Expires = expires, IsEssential = true
    };
}
