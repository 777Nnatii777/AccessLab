namespace AccessLab.Application.Authentication;

public class LoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? CaptchaToken { get; set; }
    // Pułapka na boty: ukryte pole, człowiek go nie widzi i zostawia puste.
    public string? Website { get; set; }
}
