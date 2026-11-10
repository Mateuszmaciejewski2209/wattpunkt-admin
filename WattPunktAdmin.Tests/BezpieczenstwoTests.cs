using Microsoft.EntityFrameworkCore;
using WattPunktAdmin.Models;
using WattPunktAdmin.Services;

namespace WattPunktAdmin.Tests;

[TestClass]
public class HaslaTests
{
    [TestMethod]
    public void Sprawdz_PoprawneHaslo_ZwracaTrue()
    {
        var (skrot, sol) = Hasla.Utworz("Tajne2026!");
        Assert.IsTrue(Hasla.Sprawdz("Tajne2026!", skrot, sol, Hasla.Iteracje));
    }

    [TestMethod]
    public void Sprawdz_ZleHaslo_ZwracaFalse()
    {
        var (skrot, sol) = Hasla.Utworz("Tajne2026!");
        Assert.IsFalse(Hasla.Sprawdz("tajne2026!", skrot, sol, Hasla.Iteracje));
    }

    [TestMethod]
    public void Utworz_TeSameHasla_DajaRozneSkrotyDziekiSoli()
    {
        var a = Hasla.Utworz("Tajne2026!");
        var b = Hasla.Utworz("Tajne2026!");
        CollectionAssert.AreNotEqual(a.Sol, b.Sol);
        CollectionAssert.AreNotEqual(a.Skrot, b.Skrot);
    }

    [TestMethod]
    public void Tymczasowe_SpelniaWymaganiaSilyHasla()
    {
        for (int i = 0; i < 50; i++)
            Assert.IsNull(Hasla.SprawdzSile(Hasla.Tymczasowe()));
    }

    [TestMethod]
    [DataRow("krotki1")]
    [DataRow("bezcyfrhaslo")]
    [DataRow("1234567890")]
    public void SprawdzSile_SlabeHaslo_ZwracaKomunikat(string haslo)
    {
        Assert.IsNotNull(Hasla.SprawdzSile(haslo));
    }

    [TestMethod]
    public void SprawdzSile_OsiemZnakowLiteraICyfra_Wystarczy()
    {
        Assert.IsNull(Hasla.SprawdzSile("haslo123"));
    }
}

[TestClass]
public class UprawnieniaTests
{
    [TestMethod]
    public void Pracownik_ObslugujeKlientowIAwarie_AleNieUsuwaIniEdytujeStacji()
    {
        Assert.IsTrue(Uprawnienia.ObslugaKlientow(Rola.Pracownik));
        Assert.IsTrue(Uprawnienia.ObslugaAwarii(Rola.Pracownik));
        Assert.IsFalse(Uprawnienia.Usuwanie(Rola.Pracownik));
        Assert.IsFalse(Uprawnienia.EdycjaInfrastruktury(Rola.Pracownik));
        Assert.IsFalse(Uprawnienia.ZarzadzaniePracownikami(Rola.Pracownik));
        Assert.IsFalse(Uprawnienia.PrzegladanieDziennika(Rola.Pracownik));
    }

    [TestMethod]
    public void Administrator_MaWszystkieUprawnienia()
    {
        Assert.IsTrue(Uprawnienia.Usuwanie(Rola.Administrator));
        Assert.IsTrue(Uprawnienia.EdycjaInfrastruktury(Rola.Administrator));
        Assert.IsTrue(Uprawnienia.ZarzadzaniePracownikami(Rola.Administrator));
        Assert.IsTrue(Uprawnienia.PrzegladanieDziennika(Rola.Administrator));
    }

    [TestMethod]
    public void Wymagaj_PracownikUsuwa_RzucaBrakUprawnien()
    {
        var sesja = new SesjaUzytkownika();
        sesja.Rozpocznij(new Pracownik { PracownikID = 2, Login = "pkowalski", Rola = Rola.Pracownik });
        Assert.ThrowsExactly<BrakUprawnienException>(() => sesja.Wymagaj(Uprawnienia.Usuwanie, "usuwanie stacji"));
    }

    [TestMethod]
    public void Wymagaj_BezZalogowania_RzucaBrakUprawnien()
    {
        Assert.ThrowsExactly<BrakUprawnienException>(() => new SesjaUzytkownika().Wymagaj(Uprawnienia.ObslugaKlientow, "zapis klienta"));
    }
}

[TestClass]
public class BledyBazyTests
{
    [TestMethod]
    public void Opisz_KonfliktWspolbieznosci_MowiOOdswiezeniu()
    {
        var opis = BledyBazy.Opisz(new DbUpdateConcurrencyException("konflikt"));
        StringAssert.Contains(opis, "Odśwież");
    }

    [TestMethod]
    public void Opisz_RegulaBiznesowa_ZwracaJejKomunikat()
    {
        Assert.AreEqual("Kod zajęty.", BledyBazy.Opisz(new RegulaException("Kod zajęty.")));
    }
}
