using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

public class PracownicyViewModel : ZakladkaViewModel
{
    private PracownikWiersz? wybrany;
    private PracownikFormularz formularz = new();

    public PracownicyViewModel(Uslugi u) : base("Pracownicy", u)
    {
        OdswiezCommand = new AsyncRelayCommand(() => WykonajAsync(WczytajListeAsync, "Odświeżono listę kont"));
        NowyCommand = new RelayCommand(Nowy);
        ZapiszCommand = new AsyncRelayCommand(ZapiszAsync, () => !Formularz.HasErrors);
        AnulujCommand = new AsyncRelayCommand(AnulujAsync);
        ResetujHasloCommand = new AsyncRelayCommand(ResetujHasloAsync, () => Wybrany is not null);
        OdblokujCommand = new AsyncRelayCommand(OdblokujAsync, () => Wybrany is { Zablokowany: true });
    }

    public ObservableCollection<PracownikWiersz> Pracownicy { get; } = [];
    public IReadOnlyList<Rola> Role { get; } = Enum.GetValues<Rola>();
    public PracownikFormularz Formularz { get => formularz; private set => Ustaw(ref formularz, value); }

    public PracownikWiersz? Wybrany
    {
        get => wybrany;
        set { if (Ustaw(ref wybrany, value) && value is not null) _ = WykonajAsync(() => WczytajFormularzAsync(value.PracownikID)); }
    }

    public ICommand OdswiezCommand { get; }
    public ICommand NowyCommand { get; }
    public ICommand ZapiszCommand { get; }
    public ICommand AnulujCommand { get; }
    public ICommand ResetujHasloCommand { get; }
    public ICommand OdblokujCommand { get; }

    protected override Task ZaladujAsync() => WczytajListeAsync();

    private async Task WczytajListeAsync()
    {
        var lista = await U.Pracownicy.PracownicyAsync();
        Pracownicy.Clear();
        foreach (var p in lista) Pracownicy.Add(p);
        U.Stan.Komunikat = $"Konta: {lista.Count}, aktywne: {lista.Count(p => p.Aktywny && !p.Zablokowany)}";
    }

    private async Task WczytajFormularzAsync(int id)
    {
        var p = await U.Pracownicy.PracownikAsync(id) ?? throw new RegulaException("Konto nie istnieje.");
        var f = new PracownikFormularz();
        f.Wczytaj(p);
        Formularz = f;
    }

    private void Nowy()
    {
        Wybrany = null;
        Formularz = new PracownikFormularz();
    }

    private async Task ZapiszAsync()
    {
        if (!Formularz.WalidujWszystko()) { U.Stan.Komunikat = "Formularz zawiera błędy."; return; }
        var dane = Formularz.DoEncji();
        string? haslo = null;
        if (!await WykonajAsync(async () => haslo = await U.Pracownicy.ZapiszAsync(dane), $"Zapisano konto {dane.Login}"))
            return;
        if (haslo is not null)
            U.Okna.Informacja($"Założono konto {dane.Login}.\n\nHasło tymczasowe: {haslo}\n\nPrzekaż je pracownikowi. Przy pierwszym logowaniu będzie musiał ustawić własne hasło.", "Nowe konto");
        await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        // formularz wczytany od nowa ma aktualna wersje wiersza
        wybrany = Pracownicy.FirstOrDefault(p => p.Login == dane.Login);
        OnPropertyChanged(nameof(Wybrany));
        if (wybrany is not null) await WykonajAsync(() => WczytajFormularzAsync(wybrany.PracownikID));
    }

    private async Task AnulujAsync()
    {
        if (Wybrany is null) { Nowy(); return; }
        await WykonajAsync(() => WczytajFormularzAsync(Wybrany.PracownikID), "Przywrócono zapisane dane konta");
    }

    private async Task ResetujHasloAsync()
    {
        if (Wybrany is not { } p) return;
        if (!U.Okna.Potwierdz($"Zresetować hasło konta {p.Login}? Pracownik dostanie hasło tymczasowe.")) return;
        string haslo = "";
        if (await WykonajAsync(async () => haslo = await U.Pracownicy.ResetujHasloAsync(p.PracownikID), $"Zresetowano hasło konta {p.Login}"))
        {
            U.Okna.Informacja($"Nowe hasło tymczasowe konta {p.Login}: {haslo}\n\nPrzy logowaniu pracownik będzie musiał je zmienić.", "Reset hasła");
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
        }
    }

    private async Task OdblokujAsync()
    {
        if (Wybrany is not { } p) return;
        if (await WykonajAsync(() => U.Pracownicy.OdblokujAsync(p.PracownikID), $"Odblokowano konto {p.Login}"))
            await WykonajAsync(WczytajListeAsync, U.Stan.Komunikat);
    }
}

public class DziennikViewModel : ZakladkaViewModel
{
    private List<Opcja> pracownicyFiltra = [];
    private Opcja? pracownikFiltra;
    private string operacja = StacjeViewModel.Wszystkie;
    private DateTime? od, @do;

    public DziennikViewModel(Uslugi u) : base("Dziennik zmian", u)
    {
        SzukajCommand = new AsyncRelayCommand(() => WykonajAsync(WczytajAsync));
    }

    public ObservableCollection<WpisDziennikaWiersz> Wpisy { get; } = [];
    public List<Opcja> PracownicyFiltra { get => pracownicyFiltra; private set => Ustaw(ref pracownicyFiltra, value); }
    public List<string> Operacje { get; } = [StacjeViewModel.Wszystkie, .. SlownikiService.OperacjeDziennika];
    public Opcja? PracownikFiltra { get => pracownikFiltra; set => Ustaw(ref pracownikFiltra, value); }
    public string Operacja { get => operacja; set => Ustaw(ref operacja, value); }
    public DateTime? Od { get => od; set => Ustaw(ref od, value); }
    public DateTime? Do { get => @do; set => Ustaw(ref @do, value); }
    public ICommand SzukajCommand { get; }

    protected override async Task ZaladujAsync()
    {
        var pracownicy = await U.Pracownicy.PracownicyAsync();
        PracownicyFiltra = [new Opcja(null, StacjeViewModel.Wszystkie), .. pracownicy.Select(p => new Opcja(p.PracownikID, $"{p.Login} ({p.ImieNazwisko})"))];
        PracownikFiltra = PracownicyFiltra[0];
        await WczytajAsync();
    }

    private async Task WczytajAsync()
    {
        var lista = await U.Pracownicy.DziennikAsync(new FiltrDziennika
        {
            PracownikID = PracownikFiltra?.Id, Operacja = Operacja == StacjeViewModel.Wszystkie ? null : Operacja, Od = Od, Do = Do
        });
        Wpisy.Clear();
        foreach (var w in lista) Wpisy.Add(w);
        U.Stan.Komunikat = $"Wpisy w dzienniku: {lista.Count}";
    }
}
