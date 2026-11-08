 Raport testu bota – 2026-11-08 15:38

| Scenariusz | Prób | Zdarzenia zapisane przez serwer | Oczekiwane zabezpieczenie | Wynik |
|---|---|---|---|---|
| Konto-pułapka (dane z komentarza w HTML) | 1 | CanaryAccount ×1 | CanaryAccount | ZADZIAŁAŁO |
| Naiwny bot wypełnia ukryte pole | 1 | Honeypot ×1 | Honeypot | ZADZIAŁAŁO |
| Bot bez CAPTCHA | 1 | CaptchaFailed ×1 | CaptchaFailed | ZADZIAŁAŁO |
| Brute force na konto ADMIN | 5 | BadPassword ×4, Locked ×1 | Locked | ZADZIAŁAŁO |
| Password spraying (wiele loginów z jednego IP) | 13 | BadPassword ×12, IpLimited ×1 | IpLimited | ZADZIAŁAŁO |

Uwaga: po teście adres IP i konto ADMIN mogą być zablokowane na 15 minut.
Odblokowanie: `dotnet run --project tests/AccessLab.BotTest -- --reset`
#