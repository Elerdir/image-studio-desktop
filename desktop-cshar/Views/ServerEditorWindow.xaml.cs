using System.Windows;
using desktop_cshar.Models;

namespace desktop_cshar;

public partial class ServerEditorWindow : Window
{
    public ServerProfile ServerProfile { get; private set; }

    public ServerEditorWindow(ServerProfile? existingServer = null)
    {
        InitializeComponent();

        ServerProfile = existingServer != null
            ? new ServerProfile
            {
                Id = existingServer.Id,
                Name = existingServer.Name,
                BaseUrl = existingServer.BaseUrl,
                Description = existingServer.Description,
                Category = existingServer.Category,
                IsDefault = existingServer.IsDefault,
                IsEnabled = existingServer.IsEnabled
            }
            : new ServerProfile();

        NameTextBox.Text = ServerProfile.Name;
        BaseUrlTextBox.Text = ServerProfile.BaseUrl;
        DescriptionTextBox.Text = ServerProfile.Description;
        CategoryTextBox.Text = ServerProfile.Category;
        IsEnabledCheckBox.IsChecked = ServerProfile.IsEnabled;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(BaseUrlTextBox.Text))
        {
            MessageBox.Show("Base URL is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ServerProfile.Name = NameTextBox.Text.Trim();
        ServerProfile.BaseUrl = BaseUrlTextBox.Text.Trim();
        ServerProfile.Description = DescriptionTextBox.Text.Trim();
        ServerProfile.Category = CategoryTextBox.Text.Trim();
        ServerProfile.IsEnabled = IsEnabledCheckBox.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}