using Microsoft.Win32;
using System.Windows;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class ThemeService
{
    private const string ControlsPath = "Themes/Controls.xaml";
    private const string LightColorsPath = "Themes/Colors.Light.xaml";
    private const string DarkColorsPath = "Themes/Colors.Dark.xaml";

    public AppThemeMode CurrentMode { get; private set; } = AppThemeMode.Auto;

    public void ApplyTheme(AppThemeMode mode)
    {
        CurrentMode = mode;

        var effectiveMode = mode == AppThemeMode.Auto
            ? GetSystemTheme()
            : mode;

        var resources = Application.Current.Resources.MergedDictionaries;
        resources.Clear();

        resources.Add(new ResourceDictionary
        {
            Source = new Uri(
                effectiveMode == AppThemeMode.Dark ? DarkColorsPath : LightColorsPath,
                UriKind.Relative)
        });

        resources.Add(new ResourceDictionary
        {
            Source = new Uri(ControlsPath, UriKind.Relative)
        });
    }

    public AppThemeMode GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var value = key?.GetValue("AppsUseLightTheme");

            if (value is int intValue)
                return intValue == 0 ? AppThemeMode.Dark : AppThemeMode.Light;
        }
        catch
        {
            // ignore
        }

        return AppThemeMode.Light;
    }
}