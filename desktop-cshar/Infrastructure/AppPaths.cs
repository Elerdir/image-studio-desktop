using System.IO;

public static class AppPaths
{
    public static string AppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageStudio");

    // 👉 kompatibilita pro nový kód (WorkspaceStateService apod.)
    public static string BaseDirectory => AppDataRoot;

    public static string ServersFilePath =>
        Path.Combine(AppDataRoot, "servers.json");

    public static string HistoryFilePath =>
        Path.Combine(AppDataRoot, "history.json");

    public static string WorkspaceStateFilePath =>
        Path.Combine(AppDataRoot, "workspace-state.json");

    public static string InputsDirectory =>
        Path.Combine(AppDataRoot, "assets", "inputs");

    public static string OutputsDirectory =>
        Path.Combine(AppDataRoot, "assets", "outputs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(InputsDirectory);
        Directory.CreateDirectory(OutputsDirectory);
    }
    
    public static string GenerationPresetsFilePath =>
        Path.Combine(AppDataRoot, "generation-presets.json");
}