using System.Windows;
using desktop_cshar.Models;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    private void AddServer()
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var dialog = new ServerEditorWindow
        {
            Owner = mainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        var newServer = dialog.ServerProfile;

        if (!Servers.Any())
        {
            newServer.IsDefault = true;
        }

        Servers.Add(newServer);
        SelectedServer = newServer;
    }

    private void EditServer()
    {
        if (SelectedServer == null)
            return;

        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var dialog = new ServerEditorWindow(SelectedServer)
        {
            Owner = mainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        var edited = dialog.ServerProfile;

        SelectedServer.Name = edited.Name;
        SelectedServer.BaseUrl = edited.BaseUrl;
        SelectedServer.Description = edited.Description;
        SelectedServer.Category = edited.Category;
        SelectedServer.IsEnabled = edited.IsEnabled;
    }

    private async Task SaveServersAsync()
    {
        await _serverService.SaveAsync(Servers.ToList());

        MessageBox.Show(
            "Servery byly uloženy.",
            "Uloženo",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task DeleteSelectedServerAsync()
    {
        if (SelectedServer is null)
            return;

        var serverToDelete = SelectedServer;

        var confirm = MessageBox.Show(
            $"Opravdu chceš smazat server '{serverToDelete.Name}'?",
            "Potvrzení smazání",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        Servers.Remove(serverToDelete);

        if (serverToDelete.IsDefault && Servers.Any())
        {
            Servers[0].IsDefault = true;
        }

        SelectedServer = Servers.FirstOrDefault();

        await _serverService.SaveAsync(Servers.ToList());
    }

    private async Task SetDefaultServerAsync()
    {
        if (SelectedServer is null)
            return;

        foreach (var server in Servers)
        {
            server.IsDefault = false;
        }

        SelectedServer.IsDefault = true;

        await _serverService.SaveAsync(Servers.ToList());
    }

    private async Task TestConnectionAsync()
    {
        if (SelectedServer is null)
            return;

        UpdateStatusUi("Testuji připojení...", true);

        try
        {
            var success = await _apiClientService.TestConnectionAsync(SelectedServer);
            
            if (success)
            {
                await LoadRuntimeInfoAsync();
            }

            UpdateStatusUi(success ? "Server je dostupný" : "Server nedostupný", false);

            MessageBox.Show(
                success ? "Server je dostupný." : "Nepodařilo se připojit k serveru.",
                success ? "OK" : "Chyba",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);
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