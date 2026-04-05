using System.Windows;

namespace desktop_cshar;

public partial class PresetNameWindow : Window
{
    public string PresetName { get; private set; } = string.Empty;

    public PresetNameWindow(string initialName = "")
    {
        InitializeComponent();
        PresetNameTextBox.Text = initialName;
        PresetNameTextBox.SelectAll();
        PresetNameTextBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                "Preset name is required.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PresetName = name;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}