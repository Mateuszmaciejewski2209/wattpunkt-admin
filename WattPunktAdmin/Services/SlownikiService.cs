using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Data;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Services;

public interface ISlownikiService
{
    Task<List<Miejscowosc>> MiejscowosciAsync();
    Task<List<ModelLadowarki>> ModeleLadowarekAsync();
    Task<List<ModelPojazdu>> ModelePojazdowAsync();
    Task<List<PlanTaryfowy>> PlanyAsync();
    Task<List<KategoriaAwarii>> KategorieAwariiAsync();
    Task<List<Stacja>> StacjeAsync();
    Task<List<Ladowarka>> LadowarkiStacjiAsync(int stacjaId);
}

public class SlownikiService(IFabrykaKontekstu fabryka) : ISlownikiService
{
    public static readonly IReadOnlyList<string> TypyLokalizacji =
        ["osiedle", "biurowiec", "centrum handlowe", "parking miejski", "hotel", "MOP"];
    public static readonly IReadOnlyList<string> KlasyMocy = ["AC", "DC", "HPC"];
    public static readonly IReadOnlyList<string> TypyKlienta = ["indywidualny", "firmowy"];
    public static readonly IReadOnlyList<string> StatusySesji = ["zakończona", "przerwana przez użytkownika", "przerwana (awaria)"];
    public static readonly IReadOnlyList<string> OperacjeDziennika = ["dodanie", "edycja", "usunięcie", "logowanie", "konto"];

    public async Task<List<Miejscowosc>> MiejscowosciAsync()
    {
        await using var db = fabryka.Utworz();
        return await db.Miejscowosci.OrderBy(m => m.Nazwa).ToListAsync();
    }

    public async Task<List<ModelLadowarki>> ModeleLadowarekAsync()
    {
        await using var db = fabryka.Utworz();
        return await db.ModeleLadowarek.OrderBy(m => m.MocKW).ThenBy(m => m.Producent).ToListAsync();
    }

    public async Task<List<ModelPojazdu>> ModelePojazdowAsync()
    {
        await using var db = fabryka.Utworz();
        return await db.ModelePojazdow.OrderBy(m => m.Marka).ThenBy(m => m.Model).ToListAsync();
    }

    public async Task<List<PlanTaryfowy>> PlanyAsync()
    {
        await using var db = fabryka.Utworz();
        return await db.PlanyTaryfowe.OrderBy(p => p.PlanID).ToListAsync();
    }

    public async Task<List<KategoriaAwarii>> KategorieAwariiAsync()
    {
        await using var db = fabryka.Utworz();
        return await db.KategorieAwarii.OrderBy(k => k.Nazwa).ToListAsync();
    }

    public async Task<List<Stacja>> StacjeAsync()
    {
        await using var db = fabryka.Utworz();
        return await db.Stacje.OrderBy(s => s.Kod).ToListAsync();
    }

    public async Task<List<Ladowarka>> LadowarkiStacjiAsync(int stacjaId)
    {
        await using var db = fabryka.Utworz();
        return await db.Ladowarki.Where(l => l.StacjaID == stacjaId).OrderBy(l => l.NumerSeryjny).ToListAsync();
    }
}

// Wpis do dziennika zmian w tym samym kontekscie (i transakcji) co zmiana danych
internal static class Dziennik
{
    public static void Dodaj(WattPunktContext db, SesjaUzytkownika sesja, string operacja, string obiekt, int? id, string opis) =>
        db.DziennikZmian.Add(new WpisDziennika
        {
            PracownikID = sesja.PracownikID, Data = DateTime.Now, Operacja = operacja, Obiekt = obiekt,
            ObiektID = id, Opis = opis.Length > 400 ? opis[..400] : opis
        });

    // lista zmienionych pol do opisu w dzienniku, np. "nazwa, typ lokalizacji"
    public static string Zmiany(params (string Pole, object? Stare, object? Nowe)[] pola)
    {
        var zmienione = pola.Where(p => !Equals(p.Stare, p.Nowe)).Select(p => p.Pole).ToList();
        return zmienione.Count == 0 ? "bez zmian" : string.Join(", ", zmienione);
    }
}
