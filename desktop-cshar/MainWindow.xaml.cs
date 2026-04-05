using System.IO;
using System.Windows;
using desktop_cshar.Services;
using desktop_cshar.Infrastructure;
using desktop_cshar.ViewModels;
using System.Windows.Media.Imaging;
namespace desktop_cshar;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ServerProfileService _serverProfileService = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    public void SetPreviewImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PreviewImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        PreviewImage.Source = bitmap;
    }

    public void SetSourcePreviewImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SourcePreviewImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        SourcePreviewImage.Source = bitmap;
    }

    public void SetHistorySourcePreviewImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HistorySourcePreviewImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        HistorySourcePreviewImage.Source = bitmap;
    }

    public void SetHistoryGeneratedPreviewImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            HistoryGeneratedPreviewImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        HistoryGeneratedPreviewImage.Source = bitmap;
    }

    public void SetLastResponseJson(string json)
    {
        ResponseJsonTextBox.Text = json ?? string.Empty;
    }

    public void SetPromptOverride(string prompt)
    {
        PromptOverrideTextBox.Text = prompt ?? string.Empty;
    }

    public void SetNegativePromptOverride(string negativePrompt)
    {
        NegativePromptOverrideTextBox.Text = negativePrompt ?? string.Empty;
    }

    public void SetFinalPrompt(string prompt)
    {
        FinalPromptTextBox.Text = prompt ?? string.Empty;
    }

    public void SetFinalNegativePrompt(string negativePrompt)
    {
        FinalNegativePromptTextBox.Text = negativePrompt ?? string.Empty;
    }

    public void SetStatus(string message, bool isBusy)
    {
        StatusMessageTextBlock.Text = message ?? string.Empty;
        BusyTextBlock.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ClearPreviewImage()
    {
        PreviewImage.Source = null;
    }

    public void ClearSourcePreviewImage()
    {
        SourcePreviewImage.Source = null;
    }

    public void ClearHistorySourcePreviewImage()
    {
        HistorySourcePreviewImage.Source = null;
    }

    public void ClearHistoryGeneratedPreviewImage()
    {
        HistoryGeneratedPreviewImage.Source = null;
    }

    public void ClearLastResponseJson()
    {
        ResponseJsonTextBox.Text = string.Empty;
    }

    public void ClearPromptOverride()
    {
        PromptOverrideTextBox.Text = string.Empty;
    }

    public void ClearNegativePromptOverride()
    {
        NegativePromptOverrideTextBox.Text = string.Empty;
    }

    public void ClearFinalPrompt()
    {
        FinalPromptTextBox.Text = string.Empty;
    }

    public void ClearFinalNegativePrompt()
    {
        FinalNegativePromptTextBox.Text = string.Empty;
    }

    public void ClearAllWorkspaceUi()
    {
        ClearPreviewImage();
        ClearSourcePreviewImage();
        ClearHistorySourcePreviewImage();
        ClearHistoryGeneratedPreviewImage();
        ClearLastResponseJson();
        ClearPromptOverride();
        ClearNegativePromptOverride();
        ClearFinalPrompt();
        ClearFinalNegativePrompt();
    }

    private void WorkspaceGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length > 0 && files.Any(IsSupportedImageFile))
            {
                e.Effects = DragDropEffects.Copy;
                SetDropHighlight();
                e.Handled = true;
                return;
            }
        }

        ResetDropHighlight();
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void WorkspaceGrid_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropHighlight();
    }

    private void WorkspaceGrid_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 0)
                return;

            var validFiles = files
                .Where(IsSupportedImageFile)
                .ToList();

            if (validFiles.Count == 0)
            {
                MessageBox.Show(
                    "Podporované jsou pouze soubory .png, .jpg a .jpeg.",
                    "Nepodporovaný soubor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (DataContext is MainViewModel vm)
            {
                vm.SelectedImagePaths.Clear();

                foreach (var file in validFiles)
                {
                    vm.SelectedImagePaths.Add(file);
                }

                vm.SelectedImagePath = vm.SelectedImagePaths.FirstOrDefault() ?? string.Empty;
                vm.SelectedGalleryImagePath = vm.SelectedImagePath;

                if (!string.IsNullOrWhiteSpace(vm.SelectedImagePath))
                {
                    SetSourcePreviewImage(vm.SelectedImagePath);
                    SetStatus($"Loaded {vm.SelectedImagePaths.Count} image(s) by drag & drop", false);
                }
            }
        }
        finally
        {
            ResetDropHighlight();
        }
    }

    private void SetDropHighlight()
    {
        DropOverlay.Visibility = Visibility.Visible;
    }

    private void ResetDropHighlight()
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg";
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var servers = await _serverProfileService.LoadAsync();

        if (!servers.Any())
        {
            servers.Add(new Models.ServerProfile
            {
                Name = "Local Realistic",
                BaseUrl = "http://127.0.0.1:8000",
                Description = "Lokální backend pro realistické generování",
                Category = "Realistic",
                IsDefault = true,
                IsEnabled = true
            });

            await _serverProfileService.SaveAsync(servers);
        }

        var hasAnyServer = await _serverProfileService.HasAnyServerAsync();
        
        /*MessageBox.Show(AppPaths.ServersFilePath);

        MessageBox.Show(
            hasAnyServer ? "Server byl nalezen nebo vytvořen." : "Žádný server není dostupný.",
            "Test serverů",
            MessageBoxButton.OK,
            MessageBoxImage.Information);*/
    }
}