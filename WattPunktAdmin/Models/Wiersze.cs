namespace WattPunktAdmin.Models;

// Wiersze list wyswietlanych w siatkach (projekcje zapytan, tylko do odczytu)

public record StacjaWiersz(int StacjaID, string Kod, string Nazwa, string Miejscowosc, string Wojewodztwo,
                           string TypLokalizacji, DateTime DataUruchomienia, int LiczbaLadowarek);

public record LadowarkaWiersz(int LadowarkaID, string NumerSeryjny, int StacjaID, string Stacja, string Model,
                              short MocKW, string KlasaMocy, DateTime DataInstalacji, int LiczbaAwarii, bool AwariaOtwarta);

public record KlientWiersz(int KlientID, string TypKlienta, string Nazwa, string Miejscowosc, string Plan,
                           DateTime DataRejestracji, int LiczbaPojazdow);

public record PojazdWiersz(int PojazdID, string Model, short RokProdukcji, DateTime DataDodania, int LiczbaSesji);

public record HistoriaPlanuWiersz(string Plan, DateTime OdDaty, DateTime? DoDaty)
{
    public string Okres => DoDaty is null ? $"od {OdDaty:dd.MM.yyyy}" : $"{OdDaty:dd.MM.yyyy} do {DoDaty:dd.MM.yyyy}";
}

public record AwariaWiersz(int AwariaID, DateTime Poczatek, DateTime? Koniec, string Stacja, string NumerSeryjny,
                           string Kategoria, string? Opis)
{
    public bool Otwarta => Koniec is null;
    public string CzasNaprawy => Koniec is null ? "trwa" : $"{(Koniec.Value - Poczatek).TotalHours:0.0} h";
}

public record SesjaWiersz(long SesjaID, DateTime Start, string Stacja, string Ladowarka, string KlasaMocy,
                          string Klient, decimal EnergiaKWh, int CzasMin, decimal KwotaRazem, string MetodaPlatnosci, string Status);

public record PracownikWiersz(int PracownikID, string Login, string ImieNazwisko, Rola Rola, bool Aktywny,
                              int NieudaneProby, bool WymagaZmianyHasla, DateTime? OstatnieLogowanie)
{
    public bool Zablokowany => NieudaneProby >= 5;
    public string Stan => !Aktywny ? "wyłączone" : Zablokowany ? "zablokowane" : "aktywne";
}

public record WpisDziennikaWiersz(long WpisID, DateTime Data, string Pracownik, string Operacja, string Obiekt,
                                  int? ObiektID, string Opis);

public record StronaWynikow<T>(IReadOnlyList<T> Wiersze, int LiczbaWszystkich, decimal SumaEnergii, decimal SumaPrzychodu);

public record StacjaRanking(string Stacja, int Sesje, decimal Przychod, double Udzial);

public record Podsumowanie(int LiczbaStacji, int LiczbaLadowarek, int LiczbaKlientow, int OtwarteAwarie,
                           string OkresDanych, int SesjeMiesiac, decimal EnergiaMiesiac, decimal PrzychodMiesiac,
                           IReadOnlyList<StacjaRanking> NajlepszeStacje, IReadOnlyList<AwariaWiersz> OstatnieAwarie);

// Filtry list

public class FiltrStacji
{
    public string? Szukaj { get; set; }
    public string? Wojewodztwo { get; set; }
    public string? TypLokalizacji { get; set; }
}

public class FiltrLadowarek
{
    public string? Szukaj { get; set; }
    public int? StacjaID { get; set; }
    public string? KlasaMocy { get; set; }
}

public class FiltrKlientow
{
    public string? Szukaj { get; set; }
    public string? TypKlienta { get; set; }
    public int? PlanID { get; set; }
}

public class FiltrAwarii
{
    public bool TylkoOtwarte { get; set; }
    public int? StacjaID { get; set; }
    public int? KategoriaID { get; set; }
    public DateTime? Od { get; set; }
    public DateTime? Do { get; set; }
}

public class FiltrSesji
{
    public DateTime? Od { get; set; }
    public DateTime? Do { get; set; }
    public int? StacjaID { get; set; }
    public string? KlasaMocy { get; set; }
    public string? Status { get; set; }
    public string? Szukaj { get; set; }
}

public class FiltrDziennika
{
    public int? PracownikID { get; set; }
    public string? Operacja { get; set; }
    public DateTime? Od { get; set; }
    public DateTime? Do { get; set; }
}
