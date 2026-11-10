using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.ViewModels;

public class AwarieViewModel : ZakladkaViewModel
{
    private List<Stacja> stacje = [];
    private List<Opcja> stacjeFiltra = [], kategorieFiltra = [];
    private List<KategoriaAwarii> kategorie = [];
    private List<Ladowarka> ladowarkiStacji = [];
    private AwariaWiersz? wybrana;
    private bool tylkoOtwarte = true;
    private Opcja? stacjaFiltra, kategoriaFiltra;
    private DateTime? od, @do;
    private Task ladowanieLadowarek = Task.CompletedTask;

    public AwarieViewModel(Uslugi u) : base("Awarie", u)
    {
        SzukajCommand = new AsyncRelayCommand(() => WykonajAsync(WczytajListeAsync));
        ZglosCommand = new AsyncRelayCommand(ZglosAsync, () => !Formularz.HasErrors);
        ZamknijCommand = new AsyncRelayCommand(ZamknijAsync, () => Wybrana is { Otwarta: true });
        UsunCommand = new AsyncRelayCommand(UsunAsync, () => MozeUsuwac && Wybrana is not null);
        Formularz.PropertyChanged += ZmianaFormularza;
    }

    public AwariaFormularz Formularz { get; } = new();
    public ObservableCollection<AwariaWiersz> Awarie { get; } = [];
    public List<Stacja> Stacje { get => stacje; private set => Ustaw(ref stacje, value); }
    public List<KategoriaAwarii> Kategorie { get => kategorie; private set => Ustaw(ref kategorie, value); }
    public List<Ladowarka> LadowarkiStacji { get => ladowarkiStacji; private set => Ustaw(ref ladowarkiStacji, value); }
    public List<Opcja> StacjeFiltra { get => stacjeFiltra; private set => Ustaw(ref stacjeFiltra, value); }
    public List<Opcja> KategorieFiltra { get => kategorieFiltra; private set => Ustaw(ref kategorieFiltra, value); }

    public bool TylkoOtwarte { get => tylkoOtwarte; set { if (Ustaw(ref tylkoOtwarte, value)) SzukajCommand.Execute(null); } }
    public Opcja? StacjaFiltra { get => stacjaFiltra; set => Ustaw(ref stacjaFiltra, value); }
    public Opcja? KategoriaFiltra { get => kategoriaFiltra; set => Ustaw(ref kategoriaFiltra, value); }
    public DateTime? Od { get => od; set => Ustaw(ref od, value); }
    public DateTime? Do { get => @do; set => Ustaw(ref @do, value); }

    public AwariaWiersz? Wybrana { get => wybrana; set => Ustaw(ref wybrana, value); }

    public ICommand SzukajCommand { get; }
    public ICommand ZglosCommand { get; }
    public ICommand ZamknijCommand { get; }
    public ICommand UsunCommand { get; }

    protected override async Task ZaladujAsync()
    {
        Stacje = await U.Slowniki.StacjeAsync();
        Kategorie = await U.Slowniki.KategorieAwariiAsync();
        StacjeFiltra = [new Opcja(null, StacjeViewModel.Wszystkie), .. Stacje.Select(s => new Opcja(s.StacjaID, $"{s.Kod} {s.Nazwa}"))];
        KategorieFiltra = [new Opcja(null, StacjeViewModel.Wszystkie), .. Kategorie.Select(k => new Opcja(k.KategoriaID, k.Nazwa))];
        StacjaFiltra = StacjeFiltra[0];
        KategoriaFiltra = KategorieFiltra[0];
        await WczytajListeAsync();
    }

    private async Task WczytajListeAsync()
    {
        var lista = await U.Awarie.AwarieAsync(new Models.FiltrAwarii
        {
            TylkoOtwarte = TylkoOtwarte, StacjaID = StacjaFiltra?.Id, KategoriaID = KategoriaFiltra?.Id, Od = Od, Do = Do
        });
        Awarie.Clear();
        foreach (var a in lista) Awarie.Add(a);
        U.Stan.Komunikat = TylkoOtwarte ? $"Otwarte awarie: {lista.Count}" : $"Awarie: {lista.Count}, otwarte: {lista.Count(a => a.Otwarta)}";
    }

    // wybor stacji w formularzu zaweza liste ladowarek (kaskadowe listy wyboru)
    private async void ZmianaFormularza(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AwariaFormularz.Stacja)) return;
        Formularz.Ladowarka = null;
        if (Formularz.Stacja is not { } s) { LadowarkiStacji = []; return; }
        await (ladowanieLadowarek = WykonajAsync(async () => LadowarkiStacji = await U.Slowniki.LadowarkiStacjiAsync(s.StacjaID),
                                                 $"Ładowarki stacji {s.Kod}: wybierz ładowarkę z awarią"));
    }

    // wywolywane po kliknieciu "Zglos awarie" w zakladce Stacje albo Ladowarki
    public async Task PrzygotujZgloszenieAsync(int stacjaId, int ladowarkaId)
    {
        await ZaladujJesliTrzebaAsync();
        if (Stacje.FirstOrDefault(s => s.StacjaID == stacjaId) is not { } stacja) return;
        Formularz.Wyczysc();
        Formularz.Stacja = stacja;
        await ladowanieLadowarek;
        Formularz.Ladowarka = LadowarkiStacji.FirstOrDefault(l => l.LadowarkaID == ladowarkaId);
        U.Stan.Komunikat = $"Zgłoszenie awarii ładowarki {Formularz.Ladowarka}: wybierz kategorię i opisz usterkę";
    }

    private async Task ZglosAsync()
    {
        if (!Formularz.WalidujWszystko()) { U.Stan.Komunikat = "Formularz zgłoszenia zawiera błędy."; return; }
        var dane = Formularz.DoEncji();
        if (await WykonajAsync(() => U.Awarie.ZglosAsync(dane), $"Zgłoszono awarię ładowarki {Formularz.Ladowarka}"))
        {
            Formularz.Wyczysc();
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        }
    }

    private async Task ZamknijAsync()
    {
        if (Wybrana is not { } a) return;
        if (!U.Okna.Potwierdz($"Zamknąć awarię ładowarki {a.NumerSeryjny} z datą naprawy {DateTime.Now:dd.MM.yyyy HH:mm}?")) return;
        if (await WykonajAsync(() => U.Awarie.ZamknijAsync(a.AwariaID, DateTime.Now), $"Zamknięto awarię ładowarki {a.NumerSeryjny}"))
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
    }

    private async Task UsunAsync()
    {
        if (Wybrana is not { } a) return;
        if (!U.Okna.Potwierdz($"Usunąć wpis awarii ładowarki {a.NumerSeryjny} z {a.Poczatek:dd.MM.yyyy}?")) return;
        if (await WykonajAsync(() => U.Awarie.UsunAsync(a.AwariaID), "Usunięto wpis awarii"))
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
    }
}
