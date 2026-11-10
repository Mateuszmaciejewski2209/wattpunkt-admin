namespace WattPunktAdmin.Models;

public class Miejscowosc
{
    public int MiejscowoscID { get; set; }
    public string Nazwa { get; set; } = "";
    public string Wojewodztwo { get; set; } = "";

    public override string ToString() => $"{Nazwa} ({Wojewodztwo})";
}

public class Stacja
{
    public int StacjaID { get; set; }
    public string Kod { get; set; } = "";
    public string Nazwa { get; set; } = "";
    public int MiejscowoscID { get; set; }
    public string TypLokalizacji { get; set; } = "";
    public DateTime DataUruchomienia { get; set; }
    public byte[] Wersja { get; set; } = [];

    public Miejscowosc? Miejscowosc { get; set; }
    public List<Ladowarka> Ladowarki { get; set; } = [];

    public override string ToString() => $"{Kod} {Nazwa}";
}

public class ModelLadowarki
{
    public int ModelLadowarkiID { get; set; }
    public string Producent { get; set; } = "";
    public string Model { get; set; } = "";
    public short MocKW { get; set; }
    public string TypPradu { get; set; } = "";
    public string Zlacza { get; set; } = "";
    public bool TerminalPlatniczy { get; set; }

    public string KlasaMocy => TypPradu == "AC" ? "AC" : MocKW >= 150 ? "HPC" : "DC";
    public override string ToString() => $"{Producent} {Model} ({MocKW} kW {TypPradu})";
}

public class Ladowarka
{
    public int LadowarkaID { get; set; }
    public int StacjaID { get; set; }
    public int ModelLadowarkiID { get; set; }
    public string NumerSeryjny { get; set; } = "";
    public DateTime DataInstalacji { get; set; }
    public byte[] Wersja { get; set; } = [];

    public Stacja? Stacja { get; set; }
    public ModelLadowarki? Model { get; set; }

    public override string ToString() => NumerSeryjny;
}

public class KategoriaAwarii
{
    public int KategoriaID { get; set; }
    public string Nazwa { get; set; } = "";
    public string Grupa { get; set; } = "";

    public override string ToString() => Nazwa;
}

public class Awaria
{
    public int AwariaID { get; set; }
    public int LadowarkaID { get; set; }
    public int KategoriaID { get; set; }
    public DateTime Poczatek { get; set; }
    public DateTime? Koniec { get; set; }
    public string? Opis { get; set; }
    public byte[] Wersja { get; set; } = [];

    public Ladowarka? Ladowarka { get; set; }
    public KategoriaAwarii? Kategoria { get; set; }
}
