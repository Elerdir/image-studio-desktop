using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using desktop_cshar.Models;
using desktop_cshar.Services;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    private async Task<GenerationJobStatus?> WaitForJobCompletionAsync(string jobId)
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(jobId))
            return null;

        while (true)
        {
            // 🔥 delay AŽ po prvním requestu (lepší UX)
            var status = await _apiClientService.GetJobStatusAsync(
                SelectedServer.BaseUrl,
                jobId);

            if (status == null)
            {
                await Task.Delay(1000);
                continue;
            }

            GenerationProgress = status.Progress;
            GenerationProgressText = string.IsNullOrWhiteSpace(status.Message)
                ? status.Status
                : status.Message;

            // 🔥 DEBUG (můžeš si nechat)
            // Console.WriteLine($"Polling job {jobId} | {status.Status} | {status.Progress}");

            if (status.Status == "completed")
                return status;

            if (status.Status == "failed")
            {
                MessageBox.Show(
                    status.Error ?? "Operace selhala.",
                    "Chyba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }

            if (status.Status == "cancelled")
            {
                MessageBox.Show(
                    "Operace byla zrušena.",
                    "Zrušeno",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return null;
            }

            // 🔥 KLÍČOVÉ — 1 sekunda
            await Task.Delay(1000);
        }
    }

    private string SerializeGeneratedImagePaths(IEnumerable<string> paths)
    {
        return JsonSerializer.Serialize(paths.ToList());
    }

    private List<string> DeserializeGeneratedImagePaths(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private bool CanDeleteSelectedPreset()
    {
        if (SelectedGenerationPreset == null)
            return false;

        var defaultPresetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Fast",
            "Balanced",
            "Quality"
        };

        return !defaultPresetNames.Contains(SelectedGenerationPreset.Name);
    }

    private async Task SaveGenerationPresetAsync()
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var dialog = new PresetNameWindow("")
        {
            Owner = mainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        var presetName = dialog.PresetName.Trim();

        if (string.IsNullOrWhiteSpace(presetName))
            return;

        var preset = new GenerationPreset
        {
            Name = presetName,
            Width = GenerationWidth,
            Height = GenerationHeight,
            Steps = GenerationSteps,
            GuidanceScale = GenerationGuidanceScale
        };

        var defaultPresetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Fast",
            "Balanced",
            "Quality"
        };

        if (defaultPresetNames.Contains(presetName))
        {
            MessageBox.Show(
                "This preset name is reserved by default presets.",
                "Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var existingUserPresets = await _generationPresetService.LoadAsync();
        var existing = existingUserPresets.FirstOrDefault(p =>
            string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Width = preset.Width;
            existing.Height = preset.Height;
            existing.Steps = preset.Steps;
            existing.GuidanceScale = preset.GuidanceScale;
        }
        else
        {
            existingUserPresets.Add(preset);
        }

        await _generationPresetService.SaveAsync(existingUserPresets);

        LoadGenerationPresets();

        UpdateStatusUi($"Preset saved: {presetName}", false);
    }

    private void CopySeed()
    {
        Clipboard.SetText(GenerationSeed.ToString());
        UpdateStatusUi($"Seed copied: {GenerationSeed}", false);
    }

    private async Task SaveWorkspaceStateAsync()
    {
        var state = new WorkspaceState
        {
            PromptOverride = PromptOverride,
            NegativePromptOverride = NegativePromptOverride,
            FinalPrompt = FinalPrompt,
            FinalNegativePrompt = FinalNegativePrompt,
            GenerationWidth = GenerationWidth,
            GenerationHeight = GenerationHeight,
            GenerationSteps = GenerationSteps,
            GenerationGuidanceScale = GenerationGuidanceScale,
            UseRandomSeed = UseRandomSeed,
            GenerationSeed = GenerationSeed,
            SelectedPresetName = SelectedGenerationPreset?.Name ?? string.Empty,
            SelectedServerId = SelectedServer?.Id,
            SelectedTabIndex = SelectedTabIndex,
            SelectedModelId = SelectedModel?.Id ?? string.Empty,
            SelectedWorkspaceMode = SelectedWorkspaceMode,
            MainPrompt = MainPrompt,
            MainNegativePrompt = MainNegativePrompt,
        };

        await _workspaceStateService.SaveAsync(state);
    }

    private (string Prompt, string NegativePrompt) ExtractFinalPrompts(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);

            var prompt = doc.RootElement.TryGetProperty("prompt", out var promptElement)
                ? promptElement.GetString() ?? string.Empty
                : string.Empty;

            var negativePrompt = doc.RootElement.TryGetProperty("negative_prompt", out var negativeElement)
                ? negativeElement.GetString() ?? string.Empty
                : string.Empty;

            return (prompt, negativePrompt);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private List<string> ExtractGeneratedImageUrls(string responseJson, string baseUrl)
    {
        var result = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("generated_filenames", out var filenamesElement) &&
                filenamesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in filenamesElement.EnumerateArray())
                {
                    var filename = item.GetString();
                    if (!string.IsNullOrWhiteSpace(filename))
                    {
                        result.Add($"{baseUrl.TrimEnd('/')}/images/{filename}");
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return result;
    }

    private async Task<List<string>> DownloadGeneratedImagesAsync(List<string> imageUrls)
    {
        var localPaths = new List<string>();

        foreach (var imageUrl in imageUrls)
        {
            var fileResult = await _apiClientService.DownloadImageToTempFileAsync(imageUrl);

            if (!string.IsNullOrWhiteSpace(fileResult.FilePath))
            {
                localPaths.Add(fileResult.FilePath);
            }
        }

        return localPaths;
    }

    private void UpdateStatusUi(string message, bool isBusy)
    {
        StatusMessage = message;
        IsBusy = isBusy;

        if (Application.Current.MainWindow is MainWindow window)
        {
            window.SetStatus(message, isBusy);
        }
    }

    private string CopyToAppStorage(string sourcePath, string targetDirectory)
    {
        AppPaths.EnsureDirectories();

        var extension = Path.GetExtension(sourcePath);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var targetPath = Path.Combine(targetDirectory, fileName);

        File.Copy(sourcePath, targetPath, overwrite: true);
        return targetPath;
    }

    private void ClearWorkspaceState()
    {
        SelectedImagePaths.Clear();
        SelectedGalleryImagePath = string.Empty;
        SelectedImagePath = string.Empty;

        GeneratedImagePaths.Clear();
        SelectedGeneratedGalleryImagePath = string.Empty;
        GeneratedImagePath = string.Empty;

        PromptOverride = string.Empty;
        NegativePromptOverride = string.Empty;
        FinalPrompt = string.Empty;
        FinalNegativePrompt = string.Empty;
        LastResponseJson = string.Empty;

        IsSelectedModelAvailable = true;
        ModelWarningMessage = string.Empty;
        
        OriginalPromptPreview = string.Empty;
        PromptTranslationInfo = string.Empty;

        if (Application.Current.MainWindow is MainWindow window)
        {
            window.ClearAllWorkspaceUi();
        }
    }

    private void CopyFinalPrompt()
    {
        if (string.IsNullOrWhiteSpace(FinalPrompt))
            return;

        Clipboard.SetText(FinalPrompt);
        UpdateStatusUi("Final prompt copied", false);
    }

    private void CopyJson()
    {
        if (string.IsNullOrWhiteSpace(LastResponseJson))
            return;

        Clipboard.SetText(LastResponseJson);
        UpdateStatusUi("Response JSON copied", false);
    }

    private void ApplyGenerationPreset(GenerationPreset preset)
    {
        GenerationWidth = preset.Width;
        GenerationHeight = preset.Height;
        GenerationSteps = preset.Steps;
        GenerationGuidanceScale = preset.GuidanceScale;

        UpdateStatusUi($"Preset applied: {preset.Name}", false);
    }

    private async Task ResetGenerationParametersAsync()
    {
        GenerationWidth = 512;
        GenerationHeight = 512;
        GenerationSteps = 12;
        GenerationGuidanceScale = 6.5;
        NumberOfImages = 1;
        UseRandomSeed = true;
        GenerationSeed = 42;

        SelectedGenerationPreset = null;

        await SaveWorkspaceStateAsync();
        UpdateStatusUi("Generation parameters reset", false);
    }

    private int? GetEffectiveSeed()
    {
        return UseRandomSeed ? null : GenerationSeed;
    }

    private bool ValidateGenerationParameters()
    {
        if (GenerationWidth < 64 || GenerationWidth > 2048 || GenerationWidth % 8 != 0)
        {
            MessageBox.Show(
                "Width musí být mezi 64 a 2048 a zároveň násobek 8.",
                "Neplatná hodnota",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (GenerationHeight < 64 || GenerationHeight > 2048 || GenerationHeight % 8 != 0)
        {
            MessageBox.Show(
                "Height musí být mezi 64 a 2048 a zároveň násobek 8.",
                "Neplatná hodnota",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (GenerationSteps < 1 || GenerationSteps > 150)
        {
            MessageBox.Show(
                "Steps musí být mezi 1 a 150.",
                "Neplatná hodnota",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (GenerationGuidanceScale < 0.1 || GenerationGuidanceScale > 30)
        {
            MessageBox.Show(
                "Guidance Scale musí být mezi 0.1 a 30.",
                "Neplatná hodnota",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (NumberOfImages < 1 || NumberOfImages > 16)
        {
            MessageBox.Show(
                "Počet obrázků musí být mezi 1 a 16.",
                "Neplatná hodnota",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (!UseRandomSeed && GenerationSeed < 0)
        {
            MessageBox.Show(
                "Seed musí být 0 nebo vyšší.",
                "Neplatná hodnota",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void CopyFinalNegativePrompt()
    {
        if (string.IsNullOrWhiteSpace(FinalNegativePrompt))
            return;

        Clipboard.SetText(FinalNegativePrompt);
        UpdateStatusUi("Final negative prompt copied", false);
    }

    private async Task SaveGeneratedImageAsAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedImagePath) || !File.Exists(GeneratedImagePath))
            return;

        var defaultFileName = $"generated_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(GeneratedImagePath)}";

        var dialog = new SaveFileDialog
        {
            Title = "Uložit vygenerovaný obrázek jako",
            Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|All files (*.*)|*.*",
            FileName = defaultFileName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await using var sourceStream = File.OpenRead(GeneratedImagePath);
            await using var targetStream = File.Create(dialog.FileName);
            await sourceStream.CopyToAsync(targetStream);

            UpdateStatusUi($"Obrázek uložen: {dialog.FileName}", false);

            MessageBox.Show(
                $"Obrázek byl uložen do:\n{dialog.FileName}",
                "Uloženo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Chyba při ukládání",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenGeneratedImageFolder()
    {
        if (string.IsNullOrWhiteSpace(GeneratedImagePath) || !File.Exists(GeneratedImagePath))
            return;

        var folder = Path.GetDirectoryName(GeneratedImagePath);
        if (string.IsNullOrWhiteSpace(folder))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folder}\"",
            UseShellExecute = true
        });
    }
    
    private async Task ApplyThemeAsync(AppThemeMode mode)
    {
        if (Application.Current is App app)
        {
            app.ThemeService.ApplyTheme(mode);
        }

        await _appSettingsService.SaveAsync(new Models.AppSettings
        {
            ThemeMode = mode
        });
    }
    
    private async Task SelectModelByIdAsync(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return;

        var matchingModel = AvailableModels.FirstOrDefault(m =>
            string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));

        if (matchingModel == null)
        {
            MessageBox.Show(
                $"Model '{modelId}' není aktuálně dostupný v seznamu načtených modelů.",
                "Model not available",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SelectedModel = matchingModel;
        IsSelectedModelAvailable = true;
        ModelWarningMessage = string.Empty;

        await SaveWorkspaceStateAsync();
        UpdateStatusUi($"Selected model: {matchingModel.Name}", false);
    }
    
    private void UpdateActiveModelFlags()
    {
        foreach (var m in InstalledDiscoverModels)
        {
            m.IsActive = SelectedModel != null &&
                         string.Equals(m.Id, SelectedModel.Id, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var m in DiscoverModelItems)
        {
            m.IsActive = SelectedModel != null &&
                         string.Equals(m.Id, SelectedModel.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
    
    private async Task UnloadSelectedModelAsync()
    {
        if (SelectedServer == null || SelectedDiscoverModel == null)
            return;

        UpdateStatusUi("Unloading model...", true);

        try
        {
            var success = await _apiClientService.UnloadModelAsync(
                SelectedServer.BaseUrl,
                SelectedDiscoverModel.Id,
                false);

            if (!success)
            {
                MessageBox.Show("Unload failed.");
                return;
            }

            await LoadRuntimeInfoAsync();

            UpdateStatusUi("Model unloaded", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Unload error");
        }
        finally
        {
            UpdateStatusUi("Ready", false);
        }
    }
    
    private async Task UnloadAllModelsAsync()
    {
        if (SelectedServer == null)
            return;

        var confirm = MessageBox.Show(
            "Unload all models from VRAM?",
            "Unload all",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        UpdateStatusUi("Unloading all models...", true);

        try
        {
            var success = await _apiClientService.UnloadModelAsync(
                SelectedServer.BaseUrl,
                null,
                true);

            if (!success)
            {
                MessageBox.Show("Unload all failed.");
                return;
            }

            await LoadRuntimeInfoAsync();

            UpdateStatusUi("All models unloaded", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Unload error");
        }
        finally
        {
            UpdateStatusUi("Ready", false);
        }
    }
    
    private void UpdatePromptTranslationPreviewFromJson(string responseJson)
    {
        OriginalPromptPreview = string.Empty;
        PromptTranslationInfo = string.Empty;

        if (string.IsNullOrWhiteSpace(responseJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var originalPrompt = root.TryGetProperty("original_prompt", out var originalPromptElement)
                ? originalPromptElement.GetString() ?? string.Empty
                : string.Empty;

            var finalPrompt = root.TryGetProperty("prompt", out var promptElement)
                ? promptElement.GetString() ?? string.Empty
                : string.Empty;

            var sourceLanguage = root.TryGetProperty("prompt_source_language", out var langElement)
                ? langElement.GetString() ?? string.Empty
                : string.Empty;

            var wasTranslated = root.TryGetProperty("prompt_was_translated", out var translatedElement) &&
                                translatedElement.ValueKind == JsonValueKind.True;

            if (!string.IsNullOrWhiteSpace(originalPrompt) &&
                !string.Equals(originalPrompt, finalPrompt, StringComparison.Ordinal))
            {
                OriginalPromptPreview = originalPrompt;
            }

            if (wasTranslated)
            {
                var languageText = string.IsNullOrWhiteSpace(sourceLanguage) ? "unknown" : sourceLanguage;
                PromptTranslationInfo = $"Prompt translated from: {languageText}";
            }
            else if (!string.IsNullOrWhiteSpace(sourceLanguage))
            {
                PromptTranslationInfo = $"Prompt language: {sourceLanguage}";
            }
        }
        catch
        {
            OriginalPromptPreview = string.Empty;
            PromptTranslationInfo = string.Empty;
        }
    }
    
    public List<WorkspaceModeItem> WorkspaceModes { get; } = new()
    {
        new() { Mode = WorkspaceMode.TextToImage, Display = "Text to Image" },
        new() { Mode = WorkspaceMode.ImageAnalyzeGenerate, Display = "Image Analyze + Generate" }
    };
}