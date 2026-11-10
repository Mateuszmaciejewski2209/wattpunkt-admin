# WattPunkt Admin

Aplikacja desktopowa WPF (wzorzec MVVM) do obsługi sieci stacji ładowania samochodów elektrycznych firmy WattPunkt sp. z o.o. Dane są przechowywane w relacyjnej bazie SQL Server.

## Wymagania

* Windows 10 lub 11
* .NET 10 SDK (lub Visual Studio 2026 z obciążeniem „Programowanie aplikacji klasycznych .NET”)
* SQL Server 2019 lub nowszy (wystarczy wersja Express)

## Przygotowanie bazy danych

Skrypty z folderu `baza` należy uruchomić po kolei w SSMS albo w sqlcmd:

1. `01_utworz_baze.sql` tworzy bazę `WattPunktAdmin`, wszystkie tabele z kluczami i ograniczeniami oraz dane słownikowe,
2. `02_generuj_dane.sql` wypełnia bazę danymi (około 2,4 tys. klientów i 133 tys. sesji ładowania, trwa kilkadziesiąt sekund),
3. `03_konta_testowe.sql` dodaje konta testowe pracowników.

Przykład dla sqlcmd:

```
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -i baza\01_utworz_baze.sql
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -i baza\02_generuj_dane.sql
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -i baza\03_konta_testowe.sql
```

## Połączenie z bazą

Parametry połączenia są w pliku `WattPunktAdmin\ustawienia.json`. Domyślnie aplikacja łączy się z instancją `.\SQLEXPRESS` z uwierzytelnianiem Windows. Przy innej instancji wystarczy zmienić wartość `PolaczenieZBaza`.

## Uruchomienie

Otworzyć `WattPunktAdmin.sln` w Visual Studio i uruchomić projekt `WattPunktAdmin` albo z wiersza poleceń:

```
dotnet run --project WattPunktAdmin
```

Testy jednostkowe:

```
dotnet test
```

## Konta testowe

| Login | Hasło | Rola | Uwagi |
|---|---|---|---|
| admin | Admin2026! | Administrator | pełne uprawnienia |
| pkowalski | Pracownik2026! | Pracownik | obsługa klientów i awarii |
| kwisniewska | Serwis2026! | Pracownik | |
| mnowak | Wylaczone2026! | Pracownik | konto wyłączone, logowanie odrzucane |

Hasła są zapisane w bazie jako skrót PBKDF2 (SHA-256, 100 000 iteracji) z losową solą. Po pięciu błędnych próbach konto zostaje zablokowane, odblokować je może administrator w zakładce Pracownicy.

## Struktura projektu

* `Sprawozdanie - WattPunkt Admin.docx` (i wersja PDF) sprawozdanie z projektu
* `baza` skrypty SQL
* `WattPunktAdmin\Models` encje bazy oraz rekordy wierszy list i filtry
* `WattPunktAdmin\Data` kontekst Entity Framework Core i fabryka kontekstów
* `WattPunktAdmin\Services` logika biznesowa, uprawnienia, obsługa błędów bazy
* `WattPunktAdmin\ViewModels` modele widoków i formularze z walidacją
* `WattPunktAdmin\Widoki` okna, widoki zakładek, style i konwertery
* `WattPunktAdmin.Tests` testy jednostkowe MSTest
* `logi\bledy.log` (tworzony przy pierwszym błędzie obok pliku exe) szczegóły nieoczekiwanych wyjątków
