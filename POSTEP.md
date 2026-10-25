# Postęp nauki — AccessLab

Aktualizacja: 25 października 2026. Ostatni ukończony etap: lekcja 4.

## Jak mnie prowadzić

- Uczę się samodzielnie pisać aplikację. Mam podstawy i ograniczony czas.
- Prowadź mnie po polsku, małymi krokami: projekt, folder, plik, zadanie, sprawdzenie.
- Wyjaśniaj składnię C#, TypeScriptu i Reacta przy wykonywaniu konkretnych funkcji.
- Dawaj krótkie przykłady, szkielety i zadania do uzupełnienia. Gdy poproszę o cały plik, pokaż go i wyjaśnij ważniejsze fragmenty.
- Rozwijaj właściwy system kont, logowania i ról; dodatkowe ćwiczenia ograniczaj do niezbędnych.
- Przy błędach najpierw sprawdzaj komunikat i aktualny kod. Nie zaczynaj projektu od początku.
- Nie zakładaj dostępu do wcześniejszego czatu, mojego komputera ani prywatnego repozytorium.

## Projekt i narzędzia

AccessLab — projekt zaliczeniowy dotyczący identyfikacji, uwierzytelniania i autoryzacji; wariant 7 oraz dodatkowe TOTP. Docelowo panel użytkowników i proste notatki pokazujące różnice uprawnień.

System: Windows, terminal PowerShell.

Ścieżka repozytorium na moim komputerze:

```text
C:\Users\Natan\source\repos\777Nnatii777\AccessLab
```

Backend piszę w Visual Studio. Reacta piszę w VS Code.

| Narzędzie | Potwierdzona wersja / ustalenie |
|---|---|
| .NET SDK | 10.0.401; projekty net10.0 |
| C# | Domyślna wersja dla .NET 10: C# 14 |
| Node.js | 24.15.0 — pozostajemy przy tej wersji |
| npm | 11.12.1 |
| Git | 2.54.0.windows.1 |
| React / React DOM | ^19.3.1 w ostatnim przesłanym package.json |
| TypeScript | ~6.0.3 w ostatnim przesłanym package.json; NIE TypeScript 7 |
| Vite | ^8.3.4 w ostatnim przesłanym package.json |
| Frontend | Szablon react-ts; ESLint |
| PostgreSQL | Linia 18 według konfiguracji lekcji; baza działa w Docker Compose |
| EF Core, Identity EF, Npgsql EF | Linia 10.0.x; dokładne wersje sprawdzać w aktualnych projektach |
| Narzędzie dotnet-ef | 10.0.14, zainstalowane lokalnie |

Nie aktualizuj narzędzi tylko po to, żeby zmienić wersje. Aktualny package-lock.json i pliki projektów są źródłem dokładnych zależności.

## Struktura i architektura

- backend/AccessLab.Domain — modele i reguły niezależne od infrastruktury.
- backend/AccessLab.Application — przypadki użycia, DTO i interfejsy.
- backend/AccessLab.Infrastructure — EF Core, PostgreSQL, Identity i implementacje.
- backend/AccessLab.Api — kontrolery i konfiguracja uruchomienia.
- frontend — React, TypeScript, Vite.

Referencje: Application → Domain; Infrastructure → Application; Api → Application i Infrastructure.

Plany architektoniczne: REST, JSON, sesje z cookie HttpOnly oraz stanem sesji w bazie. DbContext pełni funkcję Unit of Work; osobny mały interfejs IUnitOfWork nie został jeszcze wdrożony.

## Co już działa

### Lekcje 1–3

- Repozytorium Git, .gitignore, README.md i POSTEP.md; wykonane wcześniejsze pushe.
- Utworzone cztery projekty backendu i ich referencje.
- Działa endpoint GET /api/health.
- Odpowiedź w ostatnim przesłanym kodzie: message = "witaj", numer = 3, version = "0.1".
- React ma src/api/health.ts i App.tsx z przyciskiem sprawdzającym połączenie.
- Jest obsługa oczekiwania, powodzenia i błędu. Użytkownik potwierdził działanie połączenia.

### Lekcja 4 — baza i Identity

