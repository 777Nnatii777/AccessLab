# AccessLab — stan realizacji projektu

**Moduł:** identyfikacja, uwierzytelnianie i autoryzacja  
**Wariant:** 7, z planowanym rozszerzeniem o TOTP  
**Aktualizacja:** 26 października 2026 r.  
**Bieżący etap:** lekcja 5 — po przygotowaniu serwisu sesji i rozszerzeniu wyniku sprawdzania danych logowania; przed połączeniem logowania z cookie i obsługą sesji przy kolejnych żądaniach.

## 1. Cel i zakres projektu

AccessLab to aplikacja webowa służąca do zarządzania kontami użytkowników, bezpiecznego logowania oraz egzekwowania uprawnień na podstawie ról. Stanowi projekt zaliczeniowy modułu 1 i podstawę dalszego rozwoju w modułach 2 i 3.

Docelowa aplikacja obejmuje panel administratora, panel użytkownika oraz proste notatki demonstrujące różnice między uprawnieniami poszczególnych ról. Rozszerzeniem na ocenę bardzo dobrą jest uwierzytelnianie dwuskładnikowe TOTP.

System ma rozdzielać trzy procesy:

- **Identyfikacja:** wskazanie konta przez podanie loginu.
- **Uwierzytelnianie:** potwierdzenie tożsamości hasłem, a po wdrożeniu TOTP również drugim składnikiem.
- **Autoryzacja:** sprawdzenie po stronie API, czy rozpoznany użytkownik może wykonać daną operację.

Wymagany przebieg pierwszego logowania: poprawne dane konta → ograniczona sesja → obowiązkowa zmiana hasła → uzyskanie zwykłego dostępu zgodnie z rolą. Sama odpowiedź `PasswordChangeRequired` nie realizuje jeszcze całego tego procesu.

## 2. Wymagania funkcjonalne i bezpieczeństwa

### 2.1. Konta i role

- Stałe konto `ADMIN`, przypisane do roli `Administrator`.
- Sześć kont testowych łącznie z kontem `ADMIN`.
- Pięć ról wariantu 7: `Administrator`, `Kierownik`, `Redaktor`, `Pracownik`, `Czytelnik`.
- Administracyjne tworzenie i edycja kont, przypisywanie ról, blokowanie, odblokowywanie, usuwanie oraz resetowanie haseł.
- Ochrona konta `ADMIN` przed usunięciem i odebraniem roli Administrator.
- Reguły dostępu egzekwowane w API; widoczność przycisków w React ma wyłącznie odzwierciedlać te reguły.

### 2.2. Hasła

| Obszar | Wymaganie lub przyjęte ustawienie |
|---|---|
| Minimalna długość zwykłego hasła | W projekcie przyjęto **16 znaków**, ustawione przez `RequiredLength = 16` |
| Minimum w tabeli wariantu 7 | 14 znaków; przyjęta polityka projektu jest surowsza |
| Hasło tymczasowe | Dokładnie **16 losowych znaków alfanumerycznych**, ważne **4 godziny** |
| Pierwsze logowanie | Obowiązkowa zmiana hasła nadanego przez administratora; nowe hasło wpisywane dwukrotnie |
| Zwykła zmiana hasła | Wymagane stare hasło, nowe hasło i potwierdzenie nowego |
| Historia | Zakaz ponownego użycia wcześniejszych haseł; przechowywanie ich hashy |
| Lista zakazana | Sprawdzanie nowego hasła z lokalną listą haseł zakazanych |
| Dodatkowa polityka wariantu | Opcjonalna odległość Levenshteina co najmniej 4 między starym i nowym hasłem, gdy stare hasło jest znane przy zmianie |
| Ustawienia administratora | Minimalna długość, włączenie dodatkowej polityki i ważność hasła w dniach |

Hasła są obsługiwane przez ASP.NET Core Identity i przechowywane jako hashe z solą. Nie przewiduje się samodzielnego zapisywania jawnych haseł ani używania prostego SHA-256 do hashowania haseł użytkowników. Wykorzystanie SHA-256 w kodzie sesji dotyczy odrębnie losowych tokenów sesyjnych.

### 2.3. Logowanie i sesje

