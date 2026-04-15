using System.Windows;
using desktop_cshar.Models;
using desktop_cshar.Services;

namespace desktop_cshar;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public ThemeService ThemeService { get; } = new();
    public AppSettingsService AppSettingsService { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = await AppSettingsService.LoadAsync();
        ThemeService.ApplyTheme(settings.ThemeMode);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}