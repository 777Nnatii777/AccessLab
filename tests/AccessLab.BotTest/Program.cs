// Symulacja bota atakującego logowanie AccessLab (Selenium + Chrome).
// Wymaga: działający backend (https://localhost:7068, Development) i frontend (npm run dev, http://localhost:5173).
// Uruchomienie:        dotnet run --project tests/AccessLab.BotTest
// Odblokowanie po teście: dotnet run --project tests/AccessLab.BotTest -- --reset
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

const string AppUrl = "http://localhost:5173";
const string ApiUrl = "https://localhost:7068";

// Certyfikat deweloperski ASP.NET – w teście akceptujemy go bez sprawdzania.
using var http = new HttpClient(new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
}) { BaseAddress = new Uri(ApiUrl) };

try { await Reset(); }
catch (HttpRequestException e)
{
    Console.WriteLine($"Brak połączenia z API ({ApiUrl}): {e.Message}. Uruchom backend w trybie Development.");
    return;
}
if (args.Contains("--reset"))
{
    Console.WriteLine("Wyczyszczono zdarzenia i zdjęto blokady kont/IP.");
    return;
}
try { await http.GetAsync(AppUrl); }
catch (HttpRequestException)
{
    Console.WriteLine($"Frontend nie odpowiada ({AppUrl}). Uruchom w osobnym terminalu: cd frontend; npm run dev");
    return;
}

var options = new ChromeOptions();
options.AddArgument("--window-size=1200,900");
using var driver = new ChromeDriver(options); // Selenium Manager sam pobiera chromedriver
var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
var js = (IJavaScriptExecutor)driver;
var rows = new List<(string Name, int Attempts, string Expected, Dictionary<string, int> Events)>();

// 1. Bot czyta źródło strony, znajduje "zapomniane" dane testowe i próbuje ich użyć.
await Scenario("Konto-pułapka (dane z komentarza w HTML)", "CanaryAccount", () =>
{
    driver.Navigate().GoToUrl(AppUrl);
    var bait = Regex.Match(driver.PageSource, @"konto testowe (\S+) / (\S+)");
    TryLogin(bait.Groups[1].Value, bait.Groups[2].Value, fillHoneypot: false, clickCaptcha: true);
    return 1;
});

// 2. Naiwny bot wypełnia wszystkie pola formularza, także ukryte.
await Scenario("Naiwny bot wypełnia ukryte pole", "Honeypot", () =>
{
    TryLogin("ADMIN", "haslo-bota-123456", fillHoneypot: true, clickCaptcha: true);
    return 1;
});

// 3. Bot w ogóle nie rusza CAPTCHA.
await Scenario("Bot bez CAPTCHA", "CaptchaFailed", () =>
{
    TryLogin("ADMIN", "haslo-bota-123456", fillHoneypot: false, clickCaptcha: false);
    return 1;
});

// 4. Brute force: wiele haseł na jedno konto.
await Scenario("Brute force na konto ADMIN", "Locked", () =>
{
    for (int i = 1; i <= 10; i++)
    {
        var message = TryLogin("ADMIN", $"zgadywane-haslo-{i:D4}", fillHoneypot: false, clickCaptcha: true);
        if (message.Contains("zablokowane", StringComparison.OrdinalIgnoreCase)) return i;
    }
    return 10;
});

// 5. Password spraying: jedno hasło, wiele loginów, ten sam adres IP.
await Scenario("Password spraying (wiele loginów z jednego IP)", "IpLimited", () =>
{
    for (int i = 1; i <= 30; i++)
    {
        var message = TryLogin($"user{i:D2}", "Wiosna2026!Wiosna2026!", fillHoneypot: false, clickCaptcha: true);
        if (message.Contains("adresu IP", StringComparison.OrdinalIgnoreCase)) return i;
    }
    return 30;
});

driver.Quit();
PrintReport();
return;

