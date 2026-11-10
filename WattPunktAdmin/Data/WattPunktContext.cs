using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.Data;

// Model EF Core odwzorowujacy istniejaca baze WattPunktAdmin (podejscie database first)
public class WattPunktContext(DbContextOptions<WattPunktContext> opcje) : DbContext(opcje)
{
    public DbSet<Miejscowosc> Miejscowosci => Set<Miejscowosc>();
    public DbSet<Stacja> Stacje => Set<Stacja>();
    public DbSet<ModelLadowarki> ModeleLadowarek => Set<ModelLadowarki>();
    public DbSet<Ladowarka> Ladowarki => Set<Ladowarka>();
    public DbSet<KategoriaAwarii> KategorieAwarii => Set<KategoriaAwarii>();
    public DbSet<Awaria> Awarie => Set<Awaria>();
    public DbSet<PlanTaryfowy> PlanyTaryfowe => Set<PlanTaryfowy>();
    public DbSet<Klient> Klienci => Set<Klient>();
    public DbSet<HistoriaPlanu> HistoriaPlanow => Set<HistoriaPlanu>();
    public DbSet<ModelPojazdu> ModelePojazdow => Set<ModelPojazdu>();
    public DbSet<Pojazd> Pojazdy => Set<Pojazd>();
    public DbSet<Sesja> Sesje => Set<Sesja>();
    public DbSet<Pracownik> Pracownicy => Set<Pracownik>();
    public DbSet<WpisDziennika> DziennikZmian => Set<WpisDziennika>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Miejscowosc>(e => e.ToTable("Miejscowosci"));

        mb.Entity<Stacja>(e =>
        {
            e.ToTable("Stacje");
            e.Property(x => x.Kod).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Nazwa).HasMaxLength(60);
            e.Property(x => x.TypLokalizacji).HasMaxLength(20);
            e.Property(x => x.DataUruchomienia).HasColumnType("date");
            e.Property(x => x.Wersja).IsRowVersion();
            e.HasOne(x => x.Miejscowosc).WithMany().HasForeignKey(x => x.MiejscowoscID);
        });

        mb.Entity<ModelLadowarki>(e =>
        {
            e.ToTable("ModeleLadowarek");
            e.Property(x => x.TypPradu).HasColumnType("char(2)");
            e.Ignore(x => x.KlasaMocy);
        });

        mb.Entity<Ladowarka>(e =>
        {
            e.ToTable("Ladowarki");
            e.Property(x => x.NumerSeryjny).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.DataInstalacji).HasColumnType("date");
            e.Property(x => x.Wersja).IsRowVersion();
            e.HasOne(x => x.Stacja).WithMany(s => s.Ladowarki).HasForeignKey(x => x.StacjaID);
            e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelLadowarkiID);
        });

        mb.Entity<KategoriaAwarii>(e =>
        {
            e.ToTable("KategorieAwarii");
            e.HasKey(x => x.KategoriaID);
            e.Property(x => x.KategoriaID).ValueGeneratedNever();
        });

        mb.Entity<Awaria>(e =>
        {
            e.ToTable("Awarie");
            e.Property(x => x.Poczatek).HasColumnType("datetime2(0)");
            e.Property(x => x.Koniec).HasColumnType("datetime2(0)");
            e.Property(x => x.Opis).HasMaxLength(200);
            e.Property(x => x.Wersja).IsRowVersion();
            e.HasOne(x => x.Ladowarka).WithMany().HasForeignKey(x => x.LadowarkaID);
            e.HasOne(x => x.Kategoria).WithMany().HasForeignKey(x => x.KategoriaID);
        });

        mb.Entity<PlanTaryfowy>(e =>
        {
            e.ToTable("PlanyTaryfowe");
            e.HasKey(x => x.PlanID);
            e.Property(x => x.PlanID).ValueGeneratedNever();
            e.Property(x => x.OplataMiesieczna).HasColumnType("money");
            e.Property(x => x.RabatProcent).HasColumnType("decimal(4,1)");
        });

        mb.Entity<Klient>(e =>
        {
            e.ToTable("Klienci");
            e.Property(x => x.TypKlienta).HasMaxLength(15);
            e.Property(x => x.Nazwa).HasMaxLength(80);
            e.Property(x => x.DataRejestracji).HasColumnType("date");
            e.Property(x => x.Wersja).IsRowVersion();
            e.HasOne(x => x.Miejscowosc).WithMany().HasForeignKey(x => x.MiejscowoscID);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanID);
            e.HasMany(x => x.Pojazdy).WithOne().HasForeignKey(p => p.KlientID);
            e.HasMany(x => x.HistoriaPlanow).WithOne().HasForeignKey(h => h.KlientID);
        });

        mb.Entity<HistoriaPlanu>(e =>
        {
            e.ToTable("HistoriaPlanow");
            e.HasKey(x => new { x.KlientID, x.OdDaty });
            e.Property(x => x.OdDaty).HasColumnType("date");
            e.Property(x => x.DoDaty).HasColumnType("date");
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanID);
        });

        mb.Entity<ModelPojazdu>(e =>
        {
            e.ToTable("ModelePojazdow");
            e.Property(x => x.PojemnoscBateriiKWh).HasColumnType("decimal(5,1)");
            e.Property(x => x.MaksMocACkW).HasColumnType("decimal(4,1)");
        });

        mb.Entity<Pojazd>(e =>
        {
            e.ToTable("Pojazdy");
            e.Property(x => x.DataDodania).HasColumnType("date");
            e.Property(x => x.Wersja).IsRowVersion();
            e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelPojazduID);
        });

        mb.Entity<Sesja>(e =>
        {
            e.ToTable("Sesje");
            e.Property(x => x.Start).HasColumnType("datetime2(0)");
            e.Property(x => x.KoniecLadowania).HasColumnType("datetime2(0)");
            e.Property(x => x.Odlaczenie).HasColumnType("datetime2(0)");
            e.Property(x => x.EnergiaKWh).HasColumnType("decimal(7,3)");
            e.Property(x => x.CenaZaKWh).HasColumnType("money");
            e.Property(x => x.KwotaEnergia).HasColumnType("money");
            e.Property(x => x.KwotaRabatu).HasColumnType("money");
            e.Property(x => x.KwotaPostoj).HasColumnType("money");
            e.Property(x => x.KwotaRazem).HasColumnType("money");
            e.HasOne(x => x.Ladowarka).WithMany().HasForeignKey(x => x.LadowarkaID);
            e.HasOne(x => x.Klient).WithMany().HasForeignKey(x => x.KlientID);
        });

        mb.Entity<Pracownik>(e =>
        {
            e.ToTable("Pracownicy");
            e.Property(x => x.Login).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.Rola).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HasloHash).HasMaxLength(32);
            e.Property(x => x.HasloSol).HasMaxLength(16);
            e.Property(x => x.DataUtworzenia).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
            e.Property(x => x.OstatnieLogowanie).HasColumnType("datetime2(0)");
            e.Property(x => x.Wersja).IsRowVersion();
            e.Ignore(x => x.ImieNazwisko);
        });

        mb.Entity<WpisDziennika>(e =>
        {
            e.ToTable("DziennikZmian");
            e.HasKey(x => x.WpisID);
            e.Property(x => x.Data).HasColumnType("datetime2(0)");
            e.HasOne(x => x.Pracownik).WithMany().HasForeignKey(x => x.PracownikID);
        });
    }
}
