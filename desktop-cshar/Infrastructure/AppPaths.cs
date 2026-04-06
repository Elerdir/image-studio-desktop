using System;
using System.IO;

namespace desktop_cshar.Services;

public static class AppPaths
{
    public static string AppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageStudio");

    // kompatibilita pro nový kód (WorkspaceStateService apod.)
    public static string BaseDirectory => AppDataRoot;

    public static string ServersFilePath =>
        Path.Combine(AppDataRoot, "servers.json");

    public static string HistoryFilePath =>
        Path.Combine(AppDataRoot, "history.json");

    public static string WorkspaceStateFilePath =>
        Path.Combine(AppDataRoot, "workspace-state.json");

    public static string GenerationPresetsFilePath =>
        Path.Combine(AppDataRoot, "generation-presets.json");

    // základní asset složky
    public static string InputsDirectory =>
        Path.Combine(AppDataRoot, "assets", "inputs");

    public static string OutputsDirectory =>
        Path.Combine(AppDataRoot, "assets", "outputs");

    // denní podsložky
    public static string GetTodayInputsDirectory() =>
        Path.Combine(InputsDirectory, DateTime.Now.ToString("yyyy-MM-dd"));

    public static string GetTodayOutputsDirectory() =>
        Path.Combine(OutputsDirectory, DateTime.Now.ToString("yyyy-MM-dd"));

    // obecná varianta pro konkrétní datum, pokud ji budeš chtít časem použít
    public static string GetInputsDirectoryForDate(DateTime date) =>
        Path.Combine(InputsDirectory, date.ToString("yyyy-MM-dd"));

    public static string GetOutputsDirectoryForDate(DateTime date) =>
        Path.Combine(OutputsDirectory, date.ToString("yyyy-MM-dd"));

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(InputsDirectory);
        Directory.CreateDirectory(OutputsDirectory);

        // vytvoří i dnešní podsložky, aby nový kód mohl rovnou ukládat
        Directory.CreateDirectory(GetTodayInputsDirectory());
        Directory.CreateDirectory(GetTodayOutputsDirectory());
    }
}