using System.ComponentModel.DataAnnotations;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.ViewModels;

public class StacjaFormularz : WalidowanyViewModel
{
    private string kod = "", nazwa = "";
    private Miejscowosc? miejscowosc;
    private string? typLokalizacji;
    private DateTime? dataUruchomienia = DateTime.Today;

    public int StacjaID { get; private set; }
    public byte[] Wersja { get; private set; } = [];
    public bool CzyNowa => StacjaID == 0;
    public string Tytul => CzyNowa ? "Nowa stacja" : $"Stacja {Kod}";

    [Required(ErrorMessage = "Podaj kod stacji.")]
    [RegularExpression("^[A-Z0-9]{5}$", ErrorMessage = "Kod to 5 wielkich liter lub cyfr, np. WAW07.")]
    public string Kod { get => kod; set => UstawIWaliduj(ref kod, value); }

    [Required(ErrorMessage = "Podaj nazwę stacji.")]
    [StringLength(60, MinimumLength = 3, ErrorMessage = "Nazwa musi mieć od 3 do 60 znaków.")]
    public string Nazwa { get => nazwa; set => UstawIWaliduj(ref nazwa, value); }

    [Required(ErrorMessage = "Wybierz miejscowość.")]
    public Miejscowosc? Miejscowosc { get => miejscowosc; set => UstawIWaliduj(ref miejscowosc, value); }

    [Required(ErrorMessage = "Wybierz typ lokalizacji.")]
    public string? TypLokalizacji { get => typLokalizacji; set => UstawIWaliduj(ref typLokalizacji, value); }

    [Required(ErrorMessage = "Podaj datę uruchomienia.")]
    public DateTime? DataUruchomienia { get => dataUruchomienia; set => UstawIWaliduj(ref dataUruchomienia, value); }

    protected override IEnumerable<string> RegulyDodatkowe(string wlasciwosc)
    {
        if (wlasciwosc == nameof(DataUruchomienia) && DataUruchomienia is DateTime d)
        {
            if (d < new DateTime(2020, 1, 1)) yield return "Sieć działa od 2022 r., data nie może być wcześniejsza niż 2020 r.";
            if (d > DateTime.Today.AddYears(1)) yield return "Data uruchomienia może wybiegać najwyżej rok w przyszłość.";
        }
    }

    public void Wczytaj(Stacja s, IEnumerable<Miejscowosc> miejscowosci)
    {
        StacjaID = s.StacjaID; Wersja = s.Wersja;
        Kod = s.Kod; Nazwa = s.Nazwa; TypLokalizacji = s.TypLokalizacji; DataUruchomienia = s.DataUruchomienia;
        Miejscowosc = miejscowosci.FirstOrDefault(m => m.MiejscowoscID == s.MiejscowoscID);
        WyczyscBledy();
        OnPropertyChanged(nameof(CzyNowa)); OnPropertyChanged(nameof(Tytul));
    }

    public Stacja DoEncji() => new()
    {
        StacjaID = StacjaID, Wersja = Wersja, Kod = Kod, Nazwa = Nazwa, MiejscowoscID = Miejscowosc!.MiejscowoscID,
        TypLokalizacji = TypLokalizacji!, DataUruchomienia = DataUruchomienia!.Value
    };
}

public class LadowarkaFormularz : WalidowanyViewModel
{
    private Stacja? stacja;
    private ModelLadowarki? model;
    private string numerSeryjny = "";
    private DateTime? dataInstalacji = DateTime.Today;

    public int LadowarkaID { get; private set; }
    public byte[] Wersja { get; private set; } = [];
    public bool CzyNowa => LadowarkaID == 0;
    public string Tytul => CzyNowa ? "Nowa ładowarka" : $"Ładowarka {NumerSeryjny}";

    [Required(ErrorMessage = "Wybierz stację.")]
    public Stacja? Stacja { get => stacja; set => UstawIWaliduj(ref stacja, value); }

    [Required(ErrorMessage = "Wybierz model ładowarki.")]
    public ModelLadowarki? Model { get => model; set => UstawIWaliduj(ref model, value); }

