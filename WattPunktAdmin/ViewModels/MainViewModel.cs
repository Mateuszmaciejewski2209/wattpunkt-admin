using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly Uslugi u;
    private ZakladkaViewModel? wybranaZakladka;

    public MainViewModel(Uslugi uslugi)
    {
        u = uslugi;
        Zakladki =
        [
            new PanelViewModel(u), new StacjeViewModel(u), new LadowarkiViewModel(u), new KlienciViewModel(u),
            new AwarieViewModel(u), new SesjeViewModel(u)
        ];
        // zakladki administracyjne widzi tylko administrator
        if (Uprawnienia.ZarzadzaniePracownikami(u.Sesja.Rola)) Zakladki.Add(new PracownicyViewModel(u));
        if (Uprawnienia.PrzegladanieDziennika(u.Sesja.Rola)) Zakladki.Add(new DziennikViewModel(u));

        // "Zglos awarie" w zakladkach Stacje i Ladowarki: przejscie do zakladki Awarie z wybrana ladowarka
        var awarie = Zakladki.OfType<AwarieViewModel>().First();
        u.Nawigacja.ZgloszenieAwarii += async (stacjaId, ladowarkaId) =>
        {
            WybranaZakladka = awarie;
            await awarie.PrzygotujZgloszenieAsync(stacjaId, ladowarkaId);
        };

        WylogujCommand = new RelayCommand(Wyloguj);
        ZakonczCommand = new RelayCommand(() => Zakonczono?.Invoke(this, EventArgs.Empty));
        ZmienHasloCommand = new RelayCommand(() =>
        {
            if (u.Okna.ZmienHaslo(u.Auth, wymuszona: false)) u.Stan.Komunikat = "Hasło zostało zmienione.";
        });
        OdswiezCommand = new AsyncRelayCommand(async () =>
        {
            if (WybranaZakladka is { } z) { z.OdswiezNaZadanie(); await z.ZaladujJesliTrzebaAsync(); }
        });
        OProgramieCommand = new RelayCommand(() => u.Okna.Informacja(
            "WattPunkt Admin 1.0\nAplikacja do obsługi sieci stacji ładowania samochodów elektrycznych.\n\nC#, .NET 10, WPF, MVVM, Entity Framework Core, SQL Server.",
            "O programie"));
        WybranaZakladka = Zakladki[0];
    }

    public ObservableCollection<ZakladkaViewModel> Zakladki { get; }
    public StanAplikacji Stan => u.Stan;
    public string Uzytkownik => $"{u.Sesja.Pracownik?.ImieNazwisko} ({u.Sesja.Pracownik?.Login})";
    public string RolaOpis => u.Sesja.Rola == Rola.Administrator ? "Administrator" : "Pracownik";
    public string Tytul => $"WattPunkt Admin: {Uzytkownik}";

    public ZakladkaViewModel? WybranaZakladka
    {
        get => wybranaZakladka;
        set { if (Ustaw(ref wybranaZakladka, value) && value is not null) _ = value.ZaladujJesliTrzebaAsync(); }
    }

    public ICommand WylogujCommand { get; }
    public ICommand ZakonczCommand { get; }
    public ICommand ZmienHasloCommand { get; }
    public ICommand OdswiezCommand { get; }
    public ICommand OProgramieCommand { get; }

    public event EventHandler? Wylogowano;
    public event EventHandler? Zakonczono;

    private void Wyloguj()
    {
        if (!u.Okna.Potwierdz("Wylogować się z programu?")) return;
        u.Auth.Wyloguj();
        Wylogowano?.Invoke(this, EventArgs.Empty);
    }
}
