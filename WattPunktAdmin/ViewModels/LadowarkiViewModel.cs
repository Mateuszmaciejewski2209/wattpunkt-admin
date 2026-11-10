using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

// Pozycja listy filtra: "(wszystkie)" ma Id = null
public record Opcja(int? Id, string Nazwa)
{
    public override string ToString() => Nazwa;
}

public class LadowarkiViewModel : ZakladkaViewModel
{
    private List<Stacja> stacje = [];
    private List<ModelLadowarki> modele = [];
    private List<Opcja> stacjeFiltra = [];
    private LadowarkaWiersz? wybrana;
    private LadowarkaFormularz formularz = new();
    private string szukaj = "", klasa = StacjeViewModel.Wszystkie;
    private Opcja? stacjaFiltra;

    public LadowarkiViewModel(Uslugi u) : base("Ładowarki", u)
    {
        SzukajCommand = new AsyncRelayCommand(() => WykonajAsync(WczytajListeAsync));
        NowaCommand = new RelayCommand(Nowa, () => MozeEdytowac);
        ZapiszCommand = new AsyncRelayCommand(ZapiszAsync, () => MozeEdytowac && !Formularz.HasErrors);
        AnulujCommand = new AsyncRelayCommand(AnulujAsync);
        UsunCommand = new AsyncRelayCommand(UsunAsync, () => MozeUsuwac && Wybrana is not null);
        ZglosAwarieCommand = new AsyncRelayCommand(
            () => U.Nawigacja.ZglosAwarieAsync(Wybrana!.StacjaID, Wybrana.LadowarkaID),
            () => Uprawnienia.ObslugaAwarii(U.Sesja.Rola) && Wybrana is { AwariaOtwarta: false });
    }

    public ICommand ZglosAwarieCommand { get; }

    public bool MozeEdytowac => Uprawnienia.EdycjaInfrastruktury(U.Sesja.Rola);
    public ObservableCollection<LadowarkaWiersz> Ladowarki { get; } = [];
    public List<Stacja> Stacje { get => stacje; private set => Ustaw(ref stacje, value); }
    public List<ModelLadowarki> Modele { get => modele; private set => Ustaw(ref modele, value); }
    public List<Opcja> StacjeFiltra { get => stacjeFiltra; private set => Ustaw(ref stacjeFiltra, value); }
    public List<string> KlasyMocy { get; } = [StacjeViewModel.Wszystkie, .. SlownikiService.KlasyMocy];

    public string Szukaj { get => szukaj; set => Ustaw(ref szukaj, value); }
    public string Klasa { get => klasa; set => Ustaw(ref klasa, value); }
    public Opcja? StacjaFiltra { get => stacjaFiltra; set => Ustaw(ref stacjaFiltra, value); }

    public LadowarkaFormularz Formularz { get => formularz; private set => Ustaw(ref formularz, value); }

    public LadowarkaWiersz? Wybrana
    {
        get => wybrana;
        set { if (Ustaw(ref wybrana, value) && value is not null) _ = WykonajAsync(() => WczytajFormularzAsync(value.LadowarkaID)); }
    }

    public ICommand SzukajCommand { get; }
    public ICommand NowaCommand { get; }
    public ICommand ZapiszCommand { get; }
    public ICommand AnulujCommand { get; }
    public ICommand UsunCommand { get; }

    protected override async Task ZaladujAsync()
    {
        Stacje = await U.Slowniki.StacjeAsync();
        Modele = await U.Slowniki.ModeleLadowarekAsync();
        StacjeFiltra = [new Opcja(null, StacjeViewModel.Wszystkie), .. Stacje.Select(s => new Opcja(s.StacjaID, $"{s.Kod} {s.Nazwa}"))];
        StacjaFiltra = StacjeFiltra[0];
        await WczytajListeAsync();
    }

    private async Task WczytajListeAsync()
    {
        var lista = await U.Stacje.LadowarkiAsync(new FiltrLadowarek
        {
            Szukaj = Szukaj, StacjaID = StacjaFiltra?.Id, KlasaMocy = Klasa == StacjeViewModel.Wszystkie ? null : Klasa
        });
        Ladowarki.Clear();
        foreach (var l in lista) Ladowarki.Add(l);
        U.Stan.Komunikat = $"Ładowarki: {lista.Count}, w tym z otwartą awarią: {lista.Count(l => l.AwariaOtwarta)}";
    }

    private async Task WczytajFormularzAsync(int id)
    {
        var l = await U.Stacje.LadowarkaAsync(id) ?? throw new RegulaException("Ładowarka została usunięta. Odśwież listę.");
        var f = new LadowarkaFormularz();
        f.Wczytaj(l, Stacje, Modele);
        Formularz = f;
    }

    private void Nowa()
    {
        Wybrana = null;
        Formularz = new LadowarkaFormularz { Stacja = Stacje.FirstOrDefault(s => s.StacjaID == StacjaFiltra?.Id) };
        Formularz.WyczyscBledy();
    }

    private async Task ZapiszAsync()
    {
        if (!Formularz.WalidujWszystko()) { U.Stan.Komunikat = "Formularz zawiera błędy."; return; }
        var dane = Formularz.DoEncji();
        var id = 0;
        if (!await WykonajAsync(async () => id = await U.Stacje.ZapiszLadowarkeAsync(dane), $"Zapisano ładowarkę {dane.NumerSeryjny}"))
            return;
        await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        // formularz wczytany od nowa ma aktualna wersje wiersza
        await WykonajAsync(() => WczytajFormularzAsync(id));
        wybrana = Ladowarki.FirstOrDefault(l => l.LadowarkaID == id);
        OnPropertyChanged(nameof(Wybrana));
    }

    private async Task AnulujAsync()
    {
        if (Wybrana is null) { Nowa(); return; }
        await WykonajAsync(() => WczytajFormularzAsync(Wybrana.LadowarkaID), "Przywrócono zapisane dane ładowarki");
    }

    private async Task UsunAsync()
    {
        if (Wybrana is not { } l) return;
        if (!U.Okna.Potwierdz($"Usunąć ładowarkę {l.NumerSeryjny}?")) return;
        if (await WykonajAsync(() => U.Stacje.UsunLadowarkeAsync(l.LadowarkaID), $"Usunięto ładowarkę {l.NumerSeryjny}"))
        {
            Nowa();
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        }
    }
}