- Działa PostgreSQL: baza accesslab, użytkownik bazy accesslab, usługa Compose postgres.
- Plik compose.yaml znajduje się w głównym katalogu repozytorium.
- ConnectionStrings:DefaultConnection i Bootstrap:AdminPassword są przechowywane w User Secrets projektu Api. Sekretów nie umieszczamy w tym pliku ani w Git.
- ApplicationUser znajduje się w Infrastructure/Identity i dziedziczy po IdentityUser.
- IsBlocked i MustChangePassword zostały poprawione z pól na właściwości z get; set;.
- ApplicationDbContext znajduje się w Infrastructure/Persistence i dziedziczy po IdentityDbContext<ApplicationUser>.
- Skonfigurowano UseNpgsql, AddIdentityCore<ApplicationUser>, role i magazyny EF Core.
- Migracja 20261025193104_InitialIdentity została zastosowana.
- Kolejna migracja dodała kolumny IsBlocked i MustChangePassword. Proponowana nazwa: AddUserSecurityProperties; dokładną nazwę potwierdzić w repozytorium.
- IdentitySeeder tworzy brakujące role, odnajduje lub tworzy ADMIN i zawiera logikę przypisania roli Administrator.
- Tworzenie konta odbywa się przez UserManager.CreateAsync(admin, adminPassword), z kontrolą IdentityResult. Hasła nie zapisujemy samodzielnie ani jawnym tekstem.
- Seeder jest wywoływany przy uruchomieniu API w środowisku Development. Udało się utworzyć konto po poprawieniu początkowego hasła.

Ostatni wynik odczytu bezpośrednio z PostgreSQL:

| UserName | IsBlocked | MustChangePassword | HasPasswordHash |
|---|---|---|---|
| ADMIN | false | true | true |

W AspNetRoles są dokładnie: Administrator, Kierownik, Redaktor, Pracownik, Czytelnik.

Utworzenie konta, hash i pięć ról są potwierdzone zapytaniami SQL. Faktyczne powiązanie ADMIN z rolą Administrator oraz ponowne uruchomienie bez duplikatów warto jeszcze sprawdzić; nie pokazano wyników tych kontroli.

## Ważne szczegóły i rozwiązane problemy

- Pole `public bool IsBlocked = false;` nie trafiło do modelu EF według domyślnych konwencji. Poprawiono je na `public bool IsBlocked { get; set; } = false;` i wykonano dodatkową migrację.
- Poprawna nazwa ustawienia długości hasła to RequiredLength, nie RequireLength.
- Podczas tworzenia ADMIN Identity zgłosiło minimum 16 znaków. Po poprawce konto zostało utworzone. Wariant wymaga minimum 14; przed dalszą pracą sprawdzić aktualne RequiredLength w Program.cs — nie potwierdzono, czy zostało zmienione z 16 na 14.
- W ostatnim przesłanym launchSettings.json API miało HTTPS na porcie 7068 i HTTP na 5285. W tamtym vite.config.ts proxy wskazywało http://localhost:5000. Użytkownik potwierdził działanie frontendu, ale finalnej konfiguracji portów nie przesłano. Przy problemie sprawdzić rzeczywiście używany adres API.
- Visual Studio pokazywało Folder View / Miscellaneous Files i nie podpowiadało kodu. Użytkownik zgłosił rozwiązanie problemu po instrukcji otwarcia projektu/solution.
- Manifest narzędzia EF został zapisany w backend/dotnet-tools.json według komunikatu instalacji. Nie zakładać, że leży w głównym .config.
- Ostatnia zapowiedź użytkownika: wykonuje commit/push po lekcji 4. Zakończenie tego pusha nie zostało potwierdzone.

## Wymagania końcowe — do zachowania

Poniżej ustalenia przeniesione z poprzedniej rozmowy. Oryginalnej treści zadania nie dołączono do tego pliku; w razie niejasności poprosić o jej załączenie.