- Formularz z loginem i maskowanym hasłem.
- Ogólny komunikat odrzucenia próby: **„Login lub hasło niepoprawne”**.
- Kontrola blokady administracyjnej oraz ograniczenie liczby błędnych prób.
- Przyjęta konfiguracja ograniczenia prób: 5 błędnych haseł i blokada czasowa na 5 minut.
- Stan sesji przechowywany w PostgreSQL; token przekazywany w cookie HttpOnly.
- Ochrona CSRF dla operacji wykorzystujących cookie uwierzytelniające.
- Wylogowanie unieważniające sesję po stronie serwera.
- Automatyczne wygasanie po bezczynności oraz kontrola końcowego terminu ważności sesji.
- Konfigurowalny przez administratora limit bezczynności.
- Sesja ograniczona do zmiany hasła nie może umożliwiać korzystania z panelu ani notatek.

### 2.4. TOTP — rozszerzenie

Planowany zakres: konfiguracja aplikacji uwierzytelniającej, potwierdzenie konfiguracji kodem, drugi krok logowania, kody odzyskiwania i ograniczenie prób weryfikacji kodów. Pełny dostęp powinien być nadawany dopiero po zakończeniu wymaganych etapów uwierzytelniania.

## 3. Technologia i architektura

### 3.1. Stos technologiczny

| Warstwa lub narzędzie | Ustalenie |
|---|---|
| Backend | ASP.NET Core Web API, projekty `net10.0` |
| Język backendu | C#, domyślna wersja dla .NET 10 |
| Dostęp do danych | EF Core 10.0.x i Npgsql EF Core 10.0.x |
| Konta i hasła | ASP.NET Core Identity |
| Baza | PostgreSQL, linia 18, uruchamiany przez Docker Compose |
| Frontend | React, TypeScript, Vite; szablon `react-ts` i ESLint |
| Dokumentacja i ręczne testowanie API | Swagger UI przez `Swashbuckle.AspNetCore`, w środowisku Development |
| Środowisko pracy | Windows, PowerShell, Visual Studio dla backendu i VS Code dla frontendu |

Ostatnio zapisane wersje środowiska: .NET SDK `10.0.401`, Node.js `24.15.0`, npm `11.12.1`, Git `2.54.0.windows.1`, lokalne narzędzie `dotnet-ef` `10.0.14`. Ostatni udostępniony `package.json` zawierał React i React DOM `^19.3.1`, TypeScript `~6.0.3` oraz Vite `^8.3.4`. Dokładne wersje zależności wynikają z aktualnych plików projektów i pliku blokady pakietów; nie przewidziano aktualizacji narzędzi jako odrębnego etapu projektu.

### 3.2. Podział odpowiedzialności

| Projekt / katalog | Odpowiedzialność |
|---|---|
| `backend/AccessLab.Domain` | Modele domenowe i reguły niezależne od infrastruktury |
| `backend/AccessLab.Application` | Kontrakty serwisów, przypadki użycia i obiekty danych |
| `backend/AccessLab.Infrastructure` | Identity, EF Core, PostgreSQL i implementacje serwisów |
| `backend/AccessLab.Api` | Kontrolery HTTP, konfiguracja aplikacji, docelowo cookie i obsługa uwierzytelniania |
| `frontend` | Interfejs React i komunikacja z API |

Referencje projektów: Application → Domain; Infrastructure → Application; Api → Application i Infrastructure. Komunikacja frontend–backend korzysta z REST i JSON. `ApplicationDbContext` pełni obecnie funkcję jednostki pracy; osobny interfejs `IUnitOfWork` nie został wdrożony.

## 4. Zrealizowane etapy

