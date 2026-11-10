using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

public class KlienciViewModel : ZakladkaViewModel
{
    private List<Miejscowosc> miejscowosci = [];
    private List<PlanTaryfowy> plany = [];
    private List<ModelPojazdu> modelePojazdow = [];
    private List<Opcja> planyFiltra = [];
    private KlientWiersz? wybrany;
    private PojazdWiersz? wybranyPojazd;
    private KlientFormularz formularz = new();
    private string szukaj = "", typ = StacjeViewModel.Wszystkie, informacjaOLiscie = "";
    private Opcja? planFiltra;

    public KlienciViewModel(Uslugi u) : base("Klienci", u)
    {
        SzukajCommand = new AsyncRelayCommand(() => WykonajAsync(WczytajListeAsync));
        NowyCommand = new RelayCommand(Nowy);
        ZapiszCommand = new AsyncRelayCommand(ZapiszAsync, () => !Formularz.HasErrors);
        AnulujCommand = new AsyncRelayCommand(AnulujAsync);
        UsunCommand = new AsyncRelayCommand(UsunAsync, () => MozeUsuwac && Wybrany is not null);
        DodajPojazdCommand = new AsyncRelayCommand(DodajPojazdAsync, () => Wybrany is not null && !Pojazd.HasErrors);
        UsunPojazdCommand = new AsyncRelayCommand(UsunPojazdAsync, () => MozeUsuwac && WybranyPojazd is not null);
    }

    public ObservableCollection<KlientWiersz> Klienci { get; } = [];
    public ObservableCollection<PojazdWiersz> Pojazdy { get; } = [];
    public ObservableCollection<HistoriaPlanuWiersz> HistoriaPlanow { get; } = [];
    public List<Miejscowosc> Miejscowosci { get => miejscowosci; private set => Ustaw(ref miejscowosci, value); }
    public List<PlanTaryfowy> Plany { get => plany; private set => Ustaw(ref plany, value); }
    public List<ModelPojazdu> ModelePojazdow { get => modelePojazdow; private set => Ustaw(ref modelePojazdow, value); }
    public List<Opcja> PlanyFiltra { get => planyFiltra; private set => Ustaw(ref planyFiltra, value); }
    public List<string> TypyFiltra { get; } = [StacjeViewModel.Wszystkie, .. SlownikiService.TypyKlienta];

    public string Szukaj { get => szukaj; set => Ustaw(ref szukaj, value); }
    public string Typ { get => typ; set => Ustaw(ref typ, value); }
    public Opcja? PlanFiltra { get => planFiltra; set => Ustaw(ref planFiltra, value); }
    public string InformacjaOLiscie { get => informacjaOLiscie; private set => Ustaw(ref informacjaOLiscie, value); }

    public KlientFormularz Formularz { get => formularz; private set => Ustaw(ref formularz, value); }
    public PojazdFormularz Pojazd { get; } = new();

    public KlientWiersz? Wybrany
    {
        get => wybrany;
        set { if (Ustaw(ref wybrany, value) && value is not null) _ = WykonajAsync(() => WczytajFormularzAsync(value.KlientID)); }
    }

    public PojazdWiersz? WybranyPojazd { get => wybranyPojazd; set => Ustaw(ref wybranyPojazd, value); }

    public ICommand SzukajCommand { get; }
    public ICommand NowyCommand { get; }
    public ICommand ZapiszCommand { get; }
    public ICommand AnulujCommand { get; }
    public ICommand UsunCommand { get; }
    public ICommand DodajPojazdCommand { get; }
    public ICommand UsunPojazdCommand { get; }

    protected override async Task ZaladujAsync()
    {
        Miejscowosci = await U.Slowniki.MiejscowosciAsync();
        Plany = await U.Slowniki.PlanyAsync();
        ModelePojazdow = await U.Slowniki.ModelePojazdowAsync();
        PlanyFiltra = [new Opcja(null, StacjeViewModel.Wszystkie), .. Plany.Select(p => new Opcja(p.PlanID, p.Nazwa))];
        PlanFiltra = PlanyFiltra[0];
        Nowy();
        await WczytajListeAsync();
    }

