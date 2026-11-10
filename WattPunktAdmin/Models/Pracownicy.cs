namespace WattPunktAdmin.Models;

public enum Rola
{
    Pracownik,
    Administrator
}

public class Pracownik
{
    public int PracownikID { get; set; }
    public string Login { get; set; } = "";
    public string Imie { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public Rola Rola { get; set; }
    public byte[] HasloHash { get; set; } = [];
    public byte[] HasloSol { get; set; } = [];
    public int Iteracje { get; set; }
    public bool Aktywny { get; set; } = true;
    public int NieudaneProby { get; set; }
    public bool WymagaZmianyHasla { get; set; }
    public DateTime DataUtworzenia { get; set; }
    public DateTime? OstatnieLogowanie { get; set; }
    public byte[] Wersja { get; set; } = [];

    public string ImieNazwisko => $"{Imie} {Nazwisko}";
}

public class WpisDziennika
{
    public long WpisID { get; set; }
    public DateTime Data { get; set; }
    public int PracownikID { get; set; }
    public string Operacja { get; set; } = "";
    public string Obiekt { get; set; } = "";
    public int? ObiektID { get; set; }
    public string Opis { get; set; } = "";

    public Pracownik? Pracownik { get; set; }
}
