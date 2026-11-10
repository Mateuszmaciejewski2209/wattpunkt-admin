using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WattPunktAdmin.Widoki;

public partial class ZmianaHaslaWindow : Window
{
    public ZmianaHaslaWindow() => InitializeComponent();

    // przy wymuszonej zmianie przycisk anulowania oznacza wylogowanie
    public static readonly IValueConverter TekstAnuluj = new TekstAnulujKonwerter();

    private sealed class TekstAnulujKonwerter : IValueConverter
    {
        public object Convert(object value, Type t, object? p, CultureInfo c) => value is true ? "Wyloguj" : "Anuluj";
        public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
    }
}
