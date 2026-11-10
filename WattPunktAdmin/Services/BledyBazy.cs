using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace WattPunktAdmin.Services;

// Zamiana wyjatkow z bazy i EF Core na komunikaty dla uzytkownika
public static class BledyBazy
{
    public static string Opisz(Exception ex)
    {
        switch (ex)
        {
            case RegulaException or BrakUprawnienException:
                return ex.Message;
            case DbUpdateConcurrencyException:
                return "Rekord został w międzyczasie zmieniony lub usunięty przez innego pracownika. Odśwież dane i spróbuj ponownie.";
            case DbUpdateException { InnerException: SqlException sql }:
                return OpiszSql(sql);
            case SqlException sql:
                return OpiszSql(sql);
            case InvalidOperationException { InnerException: SqlException sql }:
                return OpiszSql(sql);
            case TimeoutException:
                return "Serwer bazy danych nie odpowiada. Spróbuj ponownie za chwilę.";
            case FileNotFoundException or InvalidDataException:
                return ex.Message;
            default:
                return $"Nieoczekiwany błąd: {ex.Message}. Szczegóły zapisano w pliku dziennika.";
        }
    }

    private static string OpiszSql(SqlException sql) => sql.Number switch
    {
        -2 => "Przekroczono czas oczekiwania na odpowiedź serwera bazy danych.",
        -1 or 2 or 53 or 26 or 40 or 10060 or 10061 => "Brak połączenia z serwerem bazy danych. Sprawdź, czy SQL Server działa i czy dane połączenia w pliku ustawienia.json są poprawne.",
        4060 => "Baza danych WattPunktAdmin nie istnieje na serwerze. Uruchom skrypty z folderu baza.",
        18456 => "Serwer bazy danych odrzucił logowanie.",
        547 => "Operacja narusza powiązania między danymi (rekord jest używany w innych tabelach albo wskazuje nieistniejący rekord).",
        2601 or 2627 => "Taka wartość już istnieje w bazie (np. kod stacji, numer seryjny ładowarki albo login).",
        8152 or 2628 => "Wpisany tekst jest za długi dla tego pola.",
        _ => $"Błąd bazy danych ({sql.Number}): {sql.Message}"
    };
}

// Prosty dziennik bledow w pliku obok programu
public static class Log
{
    private static readonly object blokada = new();
    public static string Plik => Path.Combine(AppContext.BaseDirectory, "logi", "bledy.log");

    public static void Zapisz(Exception ex, string? kontekst = null)
    {
        try
        {
            lock (blokada)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Plik)!);
                File.AppendAllText(Plik, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kontekst}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // brak mozliwosci zapisu dziennika nie moze zatrzymac programu
        }
    }
}
