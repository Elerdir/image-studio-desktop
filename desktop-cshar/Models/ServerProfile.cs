using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace desktop_cshar.Models;

public class ServerProfile : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    public Guid Id
    {
        get => _id;
        set
        {
            _id = value;
            OnPropertyChanged();
        }
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private string _baseUrl = string.Empty;
    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            _baseUrl = value;
            OnPropertyChanged();
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            OnPropertyChanged();
        }
    }

    private string _category = string.Empty;
    public string Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
        }
    }

    private bool _isDefault;
    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            _isDefault = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}