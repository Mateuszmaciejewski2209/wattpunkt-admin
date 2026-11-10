using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface IStacjeService
{
    Task<List<StacjaWiersz>> StacjeAsync(FiltrStacji filtr);
    Task<Stacja?> StacjaAsync(int id);
    Task<int> ZapiszStacjeAsync(Stacja dane);
    Task UsunStacjeAsync(int id);

    Task<List<LadowarkaWiersz>> LadowarkiAsync(FiltrLadowarek filtr);
    Task<Ladowarka?> LadowarkaAsync(int id);
    Task<int> ZapiszLadowarkeAsync(Ladowarka dane);
    Task UsunLadowarkeAsync(int id);
}

public class StacjeService(IFabrykaKontekstu fabryka, SesjaUzytkownika sesja) : IStacjeService
{
    public async Task<List<StacjaWiersz>> StacjeAsync(FiltrStacji f)
    {
        await using var db = fabryka.Utworz();
        var q = db.Stacje.AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Szukaj))
        {
            var s = f.Szukaj.Trim();
            q = q.Where(x => x.Kod.Contains(s) || x.Nazwa.Contains(s) || x.Miejscowosc!.Nazwa.Contains(s));
        }
        if (!string.IsNullOrEmpty(f.Wojewodztwo)) q = q.Where(x => x.Miejscowosc!.Wojewodztwo == f.Wojewodztwo);
        if (!string.IsNullOrEmpty(f.TypLokalizacji)) q = q.Where(x => x.TypLokalizacji == f.TypLokalizacji);
        return await q.OrderBy(x => x.Kod)
            .Select(x => new StacjaWiersz(x.StacjaID, x.Kod, x.Nazwa, x.Miejscowosc!.Nazwa, x.Miejscowosc.Wojewodztwo,
                                         x.TypLokalizacji, x.DataUruchomienia, x.Ladowarki.Count))
            .ToListAsync();
    }

    public async Task<Stacja?> StacjaAsync(int id)
    {
        await using var db = fabryka.Utworz();
        return await db.Stacje.FirstOrDefaultAsync(x => x.StacjaID == id);
    }

    public async Task<int> ZapiszStacjeAsync(Stacja dane)
    {
        sesja.Wymagaj(Uprawnienia.EdycjaInfrastruktury, "zapis stacji");
        await using var db = fabryka.Utworz();
        dane.Kod = dane.Kod.Trim().ToUpperInvariant();
        if (await db.Stacje.AnyAsync(x => x.Kod == dane.Kod && x.StacjaID != dane.StacjaID))
            throw new RegulaException($"Stacja o kodzie {dane.Kod} już istnieje.");

        await using var tx = await db.Database.BeginTransactionAsync();
        if (dane.StacjaID == 0)
        {
            var nowa = new Stacja { Kod = dane.Kod, Nazwa = dane.Nazwa.Trim(), MiejscowoscID = dane.MiejscowoscID,
                                    TypLokalizacji = dane.TypLokalizacji, DataUruchomienia = dane.DataUruchomienia };
            db.Stacje.Add(nowa);
            await db.SaveChangesAsync();
            Dziennik.Dodaj(db, sesja, "dodanie", "Stacja", nowa.StacjaID, $"Dodano stację {nowa.Kod} {nowa.Nazwa}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return nowa.StacjaID;
        }

        var s = await db.Stacje.AsTracking().FirstOrDefaultAsync(x => x.StacjaID == dane.StacjaID)
                ?? throw new RegulaException("Stacja została w międzyczasie usunięta.");
        db.Entry(s).Property(x => x.Wersja).OriginalValue = dane.Wersja;      // kontrola wspolbieznosci
        var zmiany = Dziennik.Zmiany(("kod", s.Kod, dane.Kod), ("nazwa", s.Nazwa, dane.Nazwa.Trim()),
            ("miejscowość", s.MiejscowoscID, dane.MiejscowoscID), ("typ lokalizacji", s.TypLokalizacji, dane.TypLokalizacji),
            ("data uruchomienia", s.DataUruchomienia, dane.DataUruchomienia));
        s.Kod = dane.Kod; s.Nazwa = dane.Nazwa.Trim(); s.MiejscowoscID = dane.MiejscowoscID;
        s.TypLokalizacji = dane.TypLokalizacji; s.DataUruchomienia = dane.DataUruchomienia;
        Dziennik.Dodaj(db, sesja, "edycja", "Stacja", s.StacjaID, $"Zmieniono stację {s.Kod}: {zmiany}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return s.StacjaID;
    }

    public async Task UsunStacjeAsync(int id)
    {
        sesja.Wymagaj(Uprawnienia.Usuwanie, "usuwanie stacji");
        await using var db = fabryka.Utworz();
        var s = await db.Stacje.AsTracking().FirstOrDefaultAsync(x => x.StacjaID == id)
                ?? throw new RegulaException("Stacja została już usunięta.");
        var ladowarek = await db.Ladowarki.CountAsync(l => l.StacjaID == id);
        if (ladowarek > 0)
        {
            var slowo = ladowarek == 1 ? "ładowarkę"
                      : ladowarek % 10 is >= 2 and <= 4 && ladowarek % 100 is < 12 or > 14 ? "ładowarki" : "ładowarek";
            throw new RegulaException($"Nie można usunąć stacji {s.Kod}, bo ma {ladowarek} {slowo}. Najpierw usuń lub przenieś ładowarki.");
        }
        db.Stacje.Remove(s);
        Dziennik.Dodaj(db, sesja, "usunięcie", "Stacja", id, $"Usunięto stację {s.Kod} {s.Nazwa}");
        await db.SaveChangesAsync();
    }

    public async Task<List<LadowarkaWiersz>> LadowarkiAsync(FiltrLadowarek f)
    {
        await using var db = fabryka.Utworz();
        var q = db.Ladowarki.AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Szukaj))
        {
            var s = f.Szukaj.Trim();
            q = q.Where(x => x.NumerSeryjny.Contains(s) || x.Stacja!.Nazwa.Contains(s) || x.Model!.Producent.Contains(s));
        }
        if (f.StacjaID is int sid) q = q.Where(x => x.StacjaID == sid);
        if (f.KlasaMocy == "AC") q = q.Where(x => x.Model!.TypPradu == "AC");
        if (f.KlasaMocy == "DC") q = q.Where(x => x.Model!.TypPradu == "DC" && x.Model.MocKW < 150);
        if (f.KlasaMocy == "HPC") q = q.Where(x => x.Model!.MocKW >= 150);
        return await q.OrderBy(x => x.Stacja!.Kod).ThenBy(x => x.NumerSeryjny)
            .Select(x => new LadowarkaWiersz(x.LadowarkaID, x.NumerSeryjny, x.StacjaID, x.Stacja!.Kod + " " + x.Stacja.Nazwa,
                x.Model!.Producent + " " + x.Model.Model, x.Model.MocKW,
                x.Model.TypPradu == "AC" ? "AC" : x.Model.MocKW >= 150 ? "HPC" : "DC",
                x.DataInstalacji,
                db.Awarie.Count(a => a.LadowarkaID == x.LadowarkaID),
                db.Awarie.Any(a => a.LadowarkaID == x.LadowarkaID && a.Koniec == null)))
            .ToListAsync();
    }

    public async Task<Ladowarka?> LadowarkaAsync(int id)
    {
        await using var db = fabryka.Utworz();
        return await db.Ladowarki.FirstOrDefaultAsync(x => x.LadowarkaID == id);
    }

    public async Task<int> ZapiszLadowarkeAsync(Ladowarka dane)
    {
        sesja.Wymagaj(Uprawnienia.EdycjaInfrastruktury, "zapis ładowarki");
        await using var db = fabryka.Utworz();
        dane.NumerSeryjny = dane.NumerSeryjny.Trim().ToUpperInvariant();
        if (await db.Ladowarki.AnyAsync(x => x.NumerSeryjny == dane.NumerSeryjny && x.LadowarkaID != dane.LadowarkaID))
            throw new RegulaException($"Ładowarka o numerze seryjnym {dane.NumerSeryjny} już istnieje.");
        var stacja = await db.Stacje.FirstOrDefaultAsync(s => s.StacjaID == dane.StacjaID)
                     ?? throw new RegulaException("Wybrana stacja nie istnieje.");
        if (dane.DataInstalacji < stacja.DataUruchomienia)
            throw new RegulaException($"Data instalacji nie może być wcześniejsza niż uruchomienie stacji ({stacja.DataUruchomienia:dd.MM.yyyy}).");

        await using var tx = await db.Database.BeginTransactionAsync();
        if (dane.LadowarkaID == 0)
        {
            var nowa = new Ladowarka { StacjaID = dane.StacjaID, ModelLadowarkiID = dane.ModelLadowarkiID,
                                       NumerSeryjny = dane.NumerSeryjny, DataInstalacji = dane.DataInstalacji };
            db.Ladowarki.Add(nowa);
            await db.SaveChangesAsync();
            Dziennik.Dodaj(db, sesja, "dodanie", "Ładowarka", nowa.LadowarkaID, $"Dodano ładowarkę {nowa.NumerSeryjny} na stacji {stacja.Kod}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return nowa.LadowarkaID;
        }

        var l = await db.Ladowarki.AsTracking().FirstOrDefaultAsync(x => x.LadowarkaID == dane.LadowarkaID)
                ?? throw new RegulaException("Ładowarka została w międzyczasie usunięta.");
        db.Entry(l).Property(x => x.Wersja).OriginalValue = dane.Wersja;
        var zmiany = Dziennik.Zmiany(("stacja", l.StacjaID, dane.StacjaID), ("model", l.ModelLadowarkiID, dane.ModelLadowarkiID),
            ("numer seryjny", l.NumerSeryjny, dane.NumerSeryjny), ("data instalacji", l.DataInstalacji, dane.DataInstalacji));
        l.StacjaID = dane.StacjaID; l.ModelLadowarkiID = dane.ModelLadowarkiID;
        l.NumerSeryjny = dane.NumerSeryjny; l.DataInstalacji = dane.DataInstalacji;
        Dziennik.Dodaj(db, sesja, "edycja", "Ładowarka", l.LadowarkaID, $"Zmieniono ładowarkę {l.NumerSeryjny}: {zmiany}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return l.LadowarkaID;
    }

    public async Task UsunLadowarkeAsync(int id)
    {
        sesja.Wymagaj(Uprawnienia.Usuwanie, "usuwanie ładowarki");
        await using var db = fabryka.Utworz();
        var l = await db.Ladowarki.AsTracking().FirstOrDefaultAsync(x => x.LadowarkaID == id)
                ?? throw new RegulaException("Ładowarka została już usunięta.");
        var sesji = await db.Sesje.CountAsync(x => x.LadowarkaID == id);
        var awarii = await db.Awarie.CountAsync(x => x.LadowarkaID == id);
        if (sesji > 0 || awarii > 0)
            throw new RegulaException($"Nie można usunąć ładowarki {l.NumerSeryjny}, bo ma historię: {sesji} sesji i {awarii} awarii. Dane historyczne muszą zostać w bazie.");
        db.Ladowarki.Remove(l);
        Dziennik.Dodaj(db, sesja, "usunięcie", "Ładowarka", id, $"Usunięto ładowarkę {l.NumerSeryjny}");
        await db.SaveChangesAsync();
    }
}
