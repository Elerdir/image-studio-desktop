using System;
using System.Windows;
using Microsoft.Win32;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    private async Task ExportAsync()
    {
        if (SelectedHistoryItem == null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "ZIP archive (*.zip)|*.zip",
            FileName = $"image_export_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != true)
            return;

        UpdateStatusUi("Exportuji...", true);

        try
        {
            await _exportService.ExportAsync(SelectedHistoryItem, dialog.FileName);

            UpdateStatusUi("Export hotov", false);

            MessageBox.Show(
                $"Export uložen:\n{dialog.FileName}",
                "Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UpdateStatusUi("Export selhal", false);

            MessageBox.Show(
                ex.ToString(),
                "Export chyba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (IsBusy)
            {
                UpdateStatusUi("Ready", false);
            }
        }
    }
}