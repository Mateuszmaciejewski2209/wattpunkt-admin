using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface IPanelService
{
    Task<Podsumowanie> PodsumowanieAsync();
}

public class PanelService(IFabrykaKontekstu fabryka) : IPanelService
{
    public async Task<Podsumowanie> PodsumowanieAsync()
    {
        await using var db = fabryka.Utworz();
        var stacje = await db.Stacje.CountAsync();
        var ladowarki = await db.Ladowarki.CountAsync();
        var klienci = await db.Klienci.CountAsync();
        var otwarte = await db.Awarie.CountAsync(a => a.Koniec == null);

        // ostatni pelny miesiac, w ktorym sa dane o sesjach
        var ostatnia = await db.Sesje.MaxAsync(s => (DateTime?)s.Start) ?? DateTime.Today;
        var od = new DateTime(ostatnia.Year, ostatnia.Month, 1);
        var @do = od.AddMonths(1);
        var miesiac = db.Sesje.Where(s => s.Start >= od && s.Start < @do);
        var sesji = await miesiac.CountAsync();
        var energia = await miesiac.SumAsync(s => (decimal?)s.EnergiaKWh) ?? 0;
        var przychod = await miesiac.SumAsync(s => (decimal?)s.KwotaRazem) ?? 0;

        var ranking = await miesiac.GroupBy(s => new { s.Ladowarka!.Stacja!.Kod, s.Ladowarka.Stacja.Nazwa })
            .Select(g => new { g.Key.Kod, g.Key.Nazwa, Sesje = g.Count(), Przychod = g.Sum(s => s.KwotaRazem) })
            .OrderByDescending(x => x.Przychod).Take(8).ToListAsync();
        var najlepsze = ranking.Select(r => new StacjaRanking($"{r.Kod} {r.Nazwa}", r.Sesje, r.Przychod,
                                                               przychod == 0 ? 0 : (double)(r.Przychod / przychod) * 100)).ToList();

        var awarie = await db.Awarie.OrderByDescending(a => a.Poczatek).Take(5)
            .Select(a => new AwariaWiersz(a.AwariaID, a.Poczatek, a.Koniec, a.Ladowarka!.Stacja!.Kod + " " + a.Ladowarka.Stacja.Nazwa,
                                          a.Ladowarka.NumerSeryjny, a.Kategoria!.Nazwa, a.Opis))
            .ToListAsync();

        var nazwaMiesiaca = od.ToString("MMMM yyyy", new CultureInfo("pl-PL"));
        return new Podsumowanie(stacje, ladowarki, klienci, otwarte, nazwaMiesiaca, sesji, energia, przychod, najlepsze, awarie);
    }
}
