namespace WattPunktAdmin.Models;

public class PlanTaryfowy
{
    public int PlanID { get; set; }
    public string Nazwa { get; set; } = "";
    public decimal OplataMiesieczna { get; set; }
    public decimal RabatProcent { get; set; }
    public string? Opis { get; set; }

    public override string ToString() => Nazwa;
}

public class Klient
{
    public int KlientID { get; set; }
    public string TypKlienta { get; set; } = "";
    public string Nazwa { get; set; } = "";
    public int MiejscowoscID { get; set; }
    public DateTime DataRejestracji { get; set; }
    public int PlanID { get; set; }
    public byte[] Wersja { get; set; } = [];

    public Miejscowosc? Miejscowosc { get; set; }
    public PlanTaryfowy? Plan { get; set; }
    public List<Pojazd> Pojazdy { get; set; } = [];
    public List<HistoriaPlanu> HistoriaPlanow { get; set; } = [];
}

public class HistoriaPlanu
{
    public int KlientID { get; set; }
    public int PlanID { get; set; }
    public DateTime OdDaty { get; set; }
    public DateTime? DoDaty { get; set; }

    public PlanTaryfowy? Plan { get; set; }
}

public class ModelPojazdu
{
    public int ModelPojazduID { get; set; }
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public string Segment { get; set; } = "";
    public decimal PojemnoscBateriiKWh { get; set; }
    public decimal MaksMocACkW { get; set; }
    public short MaksMocDCkW { get; set; }

    public override string ToString() => $"{Marka} {Model}";
}

public class Pojazd
{
    public int PojazdID { get; set; }
    public int KlientID { get; set; }
    public int ModelPojazduID { get; set; }
    public short RokProdukcji { get; set; }
    public DateTime DataDodania { get; set; }
    public byte[] Wersja { get; set; } = [];

    public ModelPojazdu? Model { get; set; }
}

public class Sesja
{
    public long SesjaID { get; set; }
    public int LadowarkaID { get; set; }
    public int? KlientID { get; set; }
    public int? PojazdID { get; set; }
    public string MetodaPlatnosci { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime KoniecLadowania { get; set; }
    public DateTime Odlaczenie { get; set; }
    public byte? SocStart { get; set; }
    public byte? SocKoniec { get; set; }
    public decimal EnergiaKWh { get; set; }
    public decimal CenaZaKWh { get; set; }
    public decimal KwotaEnergia { get; set; }
    public decimal KwotaRabatu { get; set; }
    public decimal KwotaPostoj { get; set; }
    public decimal KwotaRazem { get; set; }
    public string Status { get; set; } = "";

    public Ladowarka? Ladowarka { get; set; }
    public Klient? Klient { get; set; }
}