string TryLogin(string login, string password, bool fillHoneypot, bool clickCaptcha)
{
    driver.Manage().Cookies.DeleteAllCookies();
    driver.Navigate().GoToUrl(AppUrl);
    var loginInput = wait.Until(d => d.FindElement(By.CssSelector("input[autocomplete=username]")));
    loginInput.SendKeys(Keys.Control + "a");
    loginInput.SendKeys(Keys.Delete);
    loginInput.SendKeys(login);
    driver.FindElement(By.CssSelector("input[type=password]")).SendKeys(password);
    if (fillHoneypot)
        js.ExecuteScript("document.querySelector('input[name=website]').value = 'http://spam.example'");
    if (clickCaptcha) ClickCaptcha();
    var button = driver.FindElement(By.XPath("//button[text()='Zaloguj']"));
    js.ExecuteScript("arguments[0].click()", button); // klik przez JS, bo okno z obrazkami CAPTCHA może zasłaniać przycisk
    var status = wait.Until(d => d.FindElements(By.CssSelector("[role=status]")).FirstOrDefault(e => e.Text.Length > 0));
    Console.WriteLine($"   {login,-15} -> {status.Text}");
    return status.Text;
}

// Bot klika "Nie jestem robotem". Z kluczami testowymi Google przechodzi od razu,
// z prawdziwymi kluczami Google zwykle pokazuje obrazki – wtedy bot się poddaje.
void ClickCaptcha()
{
    try
    {
        var frame = wait.Until(d => d.FindElement(By.CssSelector("iframe[src*='recaptcha/api2/anchor']")));
        driver.SwitchTo().Frame(frame);
        wait.Until(d => d.FindElement(By.Id("recaptcha-anchor"))).Click();
        driver.SwitchTo().DefaultContent();
        new WebDriverWait(driver, TimeSpan.FromSeconds(5)).Until(_ =>
            js.ExecuteScript("return document.querySelector('textarea[name=g-recaptcha-response]')?.value || ''") is string { Length: > 0 });
    }
    catch (WebDriverException)
    {
        driver.SwitchTo().DefaultContent();
    }
}

async Task Scenario(string name, string expected, Func<int> attack)
{
    Console.WriteLine($"== {name}");
    var since = DateTimeOffset.UtcNow;
    int attempts = attack();
    var events = await http.GetFromJsonAsync<List<EventRow>>(
        "/api/dev/security-events?since=" + Uri.EscapeDataString(since.ToString("O"))) ?? [];
    rows.Add((name, attempts, expected, events.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count())));
}

async Task Reset() => (await http.PostAsync("/api/dev/reset", null)).EnsureSuccessStatusCode();

void PrintReport()
{
    var md = new StringBuilder();
    md.AppendLine($"# Raport testu bota – {DateTime.Now:yyyy-MM-dd HH:mm}");
    md.AppendLine();
    md.AppendLine("| Scenariusz | Prób | Zdarzenia zapisane przez serwer | Oczekiwane zabezpieczenie | Wynik |");
    md.AppendLine("|---|---|---|---|---|");
    foreach (var r in rows)
    {
        var events = string.Join(", ", r.Events.Select(e => $"{e.Key} ×{e.Value}"));
        var result = r.Events.ContainsKey(r.Expected) ? "ZADZIAŁAŁO"
            : r.Events.ContainsKey("Success") ? "BOT SIĘ ZALOGOWAŁ"
            : "bot zatrzymany wcześniej przez inne zabezpieczenie";
        md.AppendLine($"| {r.Name} | {r.Attempts} | {events} | {r.Expected} | {result} |");
    }
    md.AppendLine();
    md.AppendLine("Uwaga: po teście adres IP i konto ADMIN mogą być zablokowane na 15 minut.");
    md.AppendLine("Odblokowanie: `dotnet run --project tests/AccessLab.BotTest -- --reset`");
    File.WriteAllText("bot-report.md", md.ToString());
    Console.WriteLine();
    Console.WriteLine(md);
    Console.WriteLine($"Raport zapisano: {Path.GetFullPath("bot-report.md")}");
}

record EventRow(string Type);