| Etap | Zakres | Stan |
|---|---|---|
| Lekcje 1–3 | Repozytorium, podział backendu, endpoint health, podstawowy frontend i komunikacja z API | Ukończone; działanie połączenia potwierdzone |
| Lekcja 4 | PostgreSQL, Identity, migracje użytkowników, role i utworzenie ADMIN | Ukończona; konto i przypisanie roli potwierdzone w bazie |
| Lekcja 5, część A | Kontrakty i serwis sprawdzania danych logowania | Zaimplementowane; poprawne hasło ADMIN zwróciło `PasswordChangeRequired` |
| Lekcja 5, część B | Testowy kontroler i Swagger UI | Zaimplementowane; wykonanie prób przez Swagger zgłoszone jako zakończone |
| Lekcja 5, część C | Model sesji, konfiguracja EF i migracja `AddUserSessions` | Wykonanie zgłoszone jako zakończone; brak załączonego aktualnego kodu migracji |
| Lekcja 5, część D | `CreatedSession`, `ISessionService`, `SessionService` i rejestracja serwisu | Wykonanie zgłoszone jako zakończone; zapis sesji nie został jeszcze pokazany w działającym procesie logowania |
| Lekcja 5, część E | `CheckedLogin` i przekazywanie ID zweryfikowanego konta | Wykonanie zgłoszone jako zakończone; ostatnia zmiana przed aktualizacją dokumentu |

**Lekcja 5 jako całość pozostaje w toku.** Nie zakończono jeszcze uwierzytelniania kolejnych żądań przez cookie, wymuszonej zmiany hasła ani wylogowania.

Stan opisuje kod udostępniony wcześniej oraz późniejsze wykonane zmiany. Najnowsza implementacja sesji i `CheckedLogin` nie była ponownie udostępniona w archiwum źródeł; jej zgodność z opisem wymaga przeglądu aktualnego repozytorium. Potwierdzenia wynikami testów zestawiono w sekcji 6.

## 5. Bieżący stan implementacji

### 5.1. Baza i Identity

- `ApplicationUser` dziedziczy po `IdentityUser`; zawiera właściwości `IsBlocked` oraz `MustChangePassword`.
- `ApplicationDbContext` dziedziczy po `IdentityDbContext<ApplicationUser>` i korzysta z Npgsql.
- `IdentitySeeder` tworzy brakujące role, odnajduje lub tworzy ADMIN i przypisuje rolę Administrator. Jest uruchamiany w Development.
- Konto jest tworzone przez `UserManager.CreateAsync`, z kontrolą wyniku operacji Identity.
- Znane migracje: `20261025193104_InitialIdentity`, `20261025201427_AddUserSecurityProperties` oraz późniejsza `AddUserSessions`, której pełny identyfikator wymaga odczytu z aktualnego repozytorium.

### 5.2. Sprawdzanie danych logowania

W `AccessLab.Application/Authentication` przygotowano:

| Typ | Rola w implementacji |
|---|---|
| `LoginRequest` | Dane wejściowe: `Login` i `Password` |
| `LoginCheckResult` | Enum: `Failed`, `PasswordChangeRequired`, `ReadyToSignIn` |
| `CheckedLogin` | Wynik zawierający `Status` i opcjonalny `UserId` |
| `ILoginService` | Kontrakt metody `Task<CheckedLogin> CheckCredentialsAsync(LoginRequest request)` |

`LoginService` w Infrastructure korzysta z `UserManager<ApplicationUser>`. Przygotowany przebieg obejmuje sprawdzenie pustych danych, wyszukanie konta, blokadę administracyjną, blokadę czasową, sprawdzenie hasła przez Identity, zapis pomyłki lub wyzerowanie licznika oraz wybór wyniku na podstawie `MustChangePassword`.

Udane sprawdzenie przekazuje `UserId` znalezionego konta. Przy odrzuceniu próby identyfikator nie jest przekazywany. `ILoginService` jest rejestrowany jako scoped.

### 5.3. Sesje

Model `Domain/Authentication/UserSession.cs` zawiera:

| Pole | Znaczenie |
|---|---|
| `Id` | Identyfikator rekordu sesji |
| `UserId` | Powiązanie z kontem |
| `TokenHash` | Hash tokenu sesyjnego |
| `CreatedAtUtc` | Czas utworzenia |
| `LastActivityAtUtc` | Ostatnia aktywność |
| `ExpiresAtUtc` | Końcowy termin ważności |
| `RevokedAtUtc` | Czas unieważnienia; `null` przed unieważnieniem |
| `IsPasswordChangeOnly` | Ograniczenie sesji do procesu zmiany hasła |

Konfiguracja EF obejmuje `DbSet<UserSession>`, unikalny indeks hasha tokenu, limit 64 znaków dla `TokenHash` i powiązanie z kontem z kaskadowym usuwaniem jego sesji.

