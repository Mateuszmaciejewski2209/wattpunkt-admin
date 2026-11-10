using WattPunktAdmin.Models;
using WattPunktAdmin.Services;
using WattPunktAdmin.ViewModels;

namespace WattPunktAdmin.Tests;

[TestClass]
public class FormularzeTests
{
    private static readonly Miejscowosc Warszawa = new() { MiejscowoscID = 1, Nazwa = "Warszawa", Wojewodztwo = "mazowieckie" };

    [TestMethod]
    public void StacjaFormularz_Pusty_PoWalidacjiMaBledy()
    {
        var f = new StacjaFormularz();
        Assert.IsFalse(f.WalidujWszystko());
        Assert.IsTrue(f.GetErrors(nameof(StacjaFormularz.Kod)).Cast<string>().Any());
        Assert.IsTrue(f.GetErrors(nameof(StacjaFormularz.Miejscowosc)).Cast<string>().Any());
    }

    [TestMethod]
    [DataRow("WAW7")]
    [DataRow("waw07")]
    [DataRow("WAW-7")]
    public void StacjaFormularz_ZlyKod_BladPrzyKodzie(string kod)
    {
        var f = new StacjaFormularz { Kod = kod };
        Assert.IsTrue(f.GetErrors(nameof(StacjaFormularz.Kod)).Cast<string>().Any());
    }

    [TestMethod]
    public void StacjaFormularz_PoprawneDane_BezBledow()
    {
        var f = new StacjaFormularz
        {
            Kod = "WAW07", Nazwa = "Warszawa Praga", Miejscowosc = Warszawa, TypLokalizacji = "osiedle", DataUruchomienia = DateTime.Today
        };
        Assert.IsTrue(f.WalidujWszystko());
        var s = f.DoEncji();
        Assert.AreEqual(1, s.MiejscowoscID);
    }

    [TestMethod]
    public void KlientFormularz_FirmaZPlanemStart_BladPlanu()
    {
        var f = new KlientFormularz
        {
            TypKlienta = KlientFormularz.Firmowy, Nazwa = "Eko Taxi sp. z o.o.", Miejscowosc = Warszawa,
            Plan = new PlanTaryfowy { PlanID = 1, Nazwa = "Start" }
        };
        Assert.IsFalse(f.WalidujWszystko());
        StringAssert.Contains(string.Join(" ", f.WszystkieBledy), "Flota");
    }

    [TestMethod]
    public void KlientFormularz_ZmianaTypuNaFirmowy_PonownieSprawdzaPlan()
    {
        var f = new KlientFormularz { Nazwa = "Jan Nowak", Miejscowosc = Warszawa, Plan = new PlanTaryfowy { PlanID = 2, Nazwa = "Plus" } };
        Assert.IsTrue(f.WalidujWszystko());
        f.TypKlienta = KlientFormularz.Firmowy;            // regula zalezna: plan musi byc sprawdzony ponownie
        Assert.IsTrue(f.HasErrors);
    }

    [TestMethod]
    public void LadowarkaFormularz_InstalacjaPrzedUruchomieniemStacji_Blad()
    {
        var stacja = new Stacja { StacjaID = 5, Kod = "WAW07", DataUruchomienia = new DateTime(2025, 6, 1) };
        var f = new LadowarkaFormularz
        {
            Stacja = stacja, Model = new ModelLadowarki { ModelLadowarkiID = 1 }, NumerSeryjny = "VAC-WAW07-1",
            DataInstalacji = new DateTime(2025, 5, 1)
        };
        Assert.IsFalse(f.WalidujWszystko());
        Assert.IsTrue(f.GetErrors(nameof(LadowarkaFormularz.DataInstalacji)).Cast<string>().Any());
    }

    [TestMethod]
    public void AwariaFormularz_ZlaGodzina_Blad()
    {
        var f = new AwariaFormularz { Godzina = "25:61" };
        Assert.IsTrue(f.GetErrors(nameof(AwariaFormularz.Godzina)).Cast<string>().Any());
        Assert.IsNull(f.Poczatek);
    }

    [TestMethod]
    public void PracownikFormularz_LoginZWielkimiLiterami_Blad()
    {
        var f = new PracownikFormularz { Login = "Jan Kowalski" };
        Assert.IsTrue(f.GetErrors(nameof(PracownikFormularz.Login)).Cast<string>().Any());
    }
}

[TestClass]
public class LogowanieViewModelTests
{
    private sealed class FalszywyAuth(WynikLogowania wynik) : IAuthService
    {
        public int Wywolania { get; private set; }
        public Task<WynikLogowania> ZalogujAsync(string login, string haslo) { Wywolania++; return Task.FromResult(wynik); }
        public Task ZmienHasloAsync(string stare, string nowe) => Task.CompletedTask;
        public void Wyloguj() { }
    }

    [TestMethod]
    public void Zaloguj_BezLoginuIHasla_PolecenieNieaktywne()
    {
        var vm = new LogowanieViewModel(new FalszywyAuth(new WynikLogowania(true, "")));
        Assert.IsFalse(vm.ZalogujCommand.CanExecute(null));
        vm.Login = "admin";
        vm.Haslo = "x";
        Assert.IsTrue(vm.ZalogujCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Zaloguj_ZleHaslo_KomunikatIWyczyszczoneHaslo()
    {
        var auth = new FalszywyAuth(new WynikLogowania(false, "Nieprawidłowy login lub hasło."));
        var vm = new LogowanieViewModel(auth) { Login = "admin", Haslo = "zle" };
        var zalogowano = false;
        vm.Zalogowano += (_, _) => zalogowano = true;

        vm.ZalogujCommand.Execute(null);
        await Task.Delay(50);

        Assert.AreEqual(1, auth.Wywolania);
        Assert.IsFalse(zalogowano);
        Assert.AreEqual("", vm.Haslo);
        StringAssert.Contains(vm.Komunikat, "Nieprawidłowy");
    }

    [TestMethod]
    public async Task Zaloguj_Sukces_ZdarzenieZalogowano()
    {
        var vm = new LogowanieViewModel(new FalszywyAuth(new WynikLogowania(true, "Zalogowano"))) { Login = "admin", Haslo = "dobre" };
        var zalogowano = false;
        vm.Zalogowano += (_, _) => zalogowano = true;

        vm.ZalogujCommand.Execute(null);
        await Task.Delay(50);

        Assert.IsTrue(zalogowano);
    }

    [TestMethod]
    public void ZmianaHasla_RozneHasla_BladPowtorzenia()
    {
        var vm = new ZmianaHaslaViewModel(new FalszywyAuth(new WynikLogowania(true, "")), wymuszona: true)
        {
            StareHaslo = "Stare2026!", NoweHaslo = "Nowe2026!", PowtorzHaslo = "Inne2026!"
        };
        Assert.IsFalse(vm.WalidujWszystko());
        StringAssert.Contains(string.Join(" ", vm.WszystkieBledy), "nie są takie same");
    }
}