    [Required(ErrorMessage = "Podaj numer seryjny.")]
    [StringLength(20, ErrorMessage = "Numer seryjny może mieć najwyżej 20 znaków.")]
    [RegularExpression("^[A-Z0-9]+(-[A-Z0-9]+)*$", ErrorMessage = "Numer seryjny: wielkie litery i cyfry rozdzielone myślnikami, np. VAC-WAW07-1.")]
    public string NumerSeryjny { get => numerSeryjny; set => UstawIWaliduj(ref numerSeryjny, value); }

    [Required(ErrorMessage = "Podaj datę instalacji.")]
    public DateTime? DataInstalacji { get => dataInstalacji; set => UstawIWaliduj(ref dataInstalacji, value); }

    protected override IEnumerable<string> RegulyDodatkowe(string wlasciwosc)
    {
        if (wlasciwosc == nameof(DataInstalacji) && DataInstalacji is DateTime d && Stacja is not null && d < Stacja.DataUruchomienia)
            yield return $"Instalacja nie może być wcześniejsza niż uruchomienie stacji ({Stacja.DataUruchomienia:dd.MM.yyyy}).";
    }

    // zmiana stacji wplywa na poprawnosc daty instalacji
    protected override IEnumerable<string> WlasciwosciZalezne(string wlasciwosc) =>
        wlasciwosc == nameof(Stacja) ? [nameof(DataInstalacji)] : [];

    public void Wczytaj(Ladowarka l, IEnumerable<Stacja> stacje, IEnumerable<ModelLadowarki> modele)
    {
        LadowarkaID = l.LadowarkaID; Wersja = l.Wersja;
        Stacja = stacje.FirstOrDefault(s => s.StacjaID == l.StacjaID);
        Model = modele.FirstOrDefault(m => m.ModelLadowarkiID == l.ModelLadowarkiID);
        NumerSeryjny = l.NumerSeryjny; DataInstalacji = l.DataInstalacji;
        WyczyscBledy();
        OnPropertyChanged(nameof(CzyNowa)); OnPropertyChanged(nameof(Tytul));
    }

    public Ladowarka DoEncji() => new()
    {
        LadowarkaID = LadowarkaID, Wersja = Wersja, StacjaID = Stacja!.StacjaID, ModelLadowarkiID = Model!.ModelLadowarkiID,
        NumerSeryjny = NumerSeryjny, DataInstalacji = DataInstalacji!.Value
    };
}

public class KlientFormularz : WalidowanyViewModel
{
    public const string Indywidualny = "indywidualny", Firmowy = "firmowy";
    private string typKlienta = Indywidualny, nazwa = "";
    private Miejscowosc? miejscowosc;
    private PlanTaryfowy? plan;
    private DateTime? dataRejestracji = DateTime.Today;

    public int KlientID { get; private set; }
    public byte[] Wersja { get; private set; } = [];
    public bool CzyNowy => KlientID == 0;
    public string Tytul => CzyNowy ? "Nowy klient" : $"Klient nr {KlientID}";

    [Required(ErrorMessage = "Wybierz typ klienta.")]
    public string TypKlienta { get => typKlienta; set => UstawIWaliduj(ref typKlienta, value); }

    [Required(ErrorMessage = "Podaj imię i nazwisko albo nazwę firmy.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Nazwa musi mieć od 3 do 80 znaków.")]
    public string Nazwa { get => nazwa; set => UstawIWaliduj(ref nazwa, value); }

    [Required(ErrorMessage = "Wybierz miejscowość.")]
    public Miejscowosc? Miejscowosc { get => miejscowosc; set => UstawIWaliduj(ref miejscowosc, value); }

    [Required(ErrorMessage = "Wybierz plan taryfowy.")]
    public PlanTaryfowy? Plan { get => plan; set => UstawIWaliduj(ref plan, value); }

    [Required(ErrorMessage = "Podaj datę rejestracji.")]
    public DateTime? DataRejestracji { get => dataRejestracji; set => UstawIWaliduj(ref dataRejestracji, value); }