Przygotowano `CreatedSession`, `ISessionService.CreateAsync(string userId)` oraz `SessionService` w Infrastructure. Serwis sprawdza istnienie konta i jego blokady, generuje 32 losowe bajty przez `RandomNumberGenerator`, koduje token szesnastkowo, zapisuje jego hash SHA-256 oraz rekord sesji i zwraca token z datą wygaśnięcia do warstwy API.

Początkowe terminy ważności w serwisie to 10 minut dla sesji ograniczonej i 8 godzin dla zwykłej. Limit bezczynności i powiązanie z ustawieniami administratora nie są jeszcze zaimplementowane. Te terminy sesji są niezależne od czterogodzinnej ważności hasła tymczasowego.

**Serwis tworzenia sesji nie został jeszcze połączony z endpointem logowania.** Cookie, odczyt sesji przy kolejnych żądaniach, rozpoznawanie użytkownika i egzekwowanie ograniczeń sesji pozostają do wdrożenia.

### 5.4. API i frontend

| Element | Stan |
|---|---|
| `GET /api/health` | Działa; w ostatnim udostępnionym kodzie zwraca `message = "witaj"`, `numer = 3`, `version = "0.1"` |
| `POST /api/dev/auth/check-credentials` | Testowy endpoint sprawdzający dane; dostępny w Development; nie tworzy sesji |
| `AuthController` | Mapuje odrzucenie próby na HTTP 401 i ogólny komunikat; dla poprawnych danych zwraca status |
| Swagger UI | Dodany w Development; lokalnie pod `https://localhost:7068/swagger` |
| React | Ekran początkowy i przycisk sprawdzający połączenie; obsługa oczekiwania, odpowiedzi i błędu |
| Formularze i panele aplikacji | Formularz logowania, zmiana hasła, panel administratora i notatki pozostają do wykonania |

Po zmianie wyniku na `CheckedLogin` kontroler odczytuje `result.Status`; identyfikator użytkownika jest przeznaczony do dalszej obsługi na serwerze.

## 6. Potwierdzone wyniki i zakres weryfikacji

### 6.1. Wyniki potwierdzone

- Utworzenie konta ADMIN z niepustym hashem hasła.
- Utworzenie pięciu ról wariantu 7.
- Powiązanie ADMIN z rolą Administrator, potwierdzone zapytaniem łączącym tabele użytkowników i ról.
- Wywołanie testowego endpointu z PowerShella przy poprawnym haśle i odpowiedź `PasswordChangeRequired`.
- Działająca komunikacja podstawowego frontendu z endpointem health.

Ostatni pokazany stan konta:

| UserName | LockoutEnabled | AccessFailedCount | LockoutEnd | IsBlocked | MustChangePassword | Rola |
|---|---|---|---|---|---|---|
| ADMIN | true | 0 | Brak wartości | false | true | Administrator |

### 6.2. Testy wymagające udokumentowania

- Jedna błędna próba zwiększa licznik, a poprawna próba przed blokadą go zeruje.
- Pięć błędnych haseł uruchamia blokadę czasową; poprawne hasło podczas blokady również jest odrzucane.
- Po wygaśnięciu blokady poprawne dane ponownie przechodzą sprawdzenie.
- Nieistniejący login, błędne hasło i zablokowane konto otrzymują ten sam komunikat odrzucenia.
- Powtórne uruchomienie seedera nie tworzy duplikatów kont i ról.
- Refaktoryzacja do `CheckedLogin` zachowuje działanie dotychczasowego endpointu.
- Utworzenie sesji faktycznie zapisuje rekord z hashem tokenu i poprawnymi datami.

Odczyt `LockoutEnabled = true` potwierdza możliwość blokowania konta, ale sam nie potwierdza działania całego procesu blokady. Obecność flagi `MustChangePassword` i odpowiedź statusowa nie zastępują testu wymuszonej zmiany hasła.

## 7. Plan uprawnień i endpointów

### 7.1. Proponowane uprawnienia do notatek

Poniższy podział jest ustaleniem projektowym służącym demonstracji RBAC. Tabela wariantu określa nazwy ról, natomiast nie podaje szczegółowych uprawnień do notatek. Reguły nie są jeszcze zaimplementowane.