- Minimum 14 znaków hasła.
- Opcjonalna polityka odległości Levenshteina co najmniej 4, włączana przez administratora.
- Zakaz ponownego użycia wcześniejszych haseł; historia przechowuje hashe.
- Lokalna lista haseł zakazanych.
- Sześć kont testowych łącznie, w tym stałe ADMIN; obecnie potwierdzone jest jedno konto.
- Pięć ról: Administrator, Kierownik, Redaktor, Pracownik, Czytelnik.
- Losowe hasło tymczasowe: dokładnie 16 znaków alfanumerycznych, ważne 4 godziny.
- Wymuszona zmiana hasła przy pierwszym logowaniu.
- Zwykła zmiana hasła: stare, nowe i powtórzenie nowego.
- Zarządzanie kontami: tworzenie, edycja, role, blokowanie, odblokowanie, usuwanie i reset hasła.
- Ochrona stałego ADMIN przed usunięciem i odebraniem roli administratora.
- Ustawienia administratora: minimalna długość, dodatkowa polityka, ważność hasła w dniach i limit bezczynności.
- Logowanie i wylogowanie, wygasanie i unieważnianie sesji po stronie serwera.
- Komunikat przy błędnych danych logowania: „Login lub hasło niepoprawne”.
- Uprawnienia egzekwowane w API; ukrywanie przycisków na froncie nie wystarcza.
- Dodatkowo TOTP: konfiguracja, potwierdzenie kodem, drugi krok logowania, kody odzyskiwania i ograniczenie prób.
- Proste notatki mogą posłużyć do pokazania uprawnień ról; nie zostały jeszcze wdrożone.

## Następna lekcja — 5: logowanie

Kontynuować od działającego konta w PostgreSQL. Nie powtarzać instalacji, tworzenia projektów ani migracji InitialIdentity.

1. Krótko sprawdzić bieżący Program.cs, ApplicationUser.cs i IdentitySeeder.cs oraz przypisanie roli ADMIN. Potwierdzić politykę długości hasła i porty.
2. Wyjaśnić różnicę: sprawdzenie hasła, uwierzytelnienie, sesja i autoryzacja.
3. Przygotować DTO logowania oraz kontrakt serwisu w Application i implementację opartą na Identity w Infrastructure.
4. Napisać wyszukiwanie konta, sprawdzanie hasła przez Identity i sprawdzanie IsBlocked. Stosować ogólny komunikat błędnych danych oraz ograniczenie prób.
5. Obsłużyć MustChangePassword = true. ADMIN ma tę flagę ustawioną: poprawne hasło nie może od razu dawać pełnego dostępu do aplikacji przed wymaganą zmianą.
6. Dodać kontroler logowania, a następnie formularz React korzystający z serwera.
7. Przy wprowadzaniu sesji cookie omówić i wdrożyć potrzebne zabezpieczenia, w tym CSRF. Docelowo cookie HttpOnly oraz sesja w bazie; nie przechodzić samowolnie na JWT w localStorage.

Podzielić lekcję na krótkie części, jeśli cały proces logowania jest za duży na jedno spotkanie. Najpierw doprowadzać każdą część do działającego wyniku.

## Czego jeszcze nie ma

Nie ma jeszcze potwierdzonego działającego logowania, formularza logowania, sesji użytkownika, wylogowania, egzekwowania blokady lub wymuszonej zmiany hasła, panelu administratora, historii haseł, Levenshteina ani TOTP. Samo zapisanie flag i ról nie realizuje tych procesów.

## Jak kontynuować w nowym czacie

Dołączyć ten POSTEP.md oraz aktualne pliki kodu potrzebne do następnego kroku. Warto udostępnić aktualne backend i frontend w ZIP bez bin, obj, node_modules, .vs, .env i sekretów. Stare archiwa z poprzedniego czatu mogą nie zawierać zmian z lekcji 4.

Wiadomość startowa:

> Kontynuujemy AccessLab. Załączam POSTEP.md i aktualny kod. Lekcje 1–4 są ukończone: PostgreSQL zawiera ADMIN z hashem hasła i pięć ról. Chcę zacząć lekcję 5 — logowanie. Prowadź mnie małymi krokami z wyjaśnieniami i krótkimi przykładami; chcę sam pisać kod. Najpierw sprawdź bieżące pliki i uwzględnij MustChangePassword = true. Nie zaczynaj projektu od nowa.

Po każdej kolejnej lekcji aktualizować ten plik i zapisywać go razem z kodem w Git.
