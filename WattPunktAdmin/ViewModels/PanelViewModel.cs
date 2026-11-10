using System.Collections.ObjectModel;
using System.Windows.Input;
using WattPunktAdmin.Models;

namespace WattPunktAdmin.ViewModels;

public class PanelViewModel : ZakladkaViewModel
{
    private Podsumowanie? dane;

    public PanelViewModel(Uslugi u) : base("Panel", u)
    {
        OdswiezCommand = new AsyncRelayCommand(() => WykonajAsync(ZaladujAsync, "Odświeżono panel"));
    }

    public Podsumowanie? Dane { get => dane; private set { Ustaw(ref dane, value); OnPropertyChanged(nameof(Powitanie)); } }
    public ObservableCollection<StacjaRanking> NajlepszeStacje { get; } = [];
    public ObservableCollection<AwariaWiersz> OstatnieAwarie { get; } = [];
    public string Powitanie => $"Dzień dobry, {U.Sesja.Pracownik?.Imie}. Dane sieci WattPunkt na dziś:";
    public ICommand OdswiezCommand { get; }

    protected override async Task ZaladujAsync()
    {
        var p = await U.Panel.PodsumowanieAsync();
        Dane = p;
        NajlepszeStacje.Clear();
        foreach (var s in p.NajlepszeStacje) NajlepszeStacje.Add(s);
        OstatnieAwarie.Clear();
        foreach (var a in p.OstatnieAwarie) OstatnieAwarie.Add(a);
    }
}