    protected override IEnumerable<string> PolaZRegulami => [nameof(Plan)];

    protected override IEnumerable<string> RegulyDodatkowe(string wlasciwosc)
    {
        if (wlasciwosc == nameof(Plan) && Plan is not null)
        {
            if (TypKlienta == Firmowy && Plan.Nazwa != "Flota") yield return "Klient firmowy musi mieć plan Flota.";
            if (TypKlienta == Indywidualny && Plan.Nazwa == "Flota") yield return "Plan Flota jest tylko dla firm.";
        }
        if (wlasciwosc == nameof(DataRejestracji) && DataRejestracji > DateTime.Today)
            yield return "Data rejestracji nie może być w przyszłości.";
    }

    protected override IEnumerable<string> WlasciwosciZalezne(string wlasciwosc) =>
        wlasciwosc == nameof(TypKlienta) ? [nameof(Plan)] : [];

    public void Wczytaj(Klient k, IEnumerable<Miejscowosc> miejscowosci, IEnumerable<PlanTaryfowy> plany)
    {
        KlientID = k.KlientID; Wersja = k.Wersja;
        TypKlienta = k.TypKlienta; Nazwa = k.Nazwa; DataRejestracji = k.DataRejestracji;
        Miejscowosc = miejscowosci.FirstOrDefault(m => m.MiejscowoscID == k.MiejscowoscID);
        Plan = plany.FirstOrDefault(p => p.PlanID == k.PlanID);
        WyczyscBledy();
        OnPropertyChanged(nameof(CzyNowy)); OnPropertyChanged(nameof(Tytul));
    }

    public Klient DoEncji() => new()
    {
        KlientID = KlientID, Wersja = Wersja, TypKlienta = TypKlienta, Nazwa = Nazwa, MiejscowoscID = Miejscowosc!.MiejscowoscID,
        PlanID = Plan!.PlanID, DataRejestracji = DataRejestracji!.Value
    };
}

public class PojazdFormularz : WalidowanyViewModel
{
    private ModelPojazdu? model;
    private int? rokProdukcji = DateTime.Today.Year;

    [Required(ErrorMessage = "Wybierz model pojazdu.")]
    public ModelPojazdu? Model { get => model; set => UstawIWaliduj(ref model, value); }

    [Required(ErrorMessage = "Podaj rok produkcji.")]
    public int? RokProdukcji { get => rokProdukcji; set => UstawIWaliduj(ref rokProdukcji, value); }

    protected override IEnumerable<string> RegulyDodatkowe(string wlasciwosc)
    {
        if (wlasciwosc == nameof(RokProdukcji) && RokProdukcji is int r && (r < 2000 || r > DateTime.Today.Year + 1))
            yield return $"Rok produkcji musi być z zakresu 2000 do {DateTime.Today.Year + 1}.";
    }

    public void Wyczysc()
    {
        model = null; rokProdukcji = DateTime.Today.Year;
        OnPropertyChanged(nameof(Model)); OnPropertyChanged(nameof(RokProdukcji));
        WyczyscBledy();
    }
}

public class AwariaFormularz : WalidowanyViewModel
{
    private Stacja? stacja;
    private Ladowarka? ladowarka;
    private KategoriaAwarii? kategoria;
    private DateTime? data = DateTime.Today;
    private string godzina = DateTime.Now.ToString("HH:mm");
    private string opis = "";

    [Required(ErrorMessage = "Wybierz stację.")]
    public Stacja? Stacja { get => stacja; set => UstawIWaliduj(ref stacja, value); }

    [Required(ErrorMessage = "Wybierz ładowarkę.")]
    public Ladowarka? Ladowarka { get => ladowarka; set => UstawIWaliduj(ref ladowarka, value); }

    [Required(ErrorMessage = "Wybierz kategorię awarii.")]
    public KategoriaAwarii? Kategoria { get => kategoria; set => UstawIWaliduj(ref kategoria, value); }

    [Required(ErrorMessage = "Podaj datę początku awarii.")]
    public DateTime? Data { get => data; set => UstawIWaliduj(ref data, value); }

