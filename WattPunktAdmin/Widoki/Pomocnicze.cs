using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WattPunktAdmin.Services;
using WattPunktAdmin.ViewModels;

namespace WattPunktAdmin.Widoki;

// true -> Visible, false -> Collapsed (z Odwroc="True" na odwrot)
public class BoolNaWidocznoscKonwerter : IValueConverter
{
    public bool Odwroc { get; set; }
    public object Convert(object value, Type t, object? p, CultureInfo c) => (value is true) ^ Odwroc ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

// obiekt wybrany -> true (np. wlaczenie panelu szczegolow)
public class NieNullKonwerter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value is not null;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

// RadioButton powiazany z wartoscia tekstowa: zaznaczony, gdy wartosc rowna sie parametrowi
public class RownaSieKonwerter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => Equals(value?.ToString(), p?.ToString());
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => value is true ? p! : Binding.DoNothing;
}

// Haslo z PasswordBox do ViewModelu: PasswordBox.Password nie jest wlasciwoscia zaleznosci, wiec nie da sie go powiazac wprost
public static class PasswordBoxPomocnik
{
    public static readonly DependencyProperty HasloProperty = DependencyProperty.RegisterAttached(
        "Haslo", typeof(string), typeof(PasswordBoxPomocnik),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PoZmianieHasla));

    public static readonly DependencyProperty WlaczProperty = DependencyProperty.RegisterAttached(
        "Wlacz", typeof(bool), typeof(PasswordBoxPomocnik), new PropertyMetadata(false, PoWlaczeniu));

    private static readonly DependencyProperty AktualizujeProperty =
        DependencyProperty.RegisterAttached("Aktualizuje", typeof(bool), typeof(PasswordBoxPomocnik));

    public static string GetHaslo(DependencyObject d) => (string)d.GetValue(HasloProperty);
    public static void SetHaslo(DependencyObject d, string value) => d.SetValue(HasloProperty, value);
    public static bool GetWlacz(DependencyObject d) => (bool)d.GetValue(WlaczProperty);
    public static void SetWlacz(DependencyObject d, bool value) => d.SetValue(WlaczProperty, value);

    private static void PoWlaczeniu(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox pb) return;
        pb.PasswordChanged -= ZmianaWPolu;
        if (e.NewValue is true) pb.PasswordChanged += ZmianaWPolu;
    }

    // ViewModel wyczyscil haslo (np. po nieudanym logowaniu): przepisz do pola
    private static void PoZmianieHasla(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox pb && !(bool)pb.GetValue(AktualizujeProperty) && pb.Password != (string)e.NewValue)
            pb.Password = (string)e.NewValue;
    }

    private static void ZmianaWPolu(object sender, RoutedEventArgs e)
    {
        var pb = (PasswordBox)sender;
        pb.SetValue(AktualizujeProperty, true);
        SetHaslo(pb, pb.Password);
        pb.SetValue(AktualizujeProperty, false);
    }
}

public class OknaDialogowe : IOkna
{
    // wlascicielem okna dialogowego moze byc tylko widoczne okno (np. przy wymuszonej zmianie hasla okno logowania jest juz zamkniete)
    private static Window? Wlasciciel =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? (Application.Current.MainWindow is { IsVisible: true } glowne ? glowne : null);

    public bool Potwierdz(string tresc, string tytul = "Potwierdzenie") =>
        Pokaz(tresc, tytul, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    public void Informacja(string tresc, string tytul = "Informacja") => Pokaz(tresc, tytul, MessageBoxButton.OK, MessageBoxImage.Information);
    public void Ostrzezenie(string tresc, string tytul = "Uwaga") => Pokaz(tresc, tytul, MessageBoxButton.OK, MessageBoxImage.Warning);
    public void Blad(string tresc, string tytul = "Błąd") => Pokaz(tresc, tytul, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool ZmienHaslo(IAuthService auth, bool wymuszona)
    {
        var vm = new ZmianaHaslaViewModel(auth, wymuszona);
        var wlasciciel = Wlasciciel;                      // ustalony przed utworzeniem nowego okna
        var okno = new ZmianaHaslaWindow { DataContext = vm };
        if (wlasciciel is not null) okno.Owner = wlasciciel;
        else okno.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        vm.Zakonczono += (_, ok) => okno.DialogResult = ok;
        return okno.ShowDialog() == true;
    }

    private static MessageBoxResult Pokaz(string tresc, string tytul, MessageBoxButton przyciski, MessageBoxImage ikona) =>
        Wlasciciel is { } w ? MessageBox.Show(w, tresc, tytul, przyciski, ikona) : MessageBox.Show(tresc, tytul, przyciski, ikona);
}
