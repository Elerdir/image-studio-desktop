using System.Windows;
using desktop_cshar.Models;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    private async Task LoadRuntimeInfoAsync()
    {
        if (SelectedServer == null)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RuntimeInfo = null;
            });
            return;
        }

        try
        {
            var runtime = await _apiClientService.GetRuntimeInfoAsync(SelectedServer.BaseUrl);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RuntimeInfo = runtime;

                // explicitně donutíme UI přepočítat i odvozené texty
                OnPropertyChanged(nameof(RuntimeInfo));
                OnPropertyChanged(nameof(RuntimeDeviceText));
                OnPropertyChanged(nameof(RuntimeGpuText));
                OnPropertyChanged(nameof(RuntimeMemoryText));
                OnPropertyChanged(nameof(RuntimeModelsText));
                OnPropertyChanged(nameof(RuntimeWarningText));
                OnPropertyChanged(nameof(RuntimeTorchText));
                OnPropertyChanged(nameof(RuntimeServerVersionText));
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RuntimeInfo = null;
                UpdateStatusUi($"Failed to load runtime info: {ex.Message}", false);
            });
        }
    }
    
    public async Task InitializeAfterStartupAsync()
    {
        await LoadRuntimeInfoAsync();
        await LoadModelsAsync();
        await LoadJobsAsync();
    }

    private async Task LoadModelsAsync()
    {
        if (SelectedServer == null)
            return;

        try
        {
            var models = await _apiClientService.GetModelsAsync(SelectedServer.BaseUrl);

            AvailableModels.Clear();

            foreach (var model in models)
            {
                AvailableModels.Add(model);
            }

            SelectedModel = AvailableModels.FirstOrDefault(m =>
                                !string.IsNullOrWhiteSpace(_pendingSelectedModelId) &&
                                string.Equals(m.Id, _pendingSelectedModelId, StringComparison.OrdinalIgnoreCase))
                            ?? AvailableModels.FirstOrDefault(m => m.Installed)
                            ?? AvailableModels.FirstOrDefault();

            _pendingSelectedModelId = null;

            IsSelectedModelAvailable = true;
            ModelWarningMessage = string.Empty;

            UpdateStatusUi("Models loaded", false);
        }
        catch (Exception ex)
        {
            AvailableModels.Clear();
            SelectedModel = null;
            UpdateStatusUi($"Failed to load models: {ex.Message}", false);
        }
    }
    
    private async void LoadAppSettingsAsync()
    {
        var settings = await _appSettingsService.LoadAsync();
        _selectedThemeMode = settings.ThemeMode;
        OnPropertyChanged(nameof(SelectedThemeMode));
    }

    private async void LoadWorkspaceStateAsync()
    {
        var state = await _workspaceStateService.LoadAsync();

        PromptOverride = state.PromptOverride;
        NegativePromptOverride = state.NegativePromptOverride;
        FinalPrompt = state.FinalPrompt;
        FinalNegativePrompt = state.FinalNegativePrompt;

        GenerationWidth = state.GenerationWidth;
        GenerationHeight = state.GenerationHeight;
        GenerationSteps = state.GenerationSteps;
        GenerationGuidanceScale = state.GenerationGuidanceScale;

        UseRandomSeed = state.UseRandomSeed;
        GenerationSeed = state.GenerationSeed;

        SelectedTabIndex = state.SelectedTabIndex;

        _pendingSelectedServerId = state.SelectedServerId;
        _pendingSelectedModelId = state.SelectedModelId;
        
        SelectedWorkspaceMode = state.SelectedWorkspaceMode;
        MainPrompt = state.MainPrompt;
        MainNegativePrompt = state.MainNegativePrompt;

        if (!string.IsNullOrWhiteSpace(state.SelectedPresetName))
        {
            var preset = GenerationPresets.FirstOrDefault(p => p.Name == state.SelectedPresetName);
            if (preset != null)
            {
                SelectedGenerationPreset = preset;
            }
        }

        if (Application.Current.MainWindow is MainWindow window)
        {
            window.SetPromptOverride(PromptOverride);
            window.SetNegativePromptOverride(NegativePromptOverride);
            window.SetFinalPrompt(FinalPrompt);
            window.SetFinalNegativePrompt(FinalNegativePrompt);
        }
    }

    private async void LoadGenerationPresets()
    {
        GenerationPresets.Clear();

        var defaultPresets = new List<GenerationPreset>
        {
            new() { Name = "Fast", Width = 512, Height = 512, Steps = 10, GuidanceScale = 5.5 },
            new() { Name = "Balanced", Width = 512, Height = 512, Steps = 20, GuidanceScale = 6.5 },
            new() { Name = "Quality", Width = 768, Height = 768, Steps = 30, GuidanceScale = 7.5 }
        };

        var userPresets = await _generationPresetService.LoadAsync();

        foreach (var preset in defaultPresets)
        {
            GenerationPresets.Add(preset);
        }

        foreach (var preset in userPresets)
        {
            if (!GenerationPresets.Any(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
            {
                GenerationPresets.Add(preset);
            }
        }

        SelectedGenerationPreset = GenerationPresets.FirstOrDefault(p => p.Name == "Balanced")
                                   ?? GenerationPresets.FirstOrDefault();
    }

    private async void LoadServersAsync()
    {
        var servers = await _serverService.LoadAsync();

        if (!servers.Any())
        {
            servers.Add(new ServerProfile
            {
                Name = "Local Realistic",
                BaseUrl = "http://127.0.0.1:8000",
                Description = "Lokální backend pro realistické generování",
                Category = "Realistic",
                IsDefault = true,
                IsEnabled = true
            });

            await _serverService.SaveAsync(servers);
        }

        Servers.Clear();
        foreach (var server in servers)
        {
            Servers.Add(server);
        }

        SelectedServer =
            Servers.FirstOrDefault(s => _pendingSelectedServerId.HasValue && s.Id == _pendingSelectedServerId.Value)
            ?? Servers.FirstOrDefault(s => s.IsDefault)
            ?? Servers.FirstOrDefault();
    }

    private async void LoadHistoryAsync()
    {
        var items = await _historyService.LoadAsync();

        HistoryItems.Clear();
        foreach (var item in items)
        {
            HistoryItems.Add(item);
        }

        ApplyHistoryFilter();
    }
    
    private async Task LoadInstalledDiscoverModelsAsync()
    {
        if (SelectedServer == null)
            return;

        try
        {
            var items = await _apiClientService.GetInstalledDiscoverModelsAsync(SelectedServer.BaseUrl);

            InstalledDiscoverModels.Clear();
            foreach (var item in items)
            {
                InstalledDiscoverModels.Add(item);
            }
            UpdateActiveModelFlags();
        }
        catch (Exception ex)
        {
            UpdateStatusUi($"Failed to load installed models: {ex.Message}", false);
        }
    }

    private async Task LoadDiscoverModelsAsync()
    {
        if (SelectedServer == null)
            return;

        try
        {
            var items = await _apiClientService.DiscoverModelsAsync(
                SelectedServer.BaseUrl,
                DiscoverSearchText,
                20,
                "text-to-image",
                true,
                DiscoverOnlySdxl);

            DiscoverModelItems.Clear();
            foreach (var item in items)
            {
                DiscoverModelItems.Add(item);
            }
            UpdateActiveModelFlags();
        }
        catch (Exception ex)
        {
            UpdateStatusUi($"Failed to discover models: {ex.Message}", false);
        }
    }
    
    private async Task LoadJobsAsync()
    {
        if (SelectedServer == null)
            return;

        try
        {
            var items = await _apiClientService.GetJobsAsync(SelectedServer.BaseUrl);

            JobItems.Clear();
            foreach (var item in items)
            {
                JobItems.Add(item);
            }

            OnPropertyChanged(nameof(JobsSummaryText));
        }
        catch (Exception ex)
        {
            UpdateStatusUi($"Failed to load jobs: {ex.Message}", false);
        }
    }
}