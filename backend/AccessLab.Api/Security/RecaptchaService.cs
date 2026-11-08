namespace AccessLab.Api.Security;

// Sprawdza token Google reCAPTCHA v2. Google sam pilnuje, że token jest
// jednorazowy i ważny 2 minuty (ponowne użycie zwraca błąd "timeout-or-duplicate").
public sealed class RecaptchaService
{
    private readonly HttpClient _http;
    private readonly string? _secret;

    public RecaptchaService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _secret = config["Recaptcha:SecretKey"];
    }

    public async Task<bool> VerifyAsync(string? token, string ip)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrEmpty(_secret)) return false;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = _secret,
            ["response"] = token,
            ["remoteip"] = ip
        });
        try
        {
            var response = await _http.PostAsync("https://www.google.com/recaptcha/api/siteverify", form);
            var result = await response.Content.ReadFromJsonAsync<GoogleAnswer>();
            return result?.Success == true;
        }
        catch (HttpRequestException) { return false; }
    }

    private sealed record GoogleAnswer(bool Success);
}
