using System.Windows.Input;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

public class LogowanieViewModel : ViewModelBase
{
    private readonly IAuthService auth;
    private string login = "", haslo = "", komunikat = "";
    private bool trwa;

    public LogowanieViewModel(IAuthService auth)
    {
        this.auth = auth;
        ZalogujCommand = new AsyncRelayCommand(ZalogujAsync, () => !string.IsNullOrWhiteSpace(Login) && Haslo.Length > 0);
    }

    public string Login { get => login; set => Ustaw(ref login, value); }
    public string Haslo { get => haslo; set => Ustaw(ref haslo, value); }
    public string Komunikat { get => komunikat; private set => Ustaw(ref komunikat, value); }
    public bool Trwa { get => trwa; private set => Ustaw(ref trwa, value); }

    public ICommand ZalogujCommand { get; }
    public event EventHandler? Zalogowano;

    private async Task ZalogujAsync()
    {
        Trwa = true;
        Komunikat = "Sprawdzanie danych...";
        try
        {
            var wynik = await auth.ZalogujAsync(Login, Haslo);
            Komunikat = wynik.Komunikat;
            if (wynik.Sukces) Zalogowano?.Invoke(this, EventArgs.Empty);
            else Haslo = "";
        }
        catch (Exception ex)
        {
            Log.Zapisz(ex, "logowanie");
            Komunikat = BledyBazy.Opisz(ex);
        }
        finally
        {
            Trwa = false;
        }
    }
}

public class ZmianaHaslaViewModel : WalidowanyViewModel
{
    private readonly IAuthService auth;
    private string stareHaslo = "", noweHaslo = "", powtorzHaslo = "", komunikat = "";

    public ZmianaHaslaViewModel(IAuthService auth, bool wymuszona)
    {
        this.auth = auth;
        Wymuszona = wymuszona;
        ZmienCommand = new AsyncRelayCommand(ZmienAsync);
        AnulujCommand = new RelayCommand(() => Zakonczono?.Invoke(this, false));
    }

    public bool Wymuszona { get; }
    public string Naglowek => Wymuszona
        ? "Hasło zostało ustawione przez administratora. Przed rozpoczęciem pracy ustaw własne hasło."
        : "Podaj obecne hasło i dwa razy nowe hasło.";

    public string StareHaslo { get => stareHaslo; set => UstawIWaliduj(ref stareHaslo, value); }
    public string NoweHaslo { get => noweHaslo; set => UstawIWaliduj(ref noweHaslo, value); }
    public string PowtorzHaslo { get => powtorzHaslo; set => UstawIWaliduj(ref powtorzHaslo, value); }
    public string Komunikat { get => komunikat; private set => Ustaw(ref komunikat, value); }

    public ICommand ZmienCommand { get; }
    public ICommand AnulujCommand { get; }
    public event EventHandler<bool>? Zakonczono;

    protected override IEnumerable<string> PolaZRegulami => [nameof(StareHaslo), nameof(NoweHaslo), nameof(PowtorzHaslo)];

    protected override IEnumerable<string> RegulyDodatkowe(string wlasciwosc)
    {
        switch (wlasciwosc)
        {
            case nameof(StareHaslo) when StareHaslo.Length == 0:
                yield return "Podaj obecne hasło.";
                break;
            case nameof(NoweHaslo) when Hasla.SprawdzSile(NoweHaslo) is string blad:
                yield return blad;
                break;
            case nameof(PowtorzHaslo) when PowtorzHaslo != NoweHaslo:
                yield return "Hasła nie są takie same.";
                break;
        }
    }

    protected override IEnumerable<string> WlasciwosciZalezne(string wlasciwosc) =>
        wlasciwosc == nameof(NoweHaslo) && PowtorzHaslo.Length > 0 ? [nameof(PowtorzHaslo)] : [];

    private async Task ZmienAsync()
    {
        if (!WalidujWszystko()) { Komunikat = "Popraw zaznaczone pola."; return; }
        try
        {
            await auth.ZmienHasloAsync(StareHaslo, NoweHaslo);
            Zakonczono?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            if (ex is not RegulaException) Log.Zapisz(ex, "zmiana hasła");
            Komunikat = BledyBazy.Opisz(ex);
        }
    }
}
