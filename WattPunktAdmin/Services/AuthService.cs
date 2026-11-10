using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public record WynikLogowania(bool Sukces, string Komunikat);

public interface IAuthService
{
    Task<WynikLogowania> ZalogujAsync(string login, string haslo);
    Task ZmienHasloAsync(string stare, string nowe);
    void Wyloguj();
}

public class AuthService(IFabrykaKontekstu fabryka, SesjaUzytkownika sesja) : IAuthService
{
    public const int LimitProb = 5;

    public async Task<WynikLogowania> ZalogujAsync(string login, string haslo)
    {
        await using var db = fabryka.Utworz();
        var p = await db.Pracownicy.AsTracking().FirstOrDefaultAsync(x => x.Login == login.Trim());

        // ten sam poczatek komunikatu dla zlego loginu i zlego hasla; licznik pozostalych prob pokazujemy swiadomie,
        // zeby pracownik wiedzial o grozacej blokadzie (kosztem tego, ze zdradza istnienie loginu)
        const string zleDane = "Nieprawidłowy login lub hasło.";
        if (p is null) return new(false, zleDane);
        if (!p.Aktywny) return new(false, "Konto jest wyłączone. Skontaktuj się z administratorem.");
        if (p.NieudaneProby >= LimitProb)
            return new(false, $"Konto zostało zablokowane po {LimitProb} nieudanych próbach logowania. Odblokować je może administrator.");

        if (!Hasla.Sprawdz(haslo, p.HasloHash, p.HasloSol, p.Iteracje))
        {
            p.NieudaneProby++;
            if (p.NieudaneProby == LimitProb)
                db.DziennikZmian.Add(new WpisDziennika { PracownikID = p.PracownikID, Data = DateTime.Now, Operacja = "konto", Obiekt = "Pracownik", ObiektID = p.PracownikID,
                                                         Opis = $"Zablokowano konto {p.Login} po {LimitProb} nieudanych próbach logowania" });
            await db.SaveChangesAsync();
            var zostalo = LimitProb - p.NieudaneProby;
            return new(false, zostalo > 0 ? $"{zleDane} Pozostałe próby: {zostalo}."
                                          : $"{zleDane} Konto zostało zablokowane.");
        }

        p.NieudaneProby = 0;
        p.OstatnieLogowanie = DateTime.Now;
        db.DziennikZmian.Add(new WpisDziennika { PracownikID = p.PracownikID, Data = DateTime.Now, Operacja = "logowanie", Obiekt = "Konto", ObiektID = p.PracownikID, Opis = $"Zalogowano: {p.Login}" });
        await db.SaveChangesAsync();
        sesja.Rozpocznij(p);
        return new(true, $"Zalogowano: {p.ImieNazwisko}");
    }

    public async Task ZmienHasloAsync(string stare, string nowe)
    {
        await using var db = fabryka.Utworz();
        var p = await db.Pracownicy.AsTracking().FirstAsync(x => x.PracownikID == sesja.PracownikID);
        if (!Hasla.Sprawdz(stare, p.HasloHash, p.HasloSol, p.Iteracje))
            throw new RegulaException("Obecne hasło jest nieprawidłowe.");
        if (Hasla.Sprawdz(nowe, p.HasloHash, p.HasloSol, p.Iteracje))
            throw new RegulaException("Nowe hasło musi się różnić od obecnego.");
        var blad = Hasla.SprawdzSile(nowe);
        if (blad is not null) throw new RegulaException(blad);

        (p.HasloHash, p.HasloSol) = Hasla.Utworz(nowe);
        p.Iteracje = Hasla.Iteracje;
        p.WymagaZmianyHasla = false;
        db.DziennikZmian.Add(new WpisDziennika { PracownikID = p.PracownikID, Data = DateTime.Now, Operacja = "konto", Obiekt = "Konto", ObiektID = p.PracownikID, Opis = "Zmieniono własne hasło" });
        await db.SaveChangesAsync();
        sesja.Pracownik!.WymagaZmianyHasla = false;
    }

    public void Wyloguj() => sesja.Zakoncz();
}
