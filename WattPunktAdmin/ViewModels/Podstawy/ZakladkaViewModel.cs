using WattPunktAdmin.Data;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.ViewModels;

// Stan wspolny dla calego okna: pasek stanu i wskaznik pracy
public class StanAplikacji : ViewModelBase
{
    private bool zajety;
    private string komunikat = "Gotowe";

    public bool Zajety { get => zajety; set => Ustaw(ref zajety, value); }
    public string Komunikat { get => komunikat; set => Ustaw(ref komunikat, value); }
}

// Okna dialogowe za interfejsem: ViewModel nie zna klasy MessageBox, wiec da sie go testowac
public interface IOkna
{
    bool Potwierdz(string tresc, string tytul = "Potwierdzenie");
    void Informacja(string tresc, string tytul = "Informacja");
    void Ostrzezenie(string tresc, string tytul = "Uwaga");
    void Blad(string tresc, string tytul = "Błąd");
    bool ZmienHaslo(IAuthService auth, bool wymuszona);
}

// Przejscia miedzy zakladkami: zakladka zglasza zamiar, a MainViewModel przelacza zakladke,
// wiec ViewModele zakladek nie musza sie znac nawzajem
public class Nawigacja
{
    public event Func<int, int, Task>? ZgloszenieAwarii;

    public Task ZglosAwarieAsync(int stacjaId, int ladowarkaId) =>
        ZgloszenieAwarii?.Invoke(stacjaId, ladowarkaId) ?? Task.CompletedTask;
}

// Wszystkie uslugi w jednym obiekcie przekazywanym do ViewModeli zakladek
public class Uslugi(IFabrykaKontekstu fabryka, SesjaUzytkownika sesja, IOkna okna)
{
    public SesjaUzytkownika Sesja { get; } = sesja;
    public IOkna Okna { get; } = okna;
    public StanAplikacji Stan { get; } = new();
    public Nawigacja Nawigacja { get; } = new();
    public IAuthService Auth { get; } = new AuthService(fabryka, sesja);
    public ISlownikiService Slowniki { get; } = new SlownikiService(fabryka);
    public IStacjeService Stacje { get; } = new StacjeService(fabryka, sesja);
    public IKlienciService Klienci { get; } = new KlienciService(fabryka, sesja);
    public IAwarieService Awarie { get; } = new AwarieService(fabryka, sesja);
    public ISesjeService Sesje { get; } = new SesjeService(fabryka);
    public IPracownicyService Pracownicy { get; } = new PracownicyService(fabryka, sesja);
    public IPanelService Panel { get; } = new PanelService(fabryka);
}

public abstract class ZakladkaViewModel(string naglowek, Uslugi uslugi) : ViewModelBase
{
    protected Uslugi U { get; } = uslugi;
    public string Naglowek { get; } = naglowek;
    public bool Zaladowana { get; private set; }

    public bool CzyAdmin => U.Sesja.Rola == Rola.Administrator;
    public bool MozeUsuwac => Uprawnienia.Usuwanie(U.Sesja.Rola);
    protected const string KomunikatTrwa = "Trwa komunikacja z bazą danych...";

    private Task ladowanie = Task.CompletedTask;

    // zwraca trwajace albo zakonczone wczytywanie, wiec mozna na nie poczekac (np. przed przygotowaniem formularza)
    public Task ZaladujJesliTrzebaAsync()
    {
        if (Zaladowana) return ladowanie;
        Zaladowana = true;
        // komunikat ustawiony przez ZaladujAsync (np. liczba wierszy) ma pierwszenstwo przed ogolnym
        return ladowanie = WykonajAsync(async () =>
        {
            await ZaladujAsync();
            if (U.Stan.Komunikat == KomunikatTrwa) U.Stan.Komunikat = "Wczytano dane: " + Naglowek;
        });
    }

    /// <summary>Oznacza dane jako nieaktualne, nastepne wywolanie ZaladujJesliTrzebaAsync wczyta je ponownie (F5).</summary>
    public void OdswiezNaZadanie() => Zaladowana = false;

    protected abstract Task ZaladujAsync();

    // Wspolna obsluga operacji na bazie: wskaznik pracy, komunikat na pasku stanu i czytelny opis bledu
    protected async Task<bool> WykonajAsync(Func<Task> operacja, string? komunikatSukcesu = null)
    {
        var poprzedni = U.Stan.Komunikat;
        U.Stan.Zajety = true;
        U.Stan.Komunikat = KomunikatTrwa;
        try
        {
            await operacja();
            // bez wlasnego komunikatu zostaje ten ustawiony przez operacje albo poprzedni
            if (komunikatSukcesu is not null) U.Stan.Komunikat = komunikatSukcesu;
            else if (U.Stan.Komunikat == KomunikatTrwa) U.Stan.Komunikat = poprzedni;
            return true;
        }
        catch (RegulaException ex)
        {
            U.Stan.Zajety = false;
            U.Stan.Komunikat = ex.Message;
            U.Okna.Ostrzezenie(ex.Message);
        }
        catch (BrakUprawnienException ex)
        {
            U.Stan.Zajety = false;
            U.Stan.Komunikat = ex.Message;
            U.Okna.Ostrzezenie(ex.Message, "Brak uprawnień");
        }
        catch (Exception ex)
        {
            Log.Zapisz(ex, Naglowek);
            var opis = BledyBazy.Opisz(ex);
            U.Stan.Zajety = false;
            U.Stan.Komunikat = opis;
            U.Okna.Blad(opis);
        }
        finally
        {
            U.Stan.Zajety = false;
        }
        return false;
    }
}
