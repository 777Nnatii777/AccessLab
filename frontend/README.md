# AccessLab — frontend demonstracyjny

React + TypeScript + Vite. Interfejs jest celowo prosty, podobny do panelu testowego Swagger.

Uruchom backend HTTPS na `https://localhost:7068` i zaufaj lokalnemu certyfikatowi
(`dotnet dev-certs https --trust`). Następnie:

```powershell
npm ci
npm run dev
```

Otwórz URL wypisany przez Vite (zwykle `http://localhost:5173`).
Proxy `/api` jest ustawione w `vite.config.ts` na backend HTTPS. Wszystkie operacje
modyfikujące dane pobierają automatycznie aktualny token CSRF.

Panel pozwala zalogować się, zmienić hasło, dodać i edytować notatki, administrować
kontami, ustawić politykę haseł oraz uruchomić TOTP. Kody odzyskiwania i hasła
jednorazowe trzeba bezpiecznie zachować po wyświetleniu.

Uwaga: ciasteczka `Secure` na lokalnym HTTP (`localhost`) zależą od zachowania
przeglądarki. Jeżeli `/auth/me` zwraca 401 po poprawnym logowaniu, sprawdź Cookies
w DevTools i korzystaj z jednej nazwy hosta `localhost` (nie przełączaj na 127.0.0.1).
