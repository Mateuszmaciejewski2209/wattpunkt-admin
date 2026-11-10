using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

public class SesjeViewModel : ZakladkaViewModel
{
    public const int RozmiarStrony = 100;
    private List<Opcja> stacjeFiltra = [];
    private Opcja? stacjaFiltra;
    private string klasa = StacjeViewModel.Wszystkie, status = StacjeViewModel.Wszystkie, szukaj = "";
    private DateTime? od, @do;
    private int strona = 1, liczbaWynikow;
    private decimal sumaEnergii, sumaPrzychodu;

    public SesjeViewModel(Uslugi u) : base("Sesje", u)
    {
        SzukajCommand = new AsyncRelayCommand(() => { strona = 1; return WykonajAsync(WczytajStroneAsync); });
        WyczyscFiltrCommand = new AsyncRelayCommand(WyczyscFiltrAsync);
        PoprzedniaCommand = new AsyncRelayCommand(() => { strona--; return WykonajAsync(WczytajStroneAsync); }, () => Strona > 1);
        NastepnaCommand = new AsyncRelayCommand(() => { strona++; return WykonajAsync(WczytajStroneAsync); }, () => Strona < LiczbaStron);
    }

    public ObservableCollection<SesjaWiersz> Sesje { get; } = [];
    public List<Opcja> StacjeFiltra { get => stacjeFiltra; private set => Ustaw(ref stacjeFiltra, value); }
    public List<string> KlasyMocy { get; } = [StacjeViewModel.Wszystkie, .. SlownikiService.KlasyMocy];
    public List<string> Statusy { get; } = [StacjeViewModel.Wszystkie, .. SlownikiService.StatusySesji];

    public Opcja? StacjaFiltra { get => stacjaFiltra; set => Ustaw(ref stacjaFiltra, value); }
    public string Klasa { get => klasa; set => Ustaw(ref klasa, value); }
    public string Status { get => status; set => Ustaw(ref status, value); }
    public string Szukaj { get => szukaj; set => Ustaw(ref szukaj, value); }
    public DateTime? Od { get => od; set => Ustaw(ref od, value); }
    public DateTime? Do { get => @do; set => Ustaw(ref @do, value); }

    public int Strona { get => strona; private set => Ustaw(ref strona, value); }
    public int LiczbaWynikow { get => liczbaWynikow; private set => Ustaw(ref liczbaWynikow, value); }
    public int LiczbaStron => Math.Max(1, (LiczbaWynikow + RozmiarStrony - 1) / RozmiarStrony);
    public decimal SumaEnergii { get => sumaEnergii; private set => Ustaw(ref sumaEnergii, value); }
    public decimal SumaPrzychodu { get => sumaPrzychodu; private set => Ustaw(ref sumaPrzychodu, value); }
    public string OpisStrony => $"Strona {Strona} z {LiczbaStron:N0}";

    public ICommand SzukajCommand { get; }
    public ICommand WyczyscFiltrCommand { get; }
    public ICommand PoprzedniaCommand { get; }
    public ICommand NastepnaCommand { get; }

    protected override async Task ZaladujAsync()
    {
        var stacje = await U.Slowniki.StacjeAsync();
        StacjeFiltra = [new Opcja(null, StacjeViewModel.Wszystkie), .. stacje.Select(s => new Opcja(s.StacjaID, $"{s.Kod} {s.Nazwa}"))];
        StacjaFiltra = StacjeFiltra[0];
        await WczytajStroneAsync();
    }

    private async Task WyczyscFiltrAsync()
    {
        StacjaFiltra = StacjeFiltra.FirstOrDefault();
        Klasa = Status = StacjeViewModel.Wszystkie;
        Szukaj = "";
        Od = Do = null;
        strona = 1;
        await WykonajAsync(WczytajStroneAsync);
    }

    private async Task WczytajStroneAsync()
    {
        var filtr = new FiltrSesji
        {
            Od = Od, Do = Do, StacjaID = StacjaFiltra?.Id, Szukaj = Szukaj,
            KlasaMocy = Klasa == StacjeViewModel.Wszystkie ? null : Klasa,
            Status = Status == StacjeViewModel.Wszystkie ? null : Status
        };
        var wynik = await U.Sesje.SesjeAsync(filtr, strona, RozmiarStrony);
        Sesje.Clear();
        foreach (var s in wynik.Wiersze) Sesje.Add(s);
        LiczbaWynikow = wynik.LiczbaWszystkich;
        SumaEnergii = wynik.SumaEnergii;
        SumaPrzychodu = wynik.SumaPrzychodu;
        Strona = strona;
        OnPropertyChanged(nameof(LiczbaStron));
        OnPropertyChanged(nameof(OpisStrony));
        U.Stan.Komunikat = $"Sesje spełniające warunki: {LiczbaWynikow:N0}";
    }
}
