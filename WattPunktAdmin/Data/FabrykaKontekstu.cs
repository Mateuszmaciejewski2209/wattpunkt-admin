using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace WattPunktAdmin.Data;

public interface IFabrykaKontekstu
{
    WattPunktContext Utworz();
}

// Krotko zyjacy kontekst na kazda operacje: w aplikacji desktopowej nie trzyma sie jednego kontekstu przez caly czas
public class FabrykaKontekstu(string polaczenie) : IFabrykaKontekstu
{
    private readonly DbContextOptions<WattPunktContext> opcje = new DbContextOptionsBuilder<WattPunktContext>()
        .UseSqlServer(polaczenie, sql => sql.CommandTimeout(30))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .Options;

    public WattPunktContext Utworz() => new(opcje);
}

public record Ustawienia(string PolaczenieZBaza)
{
    public static Ustawienia Wczytaj()
    {
        var plik = Path.Combine(AppContext.BaseDirectory, "ustawienia.json");
        if (!File.Exists(plik))
            throw new FileNotFoundException("Brak pliku ustawienia.json z danymi połączenia z bazą.", plik);
        return JsonSerializer.Deserialize<Ustawienia>(File.ReadAllText(plik))
               ?? throw new InvalidDataException("Plik ustawienia.json jest pusty.");
    }
}
