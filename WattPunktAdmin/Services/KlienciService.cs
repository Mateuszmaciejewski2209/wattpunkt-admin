using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface IKlienciService
{
    Task<List<KlientWiersz>> KlienciAsync(FiltrKlientow filtr);
    Task<Klient?> KlientAsync(int id);
    Task<int> ZapiszKlientaAsync(Klient dane);
    Task UsunKlientaAsync(int id);
    Task<List<PojazdWiersz>> PojazdyAsync(int klientId);
    Task<List<HistoriaPlanuWiersz>> HistoriaPlanowAsync(int klientId);
    Task DodajPojazdAsync(Pojazd dane);
    Task UsunPojazdAsync(int pojazdId);
}

public class KlienciService(IFabrykaKontekstu fabryka, SesjaUzytkownika sesja) : IKlienciService
{
    public const int LimitWierszy = 1000;

    public async Task<List<KlientWiersz>> KlienciAsync(FiltrKlientow f)
    {
        await using var db = fabryka.Utworz();
        var q = db.Klienci.AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Szukaj))
        {
            var s = f.Szukaj.Trim();
            q = int.TryParse(s, out var id)
                ? q.Where(x => x.KlientID == id)
                : q.Where(x => x.Nazwa.Contains(s) || x.Miejscowosc!.Nazwa.Contains(s));
        }
        if (!string.IsNullOrEmpty(f.TypKlienta)) q = q.Where(x => x.TypKlienta == f.TypKlienta);
        if (f.PlanID is int pid) q = q.Where(x => x.PlanID == pid);
        return await q.OrderBy(x => x.Nazwa).Take(LimitWierszy)
            .Select(x => new KlientWiersz(x.KlientID, x.TypKlienta, x.Nazwa, x.Miejscowosc!.Nazwa, x.Plan!.Nazwa,
                                         x.DataRejestracji, x.Pojazdy.Count))
            .ToListAsync();
    }

    public async Task<Klient?> KlientAsync(int id)
    {
        await using var db = fabryka.Utworz();
        return await db.Klienci.FirstOrDefaultAsync(x => x.KlientID == id);
    }

    public async Task<int> ZapiszKlientaAsync(Klient dane)
    {
        sesja.Wymagaj(Uprawnienia.ObslugaKlientow, "zapis klienta");
        if (dane.TypKlienta == "firmowy" && dane.PlanID != 3)
            throw new RegulaException("Klient firmowy musi mieć plan Flota.");
        if (dane.TypKlienta == "indywidualny" && dane.PlanID == 3)
            throw new RegulaException("Plan Flota jest tylko dla klientów firmowych.");

        await using var db = fabryka.Utworz();
        await using var tx = await db.Database.BeginTransactionAsync();
        var dzis = DateTime.Today;
        if (dane.KlientID == 0)
        {
            var nowy = new Klient { TypKlienta = dane.TypKlienta, Nazwa = dane.Nazwa.Trim(), MiejscowoscID = dane.MiejscowoscID,
                                    PlanID = dane.PlanID, DataRejestracji = dane.DataRejestracji };
            db.Klienci.Add(nowy);
            await db.SaveChangesAsync();
            db.HistoriaPlanow.Add(new HistoriaPlanu { KlientID = nowy.KlientID, PlanID = nowy.PlanID, OdDaty = nowy.DataRejestracji });
            Dziennik.Dodaj(db, sesja, "dodanie", "Klient", nowy.KlientID, $"Dodano klienta {nowy.KlientID}: {nowy.Nazwa}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return nowy.KlientID;
        }

        var k = await db.Klienci.AsTracking().FirstOrDefaultAsync(x => x.KlientID == dane.KlientID)
                ?? throw new RegulaException("Klient został w międzyczasie usunięty.");
        db.Entry(k).Property(x => x.Wersja).OriginalValue = dane.Wersja;
        var zmiany = Dziennik.Zmiany(("typ", k.TypKlienta, dane.TypKlienta), ("nazwa", k.Nazwa, dane.Nazwa.Trim()),
                                     ("miejscowość", k.MiejscowoscID, dane.MiejscowoscID), ("plan", k.PlanID, dane.PlanID));

        if (k.PlanID != dane.PlanID)
        {
            // zmiana planu: zamkniecie biezacego okresu w historii i nowy okres od dzisiaj
            var biezacy = await db.HistoriaPlanow.AsTracking()
                .Where(h => h.KlientID == k.KlientID && h.DoDaty == null).FirstOrDefaultAsync();
            if (biezacy is not null && biezacy.OdDaty == dzis)
                biezacy.PlanID = dane.PlanID;
            else
            {
                if (biezacy is not null) biezacy.DoDaty = dzis.AddDays(-1);
                db.HistoriaPlanow.Add(new HistoriaPlanu { KlientID = k.KlientID, PlanID = dane.PlanID, OdDaty = dzis });
            }
        }
        k.TypKlienta = dane.TypKlienta; k.Nazwa = dane.Nazwa.Trim(); k.MiejscowoscID = dane.MiejscowoscID; k.PlanID = dane.PlanID;
        Dziennik.Dodaj(db, sesja, "edycja", "Klient", k.KlientID, $"Zmieniono klienta {k.KlientID}: {zmiany}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return k.KlientID;
    }

    public async Task UsunKlientaAsync(int id)
    {
        sesja.Wymagaj(Uprawnienia.Usuwanie, "usuwanie klienta");
        await using var db = fabryka.Utworz();
        var k = await db.Klienci.AsTracking().FirstOrDefaultAsync(x => x.KlientID == id)
                ?? throw new RegulaException("Klient został już usunięty.");
        var sesji = await db.Sesje.CountAsync(s => s.KlientID == id);
        if (sesji > 0)
            throw new RegulaException($"Nie można usunąć klienta {k.Nazwa}, bo ma historię {sesji} sesji ładowania. Sesje są potrzebne do rozliczeń.");
        await using var tx = await db.Database.BeginTransactionAsync();
        db.HistoriaPlanow.RemoveRange(await db.HistoriaPlanow.AsTracking().Where(h => h.KlientID == id).ToListAsync());
        db.Pojazdy.RemoveRange(await db.Pojazdy.AsTracking().Where(p => p.KlientID == id).ToListAsync());
        db.Klienci.Remove(k);
        Dziennik.Dodaj(db, sesja, "usunięcie", "Klient", id, $"Usunięto klienta {id}: {k.Nazwa}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<PojazdWiersz>> PojazdyAsync(int klientId)
    {
        await using var db = fabryka.Utworz();
        return await db.Pojazdy.Where(p => p.KlientID == klientId).OrderBy(p => p.DataDodania)
            .Select(p => new PojazdWiersz(p.PojazdID, p.Model!.Marka + " " + p.Model.Model, p.RokProdukcji, p.DataDodania,
                                          db.Sesje.Count(s => s.PojazdID == p.PojazdID)))
            .ToListAsync();
    }

    public async Task<List<HistoriaPlanuWiersz>> HistoriaPlanowAsync(int klientId)
    {
        await using var db = fabryka.Utworz();
        return await db.HistoriaPlanow.Where(h => h.KlientID == klientId).OrderByDescending(h => h.OdDaty)
            .Select(h => new HistoriaPlanuWiersz(h.Plan!.Nazwa, h.OdDaty, h.DoDaty))
            .ToListAsync();
    }

    public async Task DodajPojazdAsync(Pojazd dane)
    {
        sesja.Wymagaj(Uprawnienia.ObslugaKlientow, "dodanie pojazdu");
        if (dane.RokProdukcji < 2000 || dane.RokProdukcji > DateTime.Today.Year + 1)
            throw new RegulaException($"Rok produkcji musi być z zakresu 2000 do {DateTime.Today.Year + 1}.");
        await using var db = fabryka.Utworz();
        var p = new Pojazd { KlientID = dane.KlientID, ModelPojazduID = dane.ModelPojazduID, RokProdukcji = dane.RokProdukcji, DataDodania = DateTime.Today };
        db.Pojazdy.Add(p);
        await db.SaveChangesAsync();
        var model = await db.ModelePojazdow.FirstAsync(m => m.ModelPojazduID == p.ModelPojazduID);
        Dziennik.Dodaj(db, sesja, "dodanie", "Pojazd", p.PojazdID, $"Dodano pojazd {model} ({p.RokProdukcji}) klientowi {p.KlientID}");
        await db.SaveChangesAsync();
    }

    public async Task UsunPojazdAsync(int pojazdId)
    {
        sesja.Wymagaj(Uprawnienia.Usuwanie, "usuwanie pojazdu");
        await using var db = fabryka.Utworz();
        var p = await db.Pojazdy.AsTracking().FirstOrDefaultAsync(x => x.PojazdID == pojazdId)
                ?? throw new RegulaException("Pojazd został już usunięty.");
        if (await db.Sesje.AnyAsync(s => s.PojazdID == pojazdId))
            throw new RegulaException("Nie można usunąć pojazdu, który ma zapisane sesje ładowania.");
        db.Pojazdy.Remove(p);
        Dziennik.Dodaj(db, sesja, "usunięcie", "Pojazd", pojazdId, $"Usunięto pojazd {pojazdId} klienta {p.KlientID}");
        await db.SaveChangesAsync();
    }
}