    private async Task WczytajListeAsync()
    {
        var lista = await U.Klienci.KlienciAsync(new FiltrKlientow
        {
            Szukaj = Szukaj, TypKlienta = Typ == StacjeViewModel.Wszystkie ? null : Typ, PlanID = PlanFiltra?.Id
        });
        Klienci.Clear();
        foreach (var k in lista) Klienci.Add(k);
        InformacjaOLiscie = lista.Count >= KlienciService.LimitWierszy
            ? $"Pokazano pierwszych {KlienciService.LimitWierszy} klientów. Zawęź wyszukiwanie, aby zobaczyć pozostałych."
            : $"Znaleziono klientów: {lista.Count}";
        U.Stan.Komunikat = InformacjaOLiscie;
    }

    private async Task WczytajFormularzAsync(int id)
    {
        var k = await U.Klienci.KlientAsync(id) ?? throw new RegulaException("Klient został usunięty. Odśwież listę.");
        var f = new KlientFormularz();
        f.Wczytaj(k, Miejscowosci, Plany);
        Formularz = f;
        await WczytajSzczegolyAsync(id);
    }

    private async Task WczytajSzczegolyAsync(int id)
    {
        var pojazdy = await U.Klienci.PojazdyAsync(id);
        Pojazdy.Clear();
        foreach (var p in pojazdy) Pojazdy.Add(p);
        var historia = await U.Klienci.HistoriaPlanowAsync(id);
        HistoriaPlanow.Clear();
        foreach (var h in historia) HistoriaPlanow.Add(h);
    }

    private void Nowy()
    {
        Wybrany = null;
        Formularz = new KlientFormularz { Plan = Plany.FirstOrDefault() };
        Formularz.WyczyscBledy();
        Pojazdy.Clear();
        HistoriaPlanow.Clear();
    }

    private async Task ZapiszAsync()
    {
        if (!Formularz.WalidujWszystko()) { U.Stan.Komunikat = "Formularz zawiera błędy."; return; }
        var dane = Formularz.DoEncji();
        var id = 0;
        if (!await WykonajAsync(async () => id = await U.Klienci.ZapiszKlientaAsync(dane), $"Zapisano klienta {dane.Nazwa}"))
            return;
        Szukaj = id.ToString();
        await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        // formularz, pojazdy i historia planow wczytane od nowa (aktualna wersja wiersza)
        await WykonajAsync(() => WczytajFormularzAsync(id));
        wybrany = Klienci.FirstOrDefault(k => k.KlientID == id);
        OnPropertyChanged(nameof(Wybrany));
    }

    private async Task AnulujAsync()
    {
        if (Wybrany is null) { Nowy(); return; }
        await WykonajAsync(() => WczytajFormularzAsync(Wybrany.KlientID), "Przywrócono zapisane dane klienta");
    }

    private async Task UsunAsync()
    {
        if (Wybrany is not { } k) return;
        if (!U.Okna.Potwierdz($"Usunąć klienta {k.Nazwa} razem z jego pojazdami?")) return;
        if (await WykonajAsync(() => U.Klienci.UsunKlientaAsync(k.KlientID), $"Usunięto klienta {k.Nazwa}"))
        {
            Nowy();
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        }
    }

    private async Task DodajPojazdAsync()
    {
        if (Wybrany is not { } k || !Pojazd.WalidujWszystko()) return;
        var p = new Pojazd { KlientID = k.KlientID, ModelPojazduID = Pojazd.Model!.ModelPojazduID, RokProdukcji = (short)Pojazd.RokProdukcji!.Value };
        if (await WykonajAsync(() => U.Klienci.DodajPojazdAsync(p), $"Dodano pojazd {Pojazd.Model} klientowi {k.Nazwa}"))
        {
            Pojazd.Wyczysc();
            await WykonajAsync(() => WczytajSzczegolyAsync(k.KlientID), U.Stan.Komunikat);
        }
    }

    private async Task UsunPojazdAsync()
    {
        if (Wybrany is not { } k || WybranyPojazd is not { } p) return;
        if (!U.Okna.Potwierdz($"Usunąć pojazd {p.Model} ({p.RokProdukcji})?")) return;
        if (await WykonajAsync(() => U.Klienci.UsunPojazdAsync(p.PojazdID), "Usunięto pojazd"))
            await WykonajAsync(() => WczytajSzczegolyAsync(k.KlientID), U.Stan.Komunikat);
    }
}
