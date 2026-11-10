using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

public class StacjeViewModel : ZakladkaViewModel
{
    public const string Wszystkie = "(wszystkie)";
    private List<Miejscowosc> miejscowosci = [];
    private StacjaWiersz? wybrana;
    private StacjaFormularz formularz = new();
    private string szukaj = "";
    private string wojewodztwo = Wszystkie, typ = Wszystkie;

    public StacjeViewModel(Uslugi u) : base("Stacje", u)
    {
        SzukajCommand = new AsyncRelayCommand(() => WykonajAsync(WczytajListeAsync));
        WyczyscFiltrCommand = new AsyncRelayCommand(async () => { szukaj = ""; wojewodztwo = typ = Wszystkie; OdswiezFiltr(); await WykonajAsync(WczytajListeAsync); });
        NowaCommand = new RelayCommand(Nowa, () => MozeEdytowac);
        ZapiszCommand = new AsyncRelayCommand(ZapiszAsync, () => MozeEdytowac && !Formularz.HasErrors);
        AnulujCommand = new AsyncRelayCommand(AnulujAsync);
        UsunCommand = new AsyncRelayCommand(UsunAsync, () => MozeUsuwac && Wybrana is not null);
        // awarie zglaszaja obie role; ladowarka moze miec tylko jedna otwarta awarie
        ZglosAwarieCommand = new AsyncRelayCommand(
            () => U.Nawigacja.ZglosAwarieAsync(WybranaLadowarkaStacji!.StacjaID, WybranaLadowarkaStacji.LadowarkaID),
            () => Uprawnienia.ObslugaAwarii(U.Sesja.Rola) && WybranaLadowarkaStacji is { AwariaOtwarta: false });
    }

    private LadowarkaWiersz? wybranaLadowarkaStacji;
    public LadowarkaWiersz? WybranaLadowarkaStacji { get => wybranaLadowarkaStacji; set => Ustaw(ref wybranaLadowarkaStacji, value); }
    public ICommand ZglosAwarieCommand { get; }

    public bool MozeEdytowac => Uprawnienia.EdycjaInfrastruktury(U.Sesja.Rola);

    public ObservableCollection<StacjaWiersz> Stacje { get; } = [];
    public ObservableCollection<LadowarkaWiersz> LadowarkiStacji { get; } = [];
    public List<Miejscowosc> Miejscowosci { get => miejscowosci; private set => Ustaw(ref miejscowosci, value); }
    public List<string> Wojewodztwa { get; private set; } = [Wszystkie];
    public IReadOnlyList<string> TypyLokalizacji => SlownikiService.TypyLokalizacji;
    public List<string> TypyFiltra { get; } = [Wszystkie, .. SlownikiService.TypyLokalizacji];

    public string Szukaj { get => szukaj; set => Ustaw(ref szukaj, value); }
    public string Wojewodztwo { get => wojewodztwo; set => Ustaw(ref wojewodztwo, value); }
    public string Typ { get => typ; set => Ustaw(ref typ, value); }

    public StacjaFormularz Formularz { get => formularz; private set => Ustaw(ref formularz, value); }

    public StacjaWiersz? Wybrana
    {
        get => wybrana;
        set { if (Ustaw(ref wybrana, value) && value is not null) _ = WykonajAsync(() => WczytajFormularzAsync(value.StacjaID)); }
    }

    public ICommand SzukajCommand { get; }
    public ICommand WyczyscFiltrCommand { get; }
    public ICommand NowaCommand { get; }
    public ICommand ZapiszCommand { get; }
    public ICommand AnulujCommand { get; }
    public ICommand UsunCommand { get; }

    protected override async Task ZaladujAsync()
    {
        Miejscowosci = await U.Slowniki.MiejscowosciAsync();
        Wojewodztwa = [Wszystkie, .. Miejscowosci.Select(m => m.Wojewodztwo).Distinct().OrderBy(w => w)];
        OnPropertyChanged(nameof(Wojewodztwa));
        await WczytajListeAsync();
    }

    private void OdswiezFiltr()
    {
        OnPropertyChanged(nameof(Szukaj)); OnPropertyChanged(nameof(Wojewodztwo)); OnPropertyChanged(nameof(Typ));
    }

    private async Task WczytajListeAsync()
    {
        var lista = await U.Stacje.StacjeAsync(new FiltrStacji
        {
            Szukaj = Szukaj, Wojewodztwo = Wojewodztwo == Wszystkie ? null : Wojewodztwo, TypLokalizacji = Typ == Wszystkie ? null : Typ
        });
        Stacje.Clear();
        foreach (var s in lista) Stacje.Add(s);
        U.Stan.Komunikat = $"Znaleziono stacji: {lista.Count}";
    }

    private async Task WczytajFormularzAsync(int stacjaId)
    {
        var s = await U.Stacje.StacjaAsync(stacjaId) ?? throw new RegulaException("Stacja została usunięta. Odśwież listę.");
        var f = new StacjaFormularz();
        f.Wczytaj(s, Miejscowosci);
        Formularz = f;
        var ladowarki = await U.Stacje.LadowarkiAsync(new FiltrLadowarek { StacjaID = stacjaId });
        LadowarkiStacji.Clear();
        foreach (var l in ladowarki) LadowarkiStacji.Add(l);
    }

    private void Nowa()
    {
        Wybrana = null;
        Formularz = new StacjaFormularz();
        LadowarkiStacji.Clear();
    }

    private async Task ZapiszAsync()
    {
        if (!Formularz.WalidujWszystko()) { U.Stan.Komunikat = "Formularz zawiera błędy."; return; }
        var dane = Formularz.DoEncji();
        var id = 0;
        var ok = await WykonajAsync(async () => id = await U.Stacje.ZapiszStacjeAsync(dane),
                                    dane.StacjaID == 0 ? $"Dodano stację {dane.Kod}" : $"Zapisano stację {dane.Kod}");
        if (!ok) return;
        await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        // formularz wczytany od nowa ma aktualna wersje wiersza, wiec kolejny zapis nie zglosi falszywego konfliktu
        await WykonajAsync(() => WczytajFormularzAsync(id));
        wybrana = Stacje.FirstOrDefault(s => s.StacjaID == id);
        OnPropertyChanged(nameof(Wybrana));
    }

    private async Task AnulujAsync()
    {
        if (Wybrana is null) { Nowa(); return; }
        await WykonajAsync(() => WczytajFormularzAsync(Wybrana.StacjaID), "Przywrócono zapisane dane stacji");
    }

    private async Task UsunAsync()
    {
        if (Wybrana is not { } s) return;
        if (!U.Okna.Potwierdz($"Usunąć stację {s.Kod} {s.Nazwa}?")) return;
        if (await WykonajAsync(() => U.Stacje.UsunStacjeAsync(s.StacjaID), $"Usunięto stację {s.Kod}"))
        {
            Nowa();
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        }
    }
}
