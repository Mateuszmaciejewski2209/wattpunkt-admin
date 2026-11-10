using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface IPracownicyService
{
    Task<List<PracownikWiersz>> PracownicyAsync();
    Task<Pracownik?> PracownikAsync(int id);
    /// <summary>Zapisuje konto. Dla nowego konta zwraca haslo tymczasowe.</summary>
    Task<string?> ZapiszAsync(Pracownik dane);
    Task<string> ResetujHasloAsync(int id);
    Task OdblokujAsync(int id);
    Task<List<WpisDziennikaWiersz>> DziennikAsync(FiltrDziennika filtr);
}

public class PracownicyService(IFabrykaKontekstu fabryka, SesjaUzytkownika sesja) : IPracownicyService
{
    public async Task<List<PracownikWiersz>> PracownicyAsync()
    {
        sesja.Wymagaj(Uprawnienia.ZarzadzaniePracownikami, "lista pracowników");
        await using var db = fabryka.Utworz();
        return await db.Pracownicy.OrderBy(p => p.Nazwisko).ThenBy(p => p.Imie)
            .Select(p => new PracownikWiersz(p.PracownikID, p.Login, p.Imie + " " + p.Nazwisko, p.Rola, p.Aktywny,
                                             p.NieudaneProby, p.WymagaZmianyHasla, p.OstatnieLogowanie))
            .ToListAsync();
    }

    public async Task<Pracownik?> PracownikAsync(int id)
    {
        sesja.Wymagaj(Uprawnienia.ZarzadzaniePracownikami, "dane pracownika");
        await using var db = fabryka.Utworz();
        return await db.Pracownicy.FirstOrDefaultAsync(p => p.PracownikID == id);
    }

    public async Task<string?> ZapiszAsync(Pracownik dane)
    {
        sesja.Wymagaj(Uprawnienia.ZarzadzaniePracownikami, "zapis konta pracownika");
        await using var db = fabryka.Utworz();
        var login = dane.Login.Trim().ToLowerInvariant();
        if (await db.Pracownicy.AnyAsync(p => p.Login == login && p.PracownikID != dane.PracownikID))
            throw new RegulaException($"Login {login} jest już zajęty.");

        if (dane.PracownikID == 0)
        {
            var haslo = Hasla.Tymczasowe();
            var (skrot, sol) = Hasla.Utworz(haslo);
            var nowy = new Pracownik { Login = login, Imie = dane.Imie.Trim(), Nazwisko = dane.Nazwisko.Trim(), Rola = dane.Rola,
                                       HasloHash = skrot, HasloSol = sol, Iteracje = Hasla.Iteracje, Aktywny = dane.Aktywny,
                                       WymagaZmianyHasla = true };
            db.Pracownicy.Add(nowy);
            await db.SaveChangesAsync();
            Dziennik.Dodaj(db, sesja, "konto", "Pracownik", nowy.PracownikID, $"Założono konto {login} ({nowy.Rola})");
            await db.SaveChangesAsync();
            return haslo;
        }

        // administrator nie moze odebrac sobie roli ani wylaczyc wlasnego konta: w systemie zawsze zostaje administrator
        if (dane.PracownikID == sesja.PracownikID && (dane.Rola != Rola.Administrator || !dane.Aktywny))
            throw new RegulaException("Nie możesz odebrać sobie roli administratora ani wyłączyć własnego konta.");

        var p = await db.Pracownicy.AsTracking().FirstOrDefaultAsync(x => x.PracownikID == dane.PracownikID)
                ?? throw new RegulaException("Konto zostało usunięte.");
        db.Entry(p).Property(x => x.Wersja).OriginalValue = dane.Wersja;
        var zmiany = Dziennik.Zmiany(("login", p.Login, login), ("imię", p.Imie, dane.Imie.Trim()), ("nazwisko", p.Nazwisko, dane.Nazwisko.Trim()),
                                     ("rola", p.Rola, dane.Rola), ("aktywność", p.Aktywny, dane.Aktywny));
        p.Login = login; p.Imie = dane.Imie.Trim(); p.Nazwisko = dane.Nazwisko.Trim(); p.Rola = dane.Rola; p.Aktywny = dane.Aktywny;
        Dziennik.Dodaj(db, sesja, "konto", "Pracownik", p.PracownikID, $"Zmieniono konto {p.Login}: {zmiany}");
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string> ResetujHasloAsync(int id)
    {
        sesja.Wymagaj(Uprawnienia.ZarzadzaniePracownikami, "reset hasła");
        await using var db = fabryka.Utworz();
        var p = await db.Pracownicy.AsTracking().FirstOrDefaultAsync(x => x.PracownikID == id)
                ?? throw new RegulaException("Konto nie istnieje.");
        var haslo = Hasla.Tymczasowe();
        (p.HasloHash, p.HasloSol) = Hasla.Utworz(haslo);
        p.Iteracje = Hasla.Iteracje;
        p.WymagaZmianyHasla = true;
        p.NieudaneProby = 0;
        Dziennik.Dodaj(db, sesja, "konto", "Pracownik", id, $"Zresetowano hasło konta {p.Login}");
        await db.SaveChangesAsync();
        return haslo;
    }

    public async Task OdblokujAsync(int id)
    {
        sesja.Wymagaj(Uprawnienia.ZarzadzaniePracownikami, "odblokowanie konta");
        await using var db = fabryka.Utworz();
        var p = await db.Pracownicy.AsTracking().FirstOrDefaultAsync(x => x.PracownikID == id)
                ?? throw new RegulaException("Konto nie istnieje.");
        p.NieudaneProby = 0;
        Dziennik.Dodaj(db, sesja, "konto", "Pracownik", id, $"Odblokowano konto {p.Login}");
        await db.SaveChangesAsync();
    }

    public async Task<List<WpisDziennikaWiersz>> DziennikAsync(FiltrDziennika f)
    {
        sesja.Wymagaj(Uprawnienia.PrzegladanieDziennika, "dziennik zmian");
        await using var db = fabryka.Utworz();
        var q = db.DziennikZmian.AsQueryable();
        if (f.PracownikID is int pid) q = q.Where(w => w.PracownikID == pid);
        if (!string.IsNullOrEmpty(f.Operacja)) q = q.Where(w => w.Operacja == f.Operacja);
        if (f.Od is DateTime od) q = q.Where(w => w.Data >= od);
        if (f.Do is DateTime @do) q = q.Where(w => w.Data < @do.AddDays(1));
        return await q.OrderByDescending(w => w.WpisID).Take(1000)
            .Select(w => new WpisDziennikaWiersz(w.WpisID, w.Data, w.Pracownik!.Login, w.Operacja, w.Obiekt, w.ObiektID, w.Opis))
            .ToListAsync();
    }
}
