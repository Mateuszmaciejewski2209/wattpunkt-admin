using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace WattPunktAdmin.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? nazwa = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nazwa));

    protected bool Ustaw<T>(ref T pole, T wartosc, [CallerMemberName] string? nazwa = null)
    {
        if (EqualityComparer<T>.Default.Equals(pole, wartosc)) return false;
        pole = wartosc;
        OnPropertyChanged(nazwa);
        return true;
    }
}

// ViewModel formularza z walidacja: atrybuty DataAnnotations i reguly dodatkowe, bledy przez INotifyDataErrorInfo
public abstract class WalidowanyViewModel : ViewModelBase, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> bledy = [];

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    public bool HasErrors => bledy.Count > 0;
    public IEnumerable GetErrors(string? wlasciwosc) =>
        wlasciwosc is not null && bledy.TryGetValue(wlasciwosc, out var lista) ? lista : Array.Empty<string>();

    // wszystkie bledy formularza, do podsumowania pod formularzem
    public IEnumerable<string> WszystkieBledy => bledy.Values.SelectMany(b => b).Distinct();

    protected bool UstawIWaliduj<T>(ref T pole, T wartosc, [CallerMemberName] string nazwa = "")
    {
        if (!Ustaw(ref pole, wartosc, nazwa)) return false;
        WalidujWlasciwosc(nazwa);
        foreach (var zalezna in WlasciwosciZalezne(nazwa)) WalidujWlasciwosc(zalezna);
        return true;
    }

    protected void WalidujWlasciwosc(string nazwa)
    {
        var wartosc = GetType().GetProperty(nazwa)?.GetValue(this);
        var wyniki = new List<ValidationResult>();
        Validator.TryValidateProperty(wartosc, new ValidationContext(this) { MemberName = nazwa }, wyniki);
        var lista = wyniki.Select(w => w.ErrorMessage ?? "Niepoprawna wartość.").Concat(RegulyDodatkowe(nazwa)).ToList();
        UstawBledy(nazwa, lista);
    }

    /// <summary>Waliduje wszystkie pola (np. przed zapisem) i zwraca true, gdy formularz jest poprawny.</summary>
    public bool WalidujWszystko()
    {
        var doSprawdzenia = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttributes<ValidationAttribute>().Any())
            .Select(p => p.Name).Concat(PolaZRegulami).Distinct();
        foreach (var nazwa in doSprawdzenia) WalidujWlasciwosc(nazwa);
        return !HasErrors;
    }

    public void UstawBladZewnetrzny(string wlasciwosc, string komunikat) => UstawBledy(wlasciwosc, [komunikat]);

    public void WyczyscBledy()
    {
        foreach (var nazwa in bledy.Keys.ToList()) UstawBledy(nazwa, []);
    }

    /// <summary>Reguly, ktorych nie da sie zapisac atrybutem (np. zaleznosci miedzy polami).</summary>
    protected virtual IEnumerable<string> RegulyDodatkowe(string wlasciwosc) => [];
    protected virtual IEnumerable<string> PolaZRegulami => [];
    protected virtual IEnumerable<string> WlasciwosciZalezne(string wlasciwosc) => [];

    private void UstawBledy(string nazwa, List<string> lista)
    {
        var bylyBledy = bledy.ContainsKey(nazwa);
        if (lista.Count == 0) bledy.Remove(nazwa); else bledy[nazwa] = lista;
        if (bylyBledy || lista.Count > 0)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nazwa));
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(WszystkieBledy));
        }
    }
}