| Rola | Odczyt | Tworzenie | Edycja | Usuwanie | Zarządzanie kontami |
|---|---|---|---|---|---|
| Administrator | Wszystkich notatek | Tak | Wszystkich | Wszystkich | Tak |
| Kierownik | Wszystkich notatek | Tak | Wszystkich | Wszystkich | Nie |
| Redaktor | Wszystkich notatek | Tak | Wszystkich | Nie | Nie |
| Pracownik | Wszystkich notatek | Tak | Własnych | Nie | Nie |
| Czytelnik | Wszystkich notatek | Nie | Nie | Nie | Nie |

Kontrola autorstwa przy edycji przez Pracownika musi odbywać się na serwerze. Sesja wymagająca zmiany hasła nie może uzyskać dostępu do notatek ani funkcji administracyjnych, niezależnie od przypisanej roli.

### 7.2. Planowane zasoby API

Poniższe adresy stanowią projekt interfejsu; nie są obecnie działającymi endpointami.

| Obszar | Planowane operacje |
|---|---|
| Uwierzytelnianie | `GET /api/auth/csrf`, `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout` |
| Hasło użytkownika | `POST /api/auth/change-password`, z obsługą pierwszej i zwykłej zmiany |
| Konta | Lista, utworzenie, edycja i usunięcie przez `/api/admin/users`; dodatkowe operacje blokowania, odblokowania, ról i resetu hasła |
| Ustawienia | Odczyt i aktualizacja przez `/api/admin/settings` |
| Notatki | `GET /api/notes`, `GET /api/notes/{id}`, `POST /api/notes`, `PUT /api/notes/{id}`, `DELETE /api/notes/{id}` |
| TOTP | Operacje konfiguracji, potwierdzenia, drugiego kroku logowania i zarządzania kodami odzyskiwania |

## 8. Pozostałe etapy realizacji

### Etap 6 — pełne logowanie i obsługa sesji

1. Połączyć wynik `CheckedLogin` z `ISessionService.CreateAsync`, używając ID konta zweryfikowanego przez serwer.
2. Wdrożyć endpoint logowania i przekazywanie tokenu w cookie HttpOnly, Secure, z dobraną polityką SameSite.
3. Wdrożyć CSRF i obsługę tokenu zabezpieczającego żądania, w tym logowanie.
4. Odczytywać token z cookie, wyszukiwać sesję po hashu oraz kontrolować jej ważność, unieważnienie i stan konta.
5. Utworzyć kontekst uwierzytelnionego użytkownika i udostępnić odczyt bieżącego konta.
6. Egzekwować ograniczenia sesji przeznaczonej wyłącznie do zmiany hasła.
7. Wdrożyć bezczynność, ręczne wylogowanie i unieważnianie sesji na serwerze.

**Kryterium zakończenia:** API rozpoznaje użytkownika w kolejnych żądaniach; sesja wygasła lub unieważniona nie daje dostępu; ADMIN przed zmianą hasła nie otrzymuje pełnych uprawnień.

### Etap 7 — zmiana haseł i polityki

- Pierwsza zmiana hasła oraz zwykła zmiana wymagająca starego hasła.
- Weryfikacja potwierdzenia nowego hasła i wszystkich aktywnych reguł.
- Historia hashy, lista haseł zakazanych i opcjonalny Levenshtein.
- Wygasanie haseł zgodnie z ustawieniami administratora.
- Generator hasła tymczasowego i czterogodzinna ważność przy tworzeniu oraz resecie konta.
- Kontrolowane zakończenie sesji ograniczonej po zmianie hasła oraz ustalone zasady unieważniania pozostałych sesji.

**Kryterium zakończenia:** pierwsze hasło nie umożliwia pominięcia wymaganej zmiany; nowe hasła spełniają politykę, a hasło tymczasowe po terminie jest odrzucane.

### Etap 8 — notatki i autoryzacja

- Model notatki z autorem, DTO, serwis i endpointy CRUD.
- Polityki dostępu według przyjętej macierzy ról.
- Sprawdzanie autorstwa przy operacjach na własnych notatkach.
- Testy bezpośrednich żądań do API dla każdej roli.

**Kryterium zakończenia:** niedozwolone operacje są odrzucane przez API również wtedy, gdy żądanie zostało wysłane z pominięciem interfejsu React.

