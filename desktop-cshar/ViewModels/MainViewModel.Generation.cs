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
    private async Task GenerateFromFinalPromptWithProgressAsync()
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(FinalPrompt))
            return;

        if (!ValidateGenerationParameters())
            return;

        if (!string.IsNullOrWhiteSpace(CurrentJobId))
        {
            MessageBox.Show("A job is already running.");
            return;
        }

        IsGeneratingWithProgress = true;
        GenerationProgress = 0;
        GenerationProgressText = "Starting...";

        try
        {
            var jobId = await _apiClientService.StartGenerateJobAsync(
                SelectedServer.BaseUrl,
                FinalPrompt,
                FinalNegativePrompt,
                GenerationWidth,
                GenerationHeight,
                GenerationSteps,
                GenerationGuidanceScale,
                GetEffectiveSeed(),
                SelectedModel?.Id,
                NumberOfImages
            );

            if (string.IsNullOrWhiteSpace(jobId))
            {
                MessageBox.Show("Nepodařilo se spustit job.");
                return;
            }

            CurrentJobId = jobId;
            await LoadJobsAsync();

            var status = await WaitForJobCompletionAsync(jobId);
            if (status == null)
                return;

            FinalPrompt = status.Prompt ?? FinalPrompt;
            FinalNegativePrompt = status.NegativePrompt ?? FinalNegativePrompt;

            var responseObject = new
            {
                generated_filenames = status.GeneratedFilenames,
                prompt = status.Prompt,
                original_prompt = status.OriginalPrompt,
                prompt_source_language = status.PromptSourceLanguage,
                prompt_was_translated = status.PromptWasTranslated,
                negative_prompt = status.NegativePrompt,
                seed = status.Seed,
                model_id = status.ModelId,
                status = status.Status,
                progress = status.Progress,
                message = status.Message
            };

            LastResponseJson = JsonSerializer.Serialize(responseObject, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            UpdatePromptTranslationPreviewFromJson(LastResponseJson);

            if (Application.Current.MainWindow is MainWindow jsonWindow)
            {
                jsonWindow.SetLastResponseJson(LastResponseJson);
                jsonWindow.SetFinalPrompt(FinalPrompt);
                jsonWindow.SetFinalNegativePrompt(FinalNegativePrompt);
            }

            var imageUrls = status.GeneratedFilenames
                .Select(f => $"{SelectedServer.BaseUrl.TrimEnd('/')}/images/{f}")
                .ToList();

            var downloadedFiles = await DownloadGeneratedImagesAsync(imageUrls);

            if (!downloadedFiles.Any())
            {
                MessageBox.Show("Obrázky byly vygenerovány, ale nepodařilo se je stáhnout.");
                return;
            }

            GeneratedImagePaths.Clear();
            foreach (var file in downloadedFiles)
            {
                GeneratedImagePaths.Add(file);
            }

            GeneratedImagePath = GeneratedImagePaths.FirstOrDefault();
            SelectedGeneratedGalleryImagePath = GeneratedImagePath;

            if (Application.Current.MainWindow is MainWindow previewWindow &&
                !string.IsNullOrWhiteSpace(GeneratedImagePath))
            {
                previewWindow.SetPreviewImage(GeneratedImagePath);
            }

            UpdateStatusUi("Generation completed", false);
        }
        finally
        {
            CurrentJobId = null;
            IsGeneratingWithProgress = false;
            GenerationProgress = 0;
            GenerationProgressText = string.Empty;
            await LoadJobsAsync();
        }
    }

    private async Task DeleteModelAsync()
    {
        if (SelectedServer == null || SelectedModel == null || string.IsNullOrWhiteSpace(SelectedModel.Id))
            return;

        var modelId = SelectedModel.Id;
        var modelName = string.IsNullOrWhiteSpace(SelectedModel.Name) ? modelId : SelectedModel.Name;

        var confirm = MessageBox.Show(
            $"Opravdu chceš odstranit model '{modelName}'?\n\nModel bude smazán z backendu i z registry.",
            "Odstranit model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        UpdateStatusUi("Removing model...", true);

        try
        {
            var success = await _apiClientService.UninstallModelAsync(
                SelectedServer.BaseUrl,
                modelId);

            if (!success)
            {
                MessageBox.Show(
                    "Model se nepodařilo odstranit.",
                    "Delete model",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await LoadModelsAsync();
            await LoadRuntimeInfoAsync();

            UpdateStatusUi("Model removed", false);

            MessageBox.Show(
                $"Model '{modelName}' byl odstraněn.",
                "Hotovo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Delete model error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UpdateStatusUi("Ready", false);
        }
    }

    private async Task CancelGenerationAsync()
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(CurrentJobId))
            return;

        try
        {
            await _apiClientService.CancelJobAsync(
                SelectedServer.BaseUrl,
                CurrentJobId);

            UpdateStatusUi("Cancelling job...", true);
            GenerationProgressText = "Cancelling...";
            await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Cancel error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task InstallModelAsync()
    {
        if (SelectedServer == null || SelectedModel == null)
            return;

        IsInstallingModel = true;
        InstallProgress = 0;
        UpdateStatusUi("Installing selected model...", true);

        try
        {
            _ = Task.Run(async () =>
            {
                while (IsInstallingModel && InstallProgress < 90)
                {
                    await Task.Delay(300);
                    InstallProgress += 5;
                }
            });

            var success = await _apiClientService.InstallModelAsync(
                SelectedServer.BaseUrl,
                SelectedModel.Id);

            IsInstallingModel = false;
            InstallProgress = 100;

            if (!success)
            {
                MessageBox.Show("Install failed.", "Model install", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadModelsAsync();
            await LoadRuntimeInfoAsync();
            UpdateStatusUi("Model installed", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Install error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsInstallingModel = false;
            InstallProgress = 0;
            UpdateStatusUi("Ready", false);
        }
    }

    private async Task InstallNewModelAsync()
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(NewModelId))
            return;

        IsInstallingModel = true;
        InstallProgress = 0;
        UpdateStatusUi("Installing new model...", true);

        try
        {
            var requestedModelId = NewModelId.Trim();

            _ = Task.Run(async () =>
            {
                while (IsInstallingModel && InstallProgress < 90)
                {
                    await Task.Delay(300);
                    InstallProgress += 5;
                }
            });

            var success = await _apiClientService.InstallModelAsync(
                SelectedServer.BaseUrl,
                requestedModelId);

            IsInstallingModel = false;
            InstallProgress = 100;

            if (!success)
            {
                MessageBox.Show("Install failed.", "Model install", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadModelsAsync();
            await LoadRuntimeInfoAsync();

            SelectedModel = AvailableModels.FirstOrDefault(m =>
                                string.Equals(m.Id, requestedModelId, StringComparison.OrdinalIgnoreCase))
                            ?? SelectedModel;

            NewModelId = string.Empty;
            UpdateStatusUi("Model installed", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Install error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsInstallingModel = false;
            InstallProgress = 0;
            UpdateStatusUi("Ready", false);
        }
    }

    private async Task RenameGenerationPresetAsync()
    {
        if (SelectedGenerationPreset == null)
            return;

        if (!CanDeleteSelectedPreset())
        {
            MessageBox.Show(
                "Default presets cannot be renamed.",
                "Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var oldName = SelectedGenerationPreset.Name;

        var dialog = new PresetNameWindow(oldName)
        {
            Owner = mainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        var newName = dialog.PresetName.Trim();

        if (string.IsNullOrWhiteSpace(newName))
            return;

        var defaultPresetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Fast",
            "Balanced",
            "Quality"
        };

        if (defaultPresetNames.Contains(newName))
        {
            MessageBox.Show(
                "This preset name is reserved by default presets.",
                "Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var userPresets = await _generationPresetService.LoadAsync();

        if (userPresets.Any(p =>
                !string.Equals(p.Name, oldName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "A preset with this name already exists.",
                "Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var presetToRename = userPresets.FirstOrDefault(p =>
            string.Equals(p.Name, oldName, StringComparison.OrdinalIgnoreCase));

        if (presetToRename == null)
        {
            MessageBox.Show(
                "Selected preset was not found.",
                "Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        presetToRename.Name = newName;

        await _generationPresetService.SaveAsync(userPresets);

        LoadGenerationPresets();

        SelectedGenerationPreset = GenerationPresets.FirstOrDefault(p =>
                                       string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase))
                                   ?? GenerationPresets.FirstOrDefault(p => p.Name == "Balanced")
                                   ?? GenerationPresets.FirstOrDefault();

        await SaveWorkspaceStateAsync();

        UpdateStatusUi($"Preset renamed: {oldName} → {newName}", false);
    }

    private async Task DeleteGenerationPresetAsync()
    {
        if (SelectedGenerationPreset == null)
            return;

        if (!CanDeleteSelectedPreset())
        {
            MessageBox.Show(
                "Default presets cannot be deleted.",
                "Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var presetToDelete = SelectedGenerationPreset;

        var confirm = MessageBox.Show(
            $"Do you really want to delete preset '{presetToDelete.Name}'?",
            "Delete Preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        var userPresets = await _generationPresetService.LoadAsync();

        userPresets = userPresets
            .Where(p => !string.Equals(p.Name, presetToDelete.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await _generationPresetService.SaveAsync(userPresets);

        LoadGenerationPresets();

        SelectedGenerationPreset = GenerationPresets.FirstOrDefault(p => p.Name == "Balanced")
                                   ?? GenerationPresets.FirstOrDefault();

        await SaveWorkspaceStateAsync();

        UpdateStatusUi($"Preset deleted: {presetToDelete.Name}", false);
    }

    private async Task StartNewSessionAsync()
    {
        SelectedHistoryItem = null;

        SelectedImagePath = string.Empty;
        SelectedGalleryImagePath = string.Empty;
        SelectedImagePaths.Clear();

        GeneratedImagePaths.Clear();
        GeneratedImagePath = string.Empty;
        SelectedGeneratedGalleryImagePath = string.Empty;

        PromptOverride = string.Empty;
        NegativePromptOverride = string.Empty;
        FinalPrompt = string.Empty;
        FinalNegativePrompt = string.Empty;
        MainPrompt = string.Empty;
        MainNegativePrompt = string.Empty;
        LastResponseJson = string.Empty;
        OriginalPromptPreview = string.Empty;
        PromptTranslationInfo = string.Empty;

        if (Application.Current.MainWindow is MainWindow window)
        {
            window.ClearAllWorkspaceUi();
        }

        await SaveWorkspaceStateAsync();
        UpdateStatusUi("New session started", false);
    }

    private void SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedImagePaths.Clear();

            foreach (var file in dialog.FileNames)
            {
                SelectedImagePaths.Add(file);
            }

            SelectedImagePath = SelectedImagePaths.FirstOrDefault() ?? string.Empty;
            SelectedGalleryImagePath = SelectedImagePath;

            if (Application.Current.MainWindow is MainWindow window &&
                !string.IsNullOrWhiteSpace(SelectedImagePath))
            {
                window.SetSourcePreviewImage(SelectedImagePath);
            }
        }
    }

    private async Task RandomizeSeedAsync()
    {
        GenerationSeed = Random.Shared.Next(0, int.MaxValue);
        UseRandomSeed = false;
        await SaveWorkspaceStateAsync();
        UpdateStatusUi($"Seed randomized: {GenerationSeed}", false);
    }

    private async Task AnalyzeAndGenerateAsync()
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(SelectedImagePath))
            return;

        if (!ValidateGenerationParameters())
            return;

        if (!string.IsNullOrWhiteSpace(CurrentJobId))
        {
            MessageBox.Show("A job is already running.");
            return;
        }

        IsGeneratingWithProgress = true;
        GenerationProgress = 0;
        GenerationProgressText = "Starting analysis...";

        UpdateStatusUi("Analyzuji a generuji obrázek...", true);

        try
        {
            var imagePaths = SelectedImagePaths.Any()
                ? SelectedImagePaths.ToList()
                : new List<string> { SelectedImagePath! };

            var jobId = await _apiClientService.StartAnalyzeAndGenerateJobAsync(
                imagePaths,
                SelectedServer.BaseUrl,
                PromptOverride,
                NegativePromptOverride,
                GenerationWidth,
                GenerationHeight,
                GenerationSteps,
                GenerationGuidanceScale,
                GetEffectiveSeed(),
                SelectedModel?.Id,
                NumberOfImages
            );

            if (string.IsNullOrWhiteSpace(jobId))
            {
                MessageBox.Show(
                    "Nepodařilo se spustit analyze+generate job.",
                    "Chyba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            CurrentJobId = jobId;

            var status = await WaitForJobCompletionAsync(jobId);
            if (status == null)
                return;

            FinalPrompt = status.Prompt ?? string.Empty;
            FinalNegativePrompt = status.NegativePrompt ?? string.Empty;

            var responseObject = new
            {
                source_filename = status.SourceFilename,
                generated_filenames = status.GeneratedFilenames,
                subject = status.Subject,
                hair = status.Hair,
                clothing = status.Clothing,
                environment = status.Environment,
                style = status.Style,
                prompt = status.Prompt,
                original_prompt = status.OriginalPrompt,
                prompt_source_language = status.PromptSourceLanguage,
                prompt_was_translated = status.PromptWasTranslated,
                negative_prompt = status.NegativePrompt,
                seed = status.Seed,
                model_id = status.ModelId,
                status = status.Status,
                progress = status.Progress,
                message = status.Message
            };

            LastResponseJson = JsonSerializer.Serialize(responseObject, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            UpdatePromptTranslationPreviewFromJson(LastResponseJson);

            if (Application.Current.MainWindow is MainWindow jsonWindow)
            {
                jsonWindow.SetLastResponseJson(LastResponseJson);
                jsonWindow.SetPromptOverride(PromptOverride);
                jsonWindow.SetNegativePromptOverride(NegativePromptOverride);
                jsonWindow.SetFinalPrompt(FinalPrompt);
                jsonWindow.SetFinalNegativePrompt(FinalNegativePrompt);
            }

            UpdateStatusUi("Stahuji vygenerované obrázky...", true);

            var imageUrls = status.GeneratedFilenames
                .Select(f => $"{SelectedServer.BaseUrl.TrimEnd('/')}/images/{f}")
                .ToList();

            var downloadedFiles = await DownloadGeneratedImagesAsync(imageUrls);

            if (!downloadedFiles.Any())
            {
                UpdateStatusUi("Stažení obrázků selhalo", false);

                MessageBox.Show(
                    "Obrázky byly vygenerovány, ale nepodařilo se je stáhnout.",
                    "Chyba načtení obrázků",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            GeneratedImagePaths.Clear();

            foreach (var file in downloadedFiles)
            {
                GeneratedImagePaths.Add(file);
            }

            GeneratedImagePath = GeneratedImagePaths.FirstOrDefault();
            SelectedGeneratedGalleryImagePath = GeneratedImagePath;

            if (Application.Current.MainWindow is MainWindow previewWindow)
            {
                if (!string.IsNullOrWhiteSpace(GeneratedImagePath))
                {
                    previewWindow.SetPreviewImage(GeneratedImagePath);
                }

                if (!string.IsNullOrWhiteSpace(SelectedImagePath) && File.Exists(SelectedImagePath))
                {
                    previewWindow.SetSourcePreviewImage(SelectedImagePath);
                }
            }

            UpdateStatusUi("Ukládám do historie...", true);

            var storedInputPath = CopyToAppStorage(SelectedImagePath!, AppPaths.GetTodayInputsDirectory());

            var storedGeneratedPaths = new List<string>();
            foreach (var generatedPath in GeneratedImagePaths)
            {
                if (!string.IsNullOrWhiteSpace(generatedPath) && File.Exists(generatedPath))
                {
                    storedGeneratedPaths.Add(CopyToAppStorage(generatedPath, AppPaths.GetTodayOutputsDirectory()));
                }
            }

            var storedOutputPath = storedGeneratedPaths.FirstOrDefault() ?? string.Empty;

            var historyItem = new HistoryItem
            {
                ServerId = SelectedServer.Id,
                ServerName = SelectedServer.Name,
                ModelId = SelectedModel?.Id ?? string.Empty,
                ModelName = SelectedModel?.Name ?? string.Empty,
                SourceImagePath = storedInputPath,
                GeneratedImagePath = storedOutputPath,
                GeneratedImagePathsJson = SerializeGeneratedImagePaths(storedGeneratedPaths),
                PromptOverride = PromptOverride,
                NegativePromptOverride = NegativePromptOverride,
                FinalPrompt = FinalPrompt,
                FinalNegativePrompt = FinalNegativePrompt,
                ResponseJson = LastResponseJson,
                OperationType = "AnalyzeAndGenerate",
                Seed = GetEffectiveSeed(),
                UsedRandomSeed = UseRandomSeed,
            };

            await _historyService.AddAsync(historyItem);
            HistoryItems.Insert(0, historyItem);
            ApplyHistoryFilter();

            await SaveWorkspaceStateAsync();

            UpdateStatusUi("Hotovo", false);

            MessageBox.Show(
                "Hotovo!",
                "Úspěch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            CurrentJobId = null;
            IsGeneratingWithProgress = false;
            GenerationProgress = 0;
            GenerationProgressText = string.Empty;

            if (IsBusy)
            {
                UpdateStatusUi("Ready", false);
            }
        }
    }

    private async Task GenerateFromFinalPromptAsync()
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(FinalPrompt))
            return;

        if (!ValidateGenerationParameters())
            return;

        UpdateStatusUi("Generuji z finálního promptu...", true);

        try
        {
            var result = await _apiClientService.GenerateAsync(
                SelectedServer.BaseUrl,
                FinalPrompt,
                FinalNegativePrompt,
                GenerationWidth,
                GenerationHeight,
                GenerationSteps,
                GenerationGuidanceScale,
                GetEffectiveSeed(),
                SelectedModel?.Id,
                NumberOfImages
            );

            LastResponseJson = result.ResponseJson ?? string.Empty;
            UpdatePromptTranslationPreviewFromJson(LastResponseJson);

            var (parsedPrompt, parsedNegativePrompt) = ExtractFinalPrompts(LastResponseJson);

            if (!string.IsNullOrWhiteSpace(parsedPrompt))
                FinalPrompt = parsedPrompt;

            if (!string.IsNullOrWhiteSpace(parsedNegativePrompt))
                FinalNegativePrompt = parsedNegativePrompt;

            if (Application.Current.MainWindow is MainWindow jsonWindow)
            {
                jsonWindow.SetLastResponseJson(LastResponseJson);
                jsonWindow.SetFinalPrompt(FinalPrompt);
                jsonWindow.SetFinalNegativePrompt(FinalNegativePrompt);
            }

            if (!result.Success)
            {
                UpdateStatusUi("Generování z finálního promptu selhalo", false);

                MessageBox.Show(
                    result.ErrorMessage ?? "Generování selhalo.",
                    "Chyba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            UpdateStatusUi("Stahuji vygenerované obrázky...", true);

            var imageUrls = ExtractGeneratedImageUrls(LastResponseJson, SelectedServer.BaseUrl);

            if (!imageUrls.Any() && !string.IsNullOrWhiteSpace(result.ImagePath))
            {
                imageUrls.Add(result.ImagePath);
            }

            var downloadedFiles = await DownloadGeneratedImagesAsync(imageUrls);

            if (!downloadedFiles.Any())
            {
                UpdateStatusUi("Stažení obrázků selhalo", false);

                MessageBox.Show(
                    "Obrázky byly vygenerovány, ale nepodařilo se je stáhnout.",
                    "Chyba načtení obrázků",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            GeneratedImagePaths.Clear();

            foreach (var file in downloadedFiles)
            {
                GeneratedImagePaths.Add(file);
            }

            GeneratedImagePath = GeneratedImagePaths.FirstOrDefault();
            SelectedGeneratedGalleryImagePath = GeneratedImagePath;

            if (Application.Current.MainWindow is MainWindow previewWindow)
            {
                if (!string.IsNullOrWhiteSpace(GeneratedImagePath))
                {
                    previewWindow.SetPreviewImage(GeneratedImagePath);
                }

                if (!string.IsNullOrWhiteSpace(SelectedImagePath) && File.Exists(SelectedImagePath))
                {
                    previewWindow.SetSourcePreviewImage(SelectedImagePath);
                }
            }

            UpdateStatusUi("Ukládám do historie...", true);

            string storedInputPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(SelectedImagePath) && File.Exists(SelectedImagePath))
            {
                storedInputPath = CopyToAppStorage(SelectedImagePath, AppPaths.GetTodayInputsDirectory());
            }

            var storedGeneratedPaths = new List<string>();
            foreach (var generatedPath in GeneratedImagePaths)
            {
                if (!string.IsNullOrWhiteSpace(generatedPath) && File.Exists(generatedPath))
                {
                    storedGeneratedPaths.Add(CopyToAppStorage(generatedPath, AppPaths.GetTodayOutputsDirectory()));
                }
            }

            var storedOutputPath = storedGeneratedPaths.FirstOrDefault() ?? string.Empty;

            var historyItem = new HistoryItem
            {
                ServerId = SelectedServer.Id,
                ServerName = SelectedServer.Name,
                ModelId = SelectedModel?.Id ?? string.Empty,
                ModelName = SelectedModel?.Name ?? string.Empty,
                SourceImagePath = storedInputPath,
                GeneratedImagePath = storedOutputPath,
                GeneratedImagePathsJson = SerializeGeneratedImagePaths(storedGeneratedPaths),
                PromptOverride = PromptOverride,
                NegativePromptOverride = NegativePromptOverride,
                FinalPrompt = FinalPrompt,
                FinalNegativePrompt = FinalNegativePrompt,
                ResponseJson = LastResponseJson,
                OperationType = "GenerateFromFinalPrompt",
                Seed = GetEffectiveSeed(),
                UsedRandomSeed = UseRandomSeed,
            };

            await _historyService.AddAsync(historyItem);
            HistoryItems.Insert(0, historyItem);
            ApplyHistoryFilter();

            await SaveWorkspaceStateAsync();

            UpdateStatusUi("Hotovo", false);

            MessageBox.Show(
                "Generování z finálního promptu dokončeno.",
                "Úspěch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            if (IsBusy)
            {
                UpdateStatusUi("Ready", false);
            }
        }
    }

    private async Task GenerateFromTextAsync()
    {
        if (SelectedServer == null)
            return;

        var promptToUse = IsTextToImageMode ? MainPrompt : FinalPrompt;
        var negativePromptToUse = IsTextToImageMode ? MainNegativePrompt : FinalNegativePrompt;

        if (string.IsNullOrWhiteSpace(promptToUse))
            return;

        if (!ValidateGenerationParameters())
            return;

        UpdateStatusUi("Generuji z textového promptu...", true);

        try
        {
            var result = await _apiClientService.GenerateAsync(
                SelectedServer.BaseUrl,
                promptToUse,
                negativePromptToUse,
                GenerationWidth,
                GenerationHeight,
                GenerationSteps,
                GenerationGuidanceScale,
                GetEffectiveSeed(),
                SelectedModel?.Id,
                NumberOfImages
            );

            LastResponseJson = result.ResponseJson ?? string.Empty;
            UpdatePromptTranslationPreviewFromJson(LastResponseJson);
            var (parsedPrompt, parsedNegativePrompt) = ExtractFinalPrompts(LastResponseJson);

            if (!string.IsNullOrWhiteSpace(parsedPrompt))
                FinalPrompt = parsedPrompt;

            if (!string.IsNullOrWhiteSpace(parsedNegativePrompt))
                FinalNegativePrompt = parsedNegativePrompt;

            if (Application.Current.MainWindow is MainWindow jsonWindow)
            {
                jsonWindow.SetLastResponseJson(LastResponseJson);
                jsonWindow.SetFinalPrompt(FinalPrompt);
                jsonWindow.SetFinalNegativePrompt(FinalNegativePrompt);
            }

            if (!result.Success)
            {
                UpdateStatusUi("Generování z textu selhalo", false);

                MessageBox.Show(
                    result.ErrorMessage ?? "Generování selhalo.",
                    "Chyba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            UpdateStatusUi("Stahuji vygenerované obrázky...", true);

            var imageUrls = ExtractGeneratedImageUrls(LastResponseJson, SelectedServer.BaseUrl);

            if (!imageUrls.Any() && !string.IsNullOrWhiteSpace(result.ImagePath))
            {
                imageUrls.Add(result.ImagePath);
            }

            var downloadedFiles = await DownloadGeneratedImagesAsync(imageUrls);

            if (!downloadedFiles.Any())
            {
                UpdateStatusUi("Stažení obrázků selhalo", false);

                MessageBox.Show(
                    "Obrázky byly vygenerovány, ale nepodařilo se je stáhnout.",
                    "Chyba načtení obrázků",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            GeneratedImagePaths.Clear();

            foreach (var file in downloadedFiles)
            {
                GeneratedImagePaths.Add(file);
            }

            GeneratedImagePath = GeneratedImagePaths.FirstOrDefault();
            SelectedGeneratedGalleryImagePath = GeneratedImagePath;

            if (Application.Current.MainWindow is MainWindow previewWindow)
            {
                if (!string.IsNullOrWhiteSpace(GeneratedImagePath))
                {
                    previewWindow.SetPreviewImage(GeneratedImagePath);
                }
            }

            UpdateStatusUi("Ukládám do historie...", true);

            var storedGeneratedPaths = new List<string>();
            foreach (var generatedPath in GeneratedImagePaths)
            {
                if (!string.IsNullOrWhiteSpace(generatedPath) && File.Exists(generatedPath))
                {
                    storedGeneratedPaths.Add(CopyToAppStorage(generatedPath, AppPaths.GetTodayOutputsDirectory()));
                }
            }

            var storedOutputPath = storedGeneratedPaths.FirstOrDefault() ?? string.Empty;

            var historyItem = new HistoryItem
            {
                ServerId = SelectedServer.Id,
                ServerName = SelectedServer.Name,
                ModelId = SelectedModel?.Id ?? string.Empty,
                ModelName = SelectedModel?.Name ?? string.Empty,
                SourceImagePath = string.Empty,
                GeneratedImagePath = storedOutputPath,
                GeneratedImagePathsJson = SerializeGeneratedImagePaths(storedGeneratedPaths),
                PromptOverride = PromptOverride,
                NegativePromptOverride = NegativePromptOverride,
                FinalPrompt = FinalPrompt,
                FinalNegativePrompt = FinalNegativePrompt,
                ResponseJson = LastResponseJson,
                OperationType = "GenerateFromText",
                Seed = GetEffectiveSeed(),
                UsedRandomSeed = UseRandomSeed,
            };

            await _historyService.AddAsync(historyItem);
            HistoryItems.Insert(0, historyItem);
            ApplyHistoryFilter();

            await SaveWorkspaceStateAsync();

            UpdateStatusUi("Hotovo", false);

            MessageBox.Show(
                "Generování z textu dokončeno.",
                "Úspěch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            if (IsBusy)
            {
                UpdateStatusUi("Ready", false);
            }
        }
    }

    private async Task ClearWorkspaceAsync()
    {
        SelectedHistoryItem = null;
        ClearWorkspaceState();
        await SaveWorkspaceStateAsync();
        UpdateStatusUi("Workspace cleared", false);
    }

    private async Task InstallSelectedDiscoveredModelAsync()
    {
        if (SelectedServer == null || SelectedDiscoverModel == null ||
            string.IsNullOrWhiteSpace(SelectedDiscoverModel.Id))
            return;

        UpdateStatusUi("Installing discovered model...", true);

        try
        {
            var success = await _apiClientService.InstallModelAsync(
                SelectedServer.BaseUrl,
                SelectedDiscoverModel.Id);

            if (!success)
            {
                MessageBox.Show(
                    "Install failed.",
                    "Model install",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await LoadModelsAsync();
            await LoadRuntimeInfoAsync();
            await LoadInstalledDiscoverModelsAsync();
            await LoadDiscoverModelsAsync();

            UpdateStatusUi("Model installed", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Install error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UpdateStatusUi("Ready", false);
        }
    }

    private async Task UninstallSelectedDiscoveredModelAsync()
    {
        if (SelectedServer == null || SelectedDiscoverModel == null ||
            string.IsNullOrWhiteSpace(SelectedDiscoverModel.Id))
            return;

        var modelId = SelectedDiscoverModel.Id;
        var modelName = string.IsNullOrWhiteSpace(SelectedDiscoverModel.Name)
            ? modelId
            : SelectedDiscoverModel.Name;

        var confirm = MessageBox.Show(
            $"Opravdu chceš odstranit model '{modelName}'?",
            "Odstranit model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        UpdateStatusUi("Removing discovered model...", true);

        try
        {
            var success = await _apiClientService.UninstallModelAsync(
                SelectedServer.BaseUrl,
                modelId);

            if (!success)
            {
                MessageBox.Show(
                    "Model se nepodařilo odstranit.",
                    "Delete model",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await LoadModelsAsync();
            await LoadRuntimeInfoAsync();
            await LoadInstalledDiscoverModelsAsync();
            await LoadDiscoverModelsAsync();

            UpdateStatusUi("Model removed", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Delete model error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UpdateStatusUi("Ready", false);
        }
    }

    private async Task CancelJobByIdAsync(string? jobId)
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(jobId))
            return;

        try
        {
            await _apiClientService.CancelJobAsync(
                SelectedServer.BaseUrl,
                jobId);

            if (string.Equals(CurrentJobId, jobId, StringComparison.OrdinalIgnoreCase))
            {
                UpdateStatusUi("Cancelling current job...", true);
                GenerationProgressText = "Cancelling...";
            }

            await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Cancel job error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}