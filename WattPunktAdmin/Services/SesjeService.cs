using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface ISesjeService
{
    Task<StronaWynikow<SesjaWiersz>> SesjeAsync(FiltrSesji filtr, int strona, int rozmiarStrony);
}

public class SesjeService(IFabrykaKontekstu fabryka) : ISesjeService
{
    // Filtrowanie, liczenie i stronicowanie w bazie: do programu trafia tylko jedna strona wynikow
    public async Task<StronaWynikow<SesjaWiersz>> SesjeAsync(FiltrSesji f, int strona, int rozmiarStrony)
    {
        await using var db = fabryka.Utworz();
        var q = db.Sesje.AsQueryable();
        if (f.Od is DateTime od) q = q.Where(s => s.Start >= od);
        if (f.Do is DateTime @do) q = q.Where(s => s.Start < @do.AddDays(1));
        if (f.StacjaID is int sid) q = q.Where(s => s.Ladowarka!.StacjaID == sid);
        if (f.KlasaMocy == "AC") q = q.Where(s => s.Ladowarka!.Model!.TypPradu == "AC");
        if (f.KlasaMocy == "DC") q = q.Where(s => s.Ladowarka!.Model!.TypPradu == "DC" && s.Ladowarka.Model.MocKW < 150);
        if (f.KlasaMocy == "HPC") q = q.Where(s => s.Ladowarka!.Model!.MocKW >= 150);
        if (!string.IsNullOrEmpty(f.Status)) q = q.Where(s => s.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Szukaj))
        {
            var t = f.Szukaj.Trim();
            q = long.TryParse(t, out var nr)
                ? q.Where(s => s.SesjaID == nr || s.KlientID == nr)
                : q.Where(s => s.Klient!.Nazwa.Contains(t) || s.Ladowarka!.NumerSeryjny.Contains(t));
        }

        var podsumowanie = await q.GroupBy(_ => 1)
            .Select(g => new { Liczba = g.Count(), Energia = g.Sum(s => s.EnergiaKWh), Przychod = g.Sum(s => s.KwotaRazem) })
            .FirstOrDefaultAsync();

        var wiersze = await q.OrderByDescending(s => s.Start).ThenByDescending(s => s.SesjaID)
            .Skip((strona - 1) * rozmiarStrony).Take(rozmiarStrony)
            .Select(s => new SesjaWiersz(
                s.SesjaID, s.Start, s.Ladowarka!.Stacja!.Kod + " " + s.Ladowarka.Stacja.Nazwa, s.Ladowarka.NumerSeryjny,
                s.Ladowarka.Model!.TypPradu == "AC" ? "AC" : s.Ladowarka.Model.MocKW >= 150 ? "HPC" : "DC",
                s.Klient != null ? s.Klient.Nazwa : "(bez konta)",
                s.EnergiaKWh, EF.Functions.DateDiffMinute(s.Start, s.KoniecLadowania), s.KwotaRazem, s.MetodaPlatnosci, s.Status))
            .ToListAsync();

        return new StronaWynikow<SesjaWiersz>(wiersze, podsumowanie?.Liczba ?? 0, podsumowanie?.Energia ?? 0, podsumowanie?.Przychod ?? 0);
    }
}
