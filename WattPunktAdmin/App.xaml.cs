using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using WattPunktAdmin.Data;
using WattPunktAdmin.Services;
using WattPunktAdmin.ViewModels;
using WattPunktAdmin.Widoki;

namespace WattPunktAdmin;

/// <summary>
/// Start programu: logowanie, ewentualna wymuszona zmiana hasla, okno glowne. Po wylogowaniu wraca do logowania.
/// </summary>
public partial class App : Application
{
    private IFabrykaKontekstu? fabryka;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // polskie formaty dat i liczb we wszystkich wiazaniach
        var pl = new CultureInfo("pl-PL");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = pl;
        var jezyk = XmlLanguage.GetLanguage(pl.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(jezyk));
        // elementy tekstowe (Run w TextBlock) maja wlasna wartosc domyslna jezyka
        FrameworkContentElement.LanguageProperty.OverrideMetadata(typeof(System.Windows.Documents.TextElement), new FrameworkPropertyMetadata(jezyk));

        // ostatnia linia obrony: blad, ktorego nie obsluzyl ViewModel, trafia do dziennika i nie zamyka programu
        DispatcherUnhandledException += (_, a) =>
        {
            Log.Zapisz(a.Exception, "nieobsłużony wyjątek");
            MessageBox.Show(BledyBazy.Opisz(a.Exception), "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            a.Handled = true;
        };

        try
        {
            fabryka = new FabrykaKontekstu(Ustawienia.Wczytaj().PolaczenieZBaza);
        }
        catch (Exception ex)
        {
            MessageBox.Show(BledyBazy.Opisz(ex), "WattPunkt Admin", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        PokazLogowanie();
    }

    private void PokazLogowanie()
    {
        var sesja = new SesjaUzytkownika();
        var auth = new AuthService(fabryka!, sesja);
        var logowanie = new LogowanieViewModel(auth);
        var oknoLogowania = new LogowanieWindow { DataContext = logowanie };
        logowanie.Zalogowano += (_, _) => oknoLogowania.DialogResult = true;
        if (oknoLogowania.ShowDialog() != true) { Shutdown(); return; }

        var okna = new OknaDialogowe();
        if (sesja.Pracownik!.WymagaZmianyHasla && !okna.ZmienHaslo(auth, wymuszona: true))
        {
            auth.Wyloguj();
            PokazLogowanie();
            return;
        }

        var glowny = new MainViewModel(new Uslugi(fabryka!, sesja, okna));
        var okno = new MainWindow { DataContext = glowny };
        var wylogowanie = false;
        glowny.Wylogowano += (_, _) => { wylogowanie = true; okno.Close(); };
        glowny.Zakonczono += (_, _) => okno.Close();
        okno.Closed += (_, _) => { if (wylogowanie) PokazLogowanie(); else Shutdown(); };
        MainWindow = okno;
        okno.Show();
    }
}