    [Required(ErrorMessage = "Podaj godzinę.")]
    [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Godzina w formacie GG:MM, np. 14:30.")]
    public string Godzina { get => godzina; set => UstawIWaliduj(ref godzina, value); }

    [StringLength(200, ErrorMessage = "Opis może mieć najwyżej 200 znaków.")]
    public string Opis { get => opis; set => UstawIWaliduj(ref opis, value); }

    public DateTime? Poczatek =>
        Data is DateTime d && TimeSpan.TryParse(Godzina, out var t) ? d.Date + t : null;

    protected override IEnumerable<string> PolaZRegulami => [nameof(Data)];

    protected override IEnumerable<string> RegulyDodatkowe(string wlasciwosc)
    {
        if (wlasciwosc == nameof(Data) && Poczatek > DateTime.Now)
            yield return "Początek awarii nie może być w przyszłości.";
    }

    protected override IEnumerable<string> WlasciwosciZalezne(string wlasciwosc) =>
        wlasciwosc == nameof(Godzina) ? [nameof(Data)] : [];

    public void Wyczysc()
    {
        ladowarka = null; kategoria = null; data = DateTime.Today; godzina = DateTime.Now.ToString("HH:mm"); opis = "";
        foreach (var p in new[] { nameof(Ladowarka), nameof(Kategoria), nameof(Data), nameof(Godzina), nameof(Opis) }) OnPropertyChanged(p);
        WyczyscBledy();
    }

    public Awaria DoEncji() => new()
    {
        LadowarkaID = Ladowarka!.LadowarkaID, KategoriaID = Kategoria!.KategoriaID, Poczatek = Poczatek!.Value,
        Opis = string.IsNullOrWhiteSpace(Opis) ? null : Opis
    };
}

public class PracownikFormularz : WalidowanyViewModel
{
    private string login = "", imie = "", nazwisko = "";
    private Rola? rola = Models.Rola.Pracownik;
    private bool aktywny = true;

    public int PracownikID { get; private set; }
    public byte[] Wersja { get; private set; } = [];
    public bool CzyNowy => PracownikID == 0;
    public string Tytul => CzyNowy ? "Nowe konto" : $"Konto {Login}";

    [Required(ErrorMessage = "Podaj login.")]
    [RegularExpression("^[a-z][a-z0-9._]{2,29}$", ErrorMessage = "Login: 3 do 30 małych liter, cyfr, kropek lub podkreśleń, zaczyna się literą.")]
    public string Login { get => login; set => UstawIWaliduj(ref login, value); }

    [Required(ErrorMessage = "Podaj imię.")]
    [StringLength(40, ErrorMessage = "Imię może mieć najwyżej 40 znaków.")]
    public string Imie { get => imie; set => UstawIWaliduj(ref imie, value); }

    [Required(ErrorMessage = "Podaj nazwisko.")]
    [StringLength(40, ErrorMessage = "Nazwisko może mieć najwyżej 40 znaków.")]
    public string Nazwisko { get => nazwisko; set => UstawIWaliduj(ref nazwisko, value); }

    [Required(ErrorMessage = "Wybierz rolę.")]
    public Rola? Rola { get => rola; set => UstawIWaliduj(ref rola, value); }

    public bool Aktywny { get => aktywny; set => Ustaw(ref aktywny, value); }

    public void Wczytaj(Pracownik p)
    {
        PracownikID = p.PracownikID; Wersja = p.Wersja;
        Login = p.Login; Imie = p.Imie; Nazwisko = p.Nazwisko; Rola = p.Rola; Aktywny = p.Aktywny;
        WyczyscBledy();
        OnPropertyChanged(nameof(CzyNowy)); OnPropertyChanged(nameof(Tytul));
    }

    public Pracownik DoEncji() => new()
    {
        PracownikID = PracownikID, Wersja = Wersja, Login = Login, Imie = Imie, Nazwisko = Nazwisko, Rola = Rola!.Value, Aktywny = Aktywny
    };
}
