using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace desktop_cshar.ViewModels;

public class BaseViewModel
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}