using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using desktop_cshar.Models;
using desktop_cshar.Services;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    private void LoadHistoryItemToWorkspace(HistoryItem item)
    {
        ClearWorkspaceState();

        PromptOverride = item.PromptOverride ?? string.Empty;
        NegativePromptOverride = item.NegativePromptOverride ?? string.Empty;
        FinalPrompt = item.FinalPrompt ?? string.Empty;
        FinalNegativePrompt = item.FinalNegativePrompt ?? string.Empty;
        LastResponseJson = item.ResponseJson ?? string.Empty;
        UpdatePromptTranslationPreviewFromJson(LastResponseJson);
        GeneratedImagePath = item.GeneratedImagePath;

        SelectedImagePaths.Clear();

        if (!string.IsNullOrWhiteSpace(item.SourceImagePath) && File.Exists(item.SourceImagePath))
        {
            SelectedImagePath = item.SourceImagePath;
            SelectedGalleryImagePath = item.SourceImagePath;
            SelectedImagePaths.Add(item.SourceImagePath);
        }
        else
        {
            SelectedImagePath = string.Empty;
            SelectedGalleryImagePath = string.Empty;
        }

        GeneratedImagePaths.Clear();

        var storedGeneratedPaths = DeserializeGeneratedImagePaths(item.GeneratedImagePathsJson)
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .ToList();

        if (!storedGeneratedPaths.Any() &&
            !string.IsNullOrWhiteSpace(item.GeneratedImagePath) &&
            File.Exists(item.GeneratedImagePath))
        {
            storedGeneratedPaths.Add(item.GeneratedImagePath);
        }

        foreach (var path in storedGeneratedPaths)
        {
            GeneratedImagePaths.Add(path);
        }

        GeneratedImagePath = GeneratedImagePaths.FirstOrDefault() ?? string.Empty;
        SelectedGeneratedGalleryImagePath = GeneratedImagePath;

        UseRandomSeed = item.UsedRandomSeed;

        if (!item.UsedRandomSeed && item.Seed.HasValue)
        {
            GenerationSeed = item.Seed.Value;
        }

        if (!string.IsNullOrWhiteSpace(item.ModelId))
        {
            var matchingModel = AvailableModels.FirstOrDefault(m =>
                string.Equals(m.Id, item.ModelId, StringComparison.OrdinalIgnoreCase));

            if (matchingModel != null)
            {
                SelectedModel = matchingModel;
                IsSelectedModelAvailable = true;
                ModelWarningMessage = string.Empty;
            }
            else
            {
                IsSelectedModelAvailable = false;
                ModelWarningMessage = $"Model '{item.ModelName}' není dostupný na tomto serveru.";
            }
        }
        else
        {
            IsSelectedModelAvailable = true;
            ModelWarningMessage = string.Empty;
        }

        if (Application.Current.MainWindow is MainWindow window)
        {
            window.SetPromptOverride(PromptOverride);
            window.SetNegativePromptOverride(NegativePromptOverride);
            window.SetFinalPrompt(FinalPrompt);
            window.SetFinalNegativePrompt(FinalNegativePrompt);
            window.SetLastResponseJson(LastResponseJson);

            if (!string.IsNullOrWhiteSpace(item.SourceImagePath) && File.Exists(item.SourceImagePath))
            {
                window.SetSourcePreviewImage(item.SourceImagePath);
                window.SetHistorySourcePreviewImage(item.SourceImagePath);
            }

            if (!string.IsNullOrWhiteSpace(item.GeneratedImagePath) && File.Exists(item.GeneratedImagePath))
            {
                window.SetPreviewImage(item.GeneratedImagePath);
                window.SetHistoryGeneratedPreviewImage(item.GeneratedImagePath);
            }
        }
    }

    private void ApplyHistoryFilter()
    {
        var search = (HistorySearchText ?? string.Empty).Trim().ToLowerInvariant();
        var operationFilter = SelectedHistoryOperationFilter ?? "All";

        var filtered = HistoryItems
            .Where(item =>
            {
                var matchesOperation =
                    operationFilter == "All" ||
                    string.Equals(item.OperationType, operationFilter, StringComparison.OrdinalIgnoreCase);

                if (!matchesOperation)
                    return false;

                if (string.IsNullOrWhiteSpace(search))
                    return true;

                return
                    (item.ServerName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.ModelName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.OperationType?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.PromptOverride?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.NegativePromptOverride?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.FinalPrompt?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.FinalNegativePrompt?.ToLowerInvariant().Contains(search) ?? false) ||
                    (item.ResponseJson?.ToLowerInvariant().Contains(search) ?? false);
            })
            .ToList();

        FilteredHistoryItems.Clear();
        foreach (var item in filtered)
        {
            FilteredHistoryItems.Add(item);
        }
    }

    private void ClearHistoryFilters()
    {
        HistorySearchText = string.Empty;
        SelectedHistoryOperationFilter = "All";
        ApplyHistoryFilter();
    }

    private async Task DeleteSelectedHistoryItemAsync()
    {
        if (SelectedHistoryItem == null)
            return;

        var itemToDelete = SelectedHistoryItem;

        var confirm = MessageBox.Show(
            $"Opravdu chceš smazat vybranou položku historie?\n\n" +
            $"{itemToDelete.CreatedAtLocalText} | {itemToDelete.ServerName} | {itemToDelete.ShortOperationType}",
            "Potvrzení smazání",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        UpdateStatusUi("Mažu položku historie...", true);

        try
        {
            if (!string.IsNullOrWhiteSpace(itemToDelete.SourceImagePath) &&
                File.Exists(itemToDelete.SourceImagePath))
            {
                File.Delete(itemToDelete.SourceImagePath);
            }

            if (!string.IsNullOrWhiteSpace(itemToDelete.GeneratedImagePath) &&
                File.Exists(itemToDelete.GeneratedImagePath))
            {
                File.Delete(itemToDelete.GeneratedImagePath);
            }

            var generatedPaths = DeserializeGeneratedImagePaths(itemToDelete.GeneratedImagePathsJson);

            foreach (var path in generatedPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // ignore jednotlivé chyby
                    }
                }
            }

            HistoryItems.Remove(itemToDelete);

            SelectedHistoryItem = null;
            ClearWorkspaceState();

            await _historyService.SaveAsync(HistoryItems.ToList());

            ApplyHistoryFilter();

            UpdateStatusUi("Položka historie smazána", false);
        }
        catch (Exception ex)
        {
            UpdateStatusUi("Mazání historie selhalo", false);

            MessageBox.Show(
                ex.ToString(),
                "Chyba mazání",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task RepeatFromHistoryAsync()
    {
        if (SelectedHistoryItem == null || SelectedServer == null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedHistoryItem.SourceImagePath) ||
            !File.Exists(SelectedHistoryItem.SourceImagePath))
        {
            MessageBox.Show("Vybraný záznam nemá vstupní obrázek pro opakovanou analýzu.");
            return;
        }

        if (!ValidateGenerationParameters())
            return;

        SelectedImagePaths.Clear();
        SelectedImagePaths.Add(SelectedHistoryItem.SourceImagePath);
        SelectedImagePath = SelectedHistoryItem.SourceImagePath;
        SelectedGalleryImagePath = SelectedHistoryItem.SourceImagePath;

        PromptOverride = SelectedHistoryItem.PromptOverride ?? string.Empty;
        NegativePromptOverride = SelectedHistoryItem.NegativePromptOverride ?? string.Empty;

        if (Application.Current.MainWindow is MainWindow window)
        {
            window.SetPromptOverride(PromptOverride);
            window.SetNegativePromptOverride(NegativePromptOverride);
            window.SetSourcePreviewImage(SelectedImagePath);
        }

        await AnalyzeAndGenerateAsync();
    }

    private async Task RepeatWithoutAnalyzeAsync()
    {
        if (SelectedHistoryItem == null || SelectedServer == null)
            return;

        if (!ValidateGenerationParameters())
            return;

        var finalPrompt = SelectedHistoryItem.FinalPrompt;
        var finalNegativePrompt = SelectedHistoryItem.FinalNegativePrompt;

        if (string.IsNullOrWhiteSpace(finalPrompt))
        {
            var extracted = ExtractFinalPrompts(SelectedHistoryItem.ResponseJson);
            finalPrompt = extracted.Prompt;
            finalNegativePrompt = extracted.NegativePrompt;
        }

        if (string.IsNullOrWhiteSpace(finalPrompt))
        {
            MessageBox.Show("Ve vybrané historii není dostupný finální prompt.");
            return;
        }

        UpdateStatusUi("Generuji bez analýzy...", true);

        try
        {
            var result = await _apiClientService.GenerateAsync(
                SelectedServer.BaseUrl,
                finalPrompt,
                finalNegativePrompt,
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
            FinalPrompt = finalPrompt;
            FinalNegativePrompt = finalNegativePrompt;

            if (Application.Current.MainWindow is MainWindow jsonWindow)
            {
                jsonWindow.SetLastResponseJson(LastResponseJson);
                jsonWindow.SetPromptOverride(SelectedHistoryItem.PromptOverride ?? string.Empty);
                jsonWindow.SetNegativePromptOverride(SelectedHistoryItem.NegativePromptOverride ?? string.Empty);
                jsonWindow.SetFinalPrompt(FinalPrompt);
                jsonWindow.SetFinalNegativePrompt(FinalNegativePrompt);
            }

            if (!result.Success)
            {
                UpdateStatusUi("Generování bez analýzy selhalo", false);

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

                if (!string.IsNullOrWhiteSpace(SelectedHistoryItem.SourceImagePath) &&
                    File.Exists(SelectedHistoryItem.SourceImagePath))
                {
                    previewWindow.SetSourcePreviewImage(SelectedHistoryItem.SourceImagePath);
                }
            }

            UpdateStatusUi("Ukládám do historie...", true);

            var storedInputPath = !string.IsNullOrWhiteSpace(SelectedHistoryItem.SourceImagePath) &&
                                  File.Exists(SelectedHistoryItem.SourceImagePath)
                ? CopyToAppStorage(SelectedHistoryItem.SourceImagePath, AppPaths.GetTodayInputsDirectory())
                : string.Empty;

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
                PromptOverride = SelectedHistoryItem.PromptOverride ?? string.Empty,
                NegativePromptOverride = SelectedHistoryItem.NegativePromptOverride ?? string.Empty,
                FinalPrompt = finalPrompt,
                FinalNegativePrompt = finalNegativePrompt,
                ResponseJson = LastResponseJson,
                OperationType = "GenerateOnly",
                Seed = GetEffectiveSeed(),
                UsedRandomSeed = UseRandomSeed,
            };

            await _historyService.AddAsync(historyItem);
            HistoryItems.Insert(0, historyItem);
            ApplyHistoryFilter();

            await SaveWorkspaceStateAsync();

            UpdateStatusUi("Hotovo", false);

            MessageBox.Show(
                "Generování bez analýzy dokončeno.",
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
}