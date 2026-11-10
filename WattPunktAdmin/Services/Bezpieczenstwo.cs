using System.Security.Cryptography;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

// Skrot hasla PBKDF2 (SHA-256) z losowa sola. W bazie nigdy nie ma hasla w postaci jawnej.
public static class Hasla
{
    public const int Iteracje = 100_000;
    private const int DlugoscSoli = 16;
    private const int DlugoscSkrotu = 32;

    public static (byte[] Skrot, byte[] Sol) Utworz(string haslo)
    {
        var sol = RandomNumberGenerator.GetBytes(DlugoscSoli);
        return (Policz(haslo, sol, Iteracje), sol);
    }

    public static bool Sprawdz(string haslo, byte[] skrot, byte[] sol, int iteracje)
    {
        var policzony = Policz(haslo, sol, iteracje);
        return CryptographicOperations.FixedTimeEquals(policzony, skrot);   // porownanie w stalym czasie
    }

    private static byte[] Policz(string haslo, byte[] sol, int iteracje) =>
        Rfc2898DeriveBytes.Pbkdf2(haslo, sol, iteracje, HashAlgorithmName.SHA256, DlugoscSkrotu);

    // haslo tymczasowe po zalozeniu konta lub resecie: bez znakow latwych do pomylenia (0/O, 1/l)
    public static string Tymczasowe()
    {
        const string litery = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string cyfry = "23456789";
        var znaki = new char[10];
        for (int i = 0; i < znaki.Length; i++)
            znaki[i] = i < 7 ? litery[RandomNumberGenerator.GetInt32(litery.Length)] : cyfry[RandomNumberGenerator.GetInt32(cyfry.Length)];
        return new string(znaki);
    }

    public static string? SprawdzSile(string haslo)
    {
        if (haslo.Length < 8) return "Hasło musi mieć co najmniej 8 znaków.";
        if (!haslo.Any(char.IsLetter) || !haslo.Any(char.IsDigit)) return "Hasło musi zawierać literę i cyfrę.";
        return null;
    }
}

// Jedno miejsce z regulami uprawnien: korzystaja z niego ViewModele (wyglad) i uslugi (kontrola)
public static class Uprawnienia
{
    public static bool EdycjaInfrastruktury(Rola rola) => rola == Rola.Administrator;
    public static bool Usuwanie(Rola rola) => rola == Rola.Administrator;
    public static bool ZarzadzaniePracownikami(Rola rola) => rola == Rola.Administrator;
    public static bool PrzegladanieDziennika(Rola rola) => rola == Rola.Administrator;
    public static bool ObslugaKlientow(Rola rola) => true;
    public static bool ObslugaAwarii(Rola rola) => true;
}

public class BrakUprawnienException(string operacja)
    : Exception($"Brak uprawnień do operacji: {operacja}. Ta operacja jest dostępna tylko dla administratora.");

// Naruszenie reguly biznesowej: komunikat jest od razu zrozumialy dla uzytkownika
public class RegulaException(string komunikat) : Exception(komunikat);

public class SesjaUzytkownika
{
    public Pracownik? Pracownik { get; private set; }
    public bool Zalogowany => Pracownik is not null;
    public Rola Rola => Pracownik?.Rola ?? Rola.Pracownik;
    public int PracownikID => Pracownik?.PracownikID ?? throw new InvalidOperationException("Nikt nie jest zalogowany.");

    public void Rozpocznij(Pracownik pracownik) => Pracownik = pracownik;
    public void Zakoncz() => Pracownik = null;

    public void Wymagaj(Func<Rola, bool> regula, string operacja)
    {
        if (Pracownik is null || !regula(Pracownik.Rola))
            throw new BrakUprawnienException(operacja);
    }
}
