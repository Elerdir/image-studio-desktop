using System.IO;
using desktop_cshar.Infrastructure;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    #region Commands

    public RelayCommand AddServerCommand { get; private set; } = null!;
    public RelayCommand EditServerCommand { get; private set; } = null!;
    public RelayCommand SaveServersCommand { get; private set; } = null!;
    public RelayCommand DeleteServerCommand { get; private set; } = null!;
    public RelayCommand SetDefaultServerCommand { get; private set; } = null!;
    public RelayCommand TestConnectionCommand { get; private set; } = null!;
    public RelayCommand SelectImageCommand { get; private set; } = null!;
    public RelayCommand AnalyzeAndGenerateCommand { get; private set; } = null!;
    public RelayCommand GenerateFromFinalPromptCommand { get; private set; } = null!;
    public RelayCommand GenerateFromTextCommand { get; private set; } = null!;
    public RelayCommand ClearWorkspaceCommand { get; private set; } = null!;
    public RelayCommand CancelGenerationCommand { get; private set; } = null!;
    public RelayCommand RepeatFromHistoryCommand { get; private set; } = null!;
    public RelayCommand RepeatWithoutAnalyzeCommand { get; private set; } = null!;
    public RelayCommand ExportCommand { get; private set; } = null!;
    public RelayCommand DeleteHistoryItemCommand { get; private set; } = null!;
    public RelayCommand ClearHistoryFiltersCommand { get; private set; } = null!;
    public RelayCommand CopyFinalPromptCommand { get; private set; } = null!;
    public RelayCommand CopyJsonCommand { get; private set; } = null!;
    public RelayCommand ResetGenerationParametersCommand { get; private set; } = null!;
    public RelayCommand CopyFinalNegativePromptCommand { get; private set; } = null!;
    public RelayCommand RandomizeSeedCommand { get; private set; } = null!;
    public RelayCommand NewSessionCommand { get; private set; } = null!;
    public RelayCommand CopySeedCommand { get; private set; } = null!;
    public RelayCommand SaveGenerationPresetCommand { get; private set; } = null!;
    public RelayCommand DeleteGenerationPresetCommand { get; private set; } = null!;
    public RelayCommand RenameGenerationPresetCommand { get; private set; } = null!;
    public RelayCommand RefreshModelsCommand { get; private set; } = null!;
    public RelayCommand InstallModelCommand { get; private set; } = null!;
    public RelayCommand InstallNewModelCommand { get; private set; } = null!;
    public RelayCommand SaveGeneratedImageAsCommand { get; private set; } = null!;
    public RelayCommand OpenGeneratedImageFolderCommand { get; private set; } = null!;
    public RelayCommand DeleteModelCommand { get; private set; } = null!;
    public RelayCommand RefreshInstalledModelsCommand { get; private set; } = null!;
    public RelayCommand RefreshDiscoverModelsCommand { get; private set; } = null!;
    public RelayCommand InstallDiscoveredModelCommand { get; private set; } = null!;
    public RelayCommand UninstallDiscoveredModelCommand { get; private set; } = null!;
    public RelayCommand SelectModelCommand { get; private set; } = null!;
    public RelayCommand UnloadModelCommand { get; private set; } = null!;
    public RelayCommand UnloadAllModelsCommand { get; private set; } = null!;
    public RelayCommand RefreshJobsCommand { get; private set; } = null!;
    public RelayCommand CancelJobByIdCommand { get; private set; } = null!;
    
    #endregion

    private void InitializeCommands()
    {
        AddServerCommand = new RelayCommand(
            _ => AddServer(),
            _ => !IsBusy);

        EditServerCommand = new RelayCommand(
            _ => EditServer(),
            _ => !IsBusy && SelectedServer is not null);

        SaveServersCommand = new RelayCommand(
            async _ => await SaveServersAsync(),
            _ => !IsBusy);

        DeleteServerCommand = new RelayCommand(
            async _ => await DeleteSelectedServerAsync(),
            _ => !IsBusy && SelectedServer is not null);

        SetDefaultServerCommand = new RelayCommand(
            async _ => await SetDefaultServerAsync(),
            _ => !IsBusy && SelectedServer is not null);

        TestConnectionCommand = new RelayCommand(
            async _ => await TestConnectionAsync(),
            _ => !IsBusy && SelectedServer is not null);

        SelectImageCommand = new RelayCommand(
            _ => SelectImage(),
            _ => !IsBusy);

        AnalyzeAndGenerateCommand = new RelayCommand(
            async _ => await AnalyzeAndGenerateAsync(),
            _ => !IsBusy && SelectedServer is not null && !string.IsNullOrWhiteSpace(SelectedImagePath));

        GenerateFromFinalPromptCommand = new RelayCommand(
            async _ => await GenerateFromFinalPromptWithProgressAsync(),
            _ => !IsBusy && SelectedServer is not null && !string.IsNullOrWhiteSpace(FinalPrompt));

        GenerateFromTextCommand = new RelayCommand(
            async _ => await GenerateFromTextAsync(),
            _ => !IsBusy &&
                 SelectedServer is not null &&
                 !string.IsNullOrWhiteSpace(IsTextToImageMode ? MainPrompt : FinalPrompt));

        ClearWorkspaceCommand = new RelayCommand(
            async _ => await ClearWorkspaceAsync(),
            _ => !IsBusy);

        CancelGenerationCommand = new RelayCommand(
            async _ => await CancelGenerationAsync(),
            _ => IsGeneratingWithProgress && SelectedServer is not null && !string.IsNullOrWhiteSpace(CurrentJobId));

        RepeatFromHistoryCommand = new RelayCommand(
            async _ => await RepeatFromHistoryAsync(),
            _ => !IsBusy && SelectedHistoryItem is not null);

        RepeatWithoutAnalyzeCommand = new RelayCommand(
            async _ => await RepeatWithoutAnalyzeAsync(),
            _ => !IsBusy && SelectedHistoryItem is not null);

        ExportCommand = new RelayCommand(
            async _ => await ExportAsync(),
            _ => !IsBusy && SelectedHistoryItem is not null);

        DeleteHistoryItemCommand = new RelayCommand(
            async _ => await DeleteSelectedHistoryItemAsync(),
            _ => !IsBusy && SelectedHistoryItem is not null);

        ClearHistoryFiltersCommand = new RelayCommand(
            _ => ClearHistoryFilters(),
            _ => !IsBusy);

        CopyFinalPromptCommand = new RelayCommand(
            _ => CopyFinalPrompt(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(FinalPrompt));

        CopyJsonCommand = new RelayCommand(
            _ => CopyJson(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(LastResponseJson));

        ResetGenerationParametersCommand = new RelayCommand(
            async _ => await ResetGenerationParametersAsync(),
            _ => !IsBusy);

        CopyFinalNegativePromptCommand = new RelayCommand(
            _ => CopyFinalNegativePrompt(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(FinalNegativePrompt));

        RandomizeSeedCommand = new RelayCommand(
            async _ => await RandomizeSeedAsync(),
            _ => !IsBusy);

        NewSessionCommand = new RelayCommand(
            async _ => await StartNewSessionAsync(),
            _ => !IsBusy);

        CopySeedCommand = new RelayCommand(
            _ => CopySeed(),
            _ => !IsBusy && !UseRandomSeed);

        SaveGenerationPresetCommand = new RelayCommand(
            async _ => await SaveGenerationPresetAsync(),
            _ => !IsBusy);

        DeleteGenerationPresetCommand = new RelayCommand(
            async _ => await DeleteGenerationPresetAsync(),
            _ => !IsBusy && SelectedGenerationPreset is not null && CanDeleteSelectedPreset());

        RenameGenerationPresetCommand = new RelayCommand(
            async _ => await RenameGenerationPresetAsync(),
            _ => !IsBusy && SelectedGenerationPreset is not null && CanDeleteSelectedPreset());

        RefreshModelsCommand = new RelayCommand(
            async _ => await LoadModelsAsync(),
            _ => !IsBusy && SelectedServer is not null);

        InstallModelCommand = new RelayCommand(
            async _ => await InstallModelAsync(),
            _ => !IsBusy && SelectedServer is not null && SelectedModel is not null);

        InstallNewModelCommand = new RelayCommand(
            async _ => await InstallNewModelAsync(),
            _ => !IsBusy && SelectedServer is not null && !string.IsNullOrWhiteSpace(NewModelId));

        SaveGeneratedImageAsCommand = new RelayCommand(
            async _ => await SaveGeneratedImageAsAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(GeneratedImagePath) && File.Exists(GeneratedImagePath));

        OpenGeneratedImageFolderCommand = new RelayCommand(
            _ => OpenGeneratedImageFolder(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(GeneratedImagePath) && File.Exists(GeneratedImagePath));
        
        DeleteModelCommand = new RelayCommand(
            async _ => await DeleteModelAsync(),
            _ => !IsBusy &&
                 SelectedServer is not null &&
                 SelectedModel is not null &&
                 !string.IsNullOrWhiteSpace(SelectedModel.Id));
        
        RefreshInstalledModelsCommand = new RelayCommand(
            async _ => await LoadInstalledDiscoverModelsAsync(),
            _ => !IsBusy && SelectedServer is not null);

        RefreshDiscoverModelsCommand = new RelayCommand(
            async _ => await LoadDiscoverModelsAsync(),
            _ => !IsBusy && SelectedServer is not null);

        InstallDiscoveredModelCommand = new RelayCommand(
            async _ => await InstallSelectedDiscoveredModelAsync(),
            _ => !IsBusy &&
                 SelectedServer is not null &&
                 SelectedDiscoverModel is not null &&
                 !SelectedDiscoverModel.Installed);

        UninstallDiscoveredModelCommand = new RelayCommand(
            async _ => await UninstallSelectedDiscoveredModelAsync(),
            _ => !IsBusy &&
                 SelectedServer is not null &&
                 SelectedDiscoverModel is not null &&
                 SelectedDiscoverModel.Installed);
        
        SelectModelCommand = new RelayCommand(
            async parameter => await SelectModelByIdAsync(parameter as string),
            parameter => !IsBusy &&
                         !string.IsNullOrWhiteSpace(parameter as string) &&
                         AvailableModels.Any());
        
        UnloadModelCommand = new RelayCommand(
            async _ => await UnloadSelectedModelAsync(),
            _ => !IsBusy &&
                 SelectedServer is not null &&
                 SelectedDiscoverModel is not null);

        UnloadAllModelsCommand = new RelayCommand(
            async _ => await UnloadAllModelsAsync(),
            _ => !IsBusy && SelectedServer is not null);
        
        RefreshJobsCommand = new RelayCommand(
            async _ => await LoadJobsAsync(),
            _ => !IsBusy && SelectedServer is not null);
        
        CancelJobByIdCommand = new RelayCommand(
            async parameter => await CancelJobByIdAsync(parameter as string),
            parameter => !IsBusy &&
                         SelectedServer is not null &&
                         !string.IsNullOrWhiteSpace(parameter as string));
    }
}