### Etap 9 — panel administratora

- Zarządzanie kontami, rolami, blokadami i resetami haseł.
- Ustawienia polityki haseł i limitu bezczynności.
- Ochrona stałego ADMIN.
- Uzupełnienie zestawu do sześciu kont testowych i sprawdzenie działania wszystkich ról.
- Określenie oraz wdrożenie unieważniania sesji po zmianach istotnych dla bezpieczeństwa, zwłaszcza blokadzie konta i resecie hasła.

### Etap 10 — interfejs React

- Wspólna obsługa zapytań, błędów, cookie i tokenu CSRF.
- Logowanie, pierwsza zmiana hasła, zwykła zmiana i wylogowanie.
- Odczyt bieżącej sesji i reagowanie na jej wygaśnięcie.
- Lista notatek i formularze zgodne z uprawnieniami.
- Panel zarządzania użytkownikami i ustawieniami.

Prace frontendowe mogą być wykonywane przyrostowo po uruchomieniu odpowiednich endpointów.

### Etap 11 — TOTP i weryfikacja końcowa

- Konfiguracja TOTP, potwierdzenie kodu, drugi krok logowania i kody odzyskiwania.
- Ograniczenie prób i brak pełnego dostępu przed zakończeniem weryfikacji drugiego składnika.
- Testy kompletnego procesu logowania, zmian haseł, sesji, ról i operacji administracyjnych.
- Aktualizacja README, instrukcji uruchomienia i dokumentacji wymagań.
- Przegląd zmian i zapis aktualnego kodu oraz dokumentacji w Git.

Numeracja etapów 6–11 stanowi plan dalszej pracy, a nie wykaz ukończonych lekcji.

## 9. Konfiguracja lokalna i uwagi techniczne

Katalog repozytorium:

```text
C:\Users\Natan\source\repos\777Nnatii777\AccessLab
```

Plik `compose.yaml` znajduje się w katalogu głównym. Usługa bazy nazywa się `postgres`; baza i użytkownik bazy mają nazwę `accesslab`. PostgreSQL musi działać przed uruchomieniem API korzystającego z bazy.

`ConnectionStrings:DefaultConnection` oraz `Bootstrap:AdminPassword` są przechowywane w User Secrets projektu Api. Dokumentacja i repozytorium nie powinny zawierać wartości sekretów.

| Element | Ostatnio znana konfiguracja |
|---|---|
| API HTTPS | `https://localhost:7068` — adres użyty w udanym teście |
| API HTTP | `http://localhost:5285` według `launchSettings.json` |
| Swagger UI | `https://localhost:7068/swagger` |
| Proxy Vite | Ostatni udostępniony plik wskazywał `http://localhost:5000`; zgodność z uruchomionym API wymaga sprawdzenia przed dalszą integracją frontendu |
| Narzędzie EF | Manifest zapisany wcześniej jako `backend/dotnet-tools.json` |

Wcześniejszy problem z brakiem kolumn `IsBlocked` i `MustChangePassword` rozwiązano przez zmianę pól na właściwości z `get; set;` i dodanie migracji. Problem z niedostępnymi typami Application w `LoginService` rozwiązano przed dalszą implementacją; kompilacja została potwierdzona.

## 10. Bieżący punkt kontynuacji

Ostatnią wykonaną zmianą jest przygotowanie `CheckedLogin` oraz dostosowanie `ILoginService`, `LoginService` i testowego `AuthController` do odczytu `result.Status` i przekazywania `UserId` po stronie serwera.

**Najbliższe zadanie:** wdrożenie połączenia sprawdzania hasła z tworzeniem sesji i cookie, wraz z ochroną CSRF i obsługą sesji w kolejnych żądaniach. Następnie wymagana pierwsza zmiana hasła, wylogowanie i autoryzacja notatek.

Przed wdrożeniem tego fragmentu pozostaje przegląd aktualnych wersji `Program.cs`, `AuthController.cs`, `LoginService.cs`, `SessionService.cs`, `CheckedLogin.cs` i `ApplicationDbContext.cs`. Dokument stanowi zapis stanu projektu; źródłem ostatecznego stanu implementacji są aktualne pliki repozytorium i wyniki testów.
