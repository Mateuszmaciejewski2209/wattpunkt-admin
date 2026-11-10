using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface IAwarieService
{
    Task<List<AwariaWiersz>> AwarieAsync(FiltrAwarii filtr);
    Task<int> ZglosAsync(Awaria dane);
    Task ZamknijAsync(int awariaId, DateTime koniec);
    Task UsunAsync(int awariaId);
}

public class AwarieService(IFabrykaKontekstu fabryka, SesjaUzytkownika sesja) : IAwarieService
{
    public async Task<List<AwariaWiersz>> AwarieAsync(FiltrAwarii f)
    {
        await using var db = fabryka.Utworz();
        var q = db.Awarie.AsQueryable();
        if (f.TylkoOtwarte) q = q.Where(a => a.Koniec == null);
        if (f.StacjaID is int sid) q = q.Where(a => a.Ladowarka!.StacjaID == sid);
        if (f.KategoriaID is int kid) q = q.Where(a => a.KategoriaID == kid);
        if (f.Od is DateTime od) q = q.Where(a => a.Poczatek >= od);
        if (f.Do is DateTime @do) q = q.Where(a => a.Poczatek < @do.AddDays(1));
        return await q.OrderByDescending(a => a.Koniec == null).ThenByDescending(a => a.Poczatek)
            .Select(a => new AwariaWiersz(a.AwariaID, a.Poczatek, a.Koniec, a.Ladowarka!.Stacja!.Kod + " " + a.Ladowarka.Stacja.Nazwa,
                                          a.Ladowarka.NumerSeryjny, a.Kategoria!.Nazwa, a.Opis))
            .ToListAsync();
    }

    public async Task<int> ZglosAsync(Awaria dane)
    {
        sesja.Wymagaj(Uprawnienia.ObslugaAwarii, "zgłoszenie awarii");
        if (dane.Poczatek > DateTime.Now.AddMinutes(5))
            throw new RegulaException("Początek awarii nie może być w przyszłości.");
        await using var db = fabryka.Utworz();
        var l = await db.Ladowarki.Include(x => x.Stacja).FirstOrDefaultAsync(x => x.LadowarkaID == dane.LadowarkaID)
                ?? throw new RegulaException("Wybrana ładowarka nie istnieje.");
        if (await db.Awarie.AnyAsync(a => a.LadowarkaID == dane.LadowarkaID && a.Koniec == null))
            throw new RegulaException($"Ładowarka {l.NumerSeryjny} ma już otwartą awarię. Zamknij ją, zanim zgłosisz następną.");
        if (dane.Poczatek < l.DataInstalacji)
            throw new RegulaException("Awaria nie może się zacząć przed instalacją ładowarki.");

        var a = new Awaria { LadowarkaID = dane.LadowarkaID, KategoriaID = dane.KategoriaID, Poczatek = dane.Poczatek, Opis = dane.Opis?.Trim() };
        db.Awarie.Add(a);
        await db.SaveChangesAsync();
        Dziennik.Dodaj(db, sesja, "dodanie", "Awaria", a.AwariaID, $"Zgłoszono awarię ładowarki {l.NumerSeryjny} ({l.Stacja!.Kod})");
        await db.SaveChangesAsync();
        return a.AwariaID;
    }

    public async Task ZamknijAsync(int awariaId, DateTime koniec)
    {
        sesja.Wymagaj(Uprawnienia.ObslugaAwarii, "zamknięcie awarii");
        await using var db = fabryka.Utworz();
        var a = await db.Awarie.AsTracking().Include(x => x.Ladowarka).FirstOrDefaultAsync(x => x.AwariaID == awariaId)
                ?? throw new RegulaException("Awaria została usunięta.");
        if (a.Koniec is not null) throw new RegulaException("Ta awaria jest już zamknięta.");
        if (koniec < a.Poczatek) throw new RegulaException("Koniec awarii nie może być wcześniejszy niż jej początek.");
        a.Koniec = koniec;
        Dziennik.Dodaj(db, sesja, "edycja", "Awaria", a.AwariaID, $"Zamknięto awarię ładowarki {a.Ladowarka!.NumerSeryjny}, czas naprawy {(koniec - a.Poczatek).TotalHours:0.0} h");
        await db.SaveChangesAsync();
    }

    public async Task UsunAsync(int awariaId)
    {
        sesja.Wymagaj(Uprawnienia.Usuwanie, "usuwanie awarii");
        await using var db = fabryka.Utworz();
        var a = await db.Awarie.AsTracking().FirstOrDefaultAsync(x => x.AwariaID == awariaId)
                ?? throw new RegulaException("Awaria została już usunięta.");
        db.Awarie.Remove(a);
        Dziennik.Dodaj(db, sesja, "usunięcie", "Awaria", awariaId, $"Usunięto wpis awarii {awariaId} (zgłoszenie z {a.Poczatek:dd.MM.yyyy HH:mm})");
        await db.SaveChangesAsync();
    }
}
