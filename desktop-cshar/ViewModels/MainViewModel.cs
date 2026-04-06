using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using desktop_cshar.Infrastructure;
using desktop_cshar.Models;
using desktop_cshar.Services;

namespace desktop_cshar.ViewModels;

public class MainViewModel : BaseViewModel
{
    #region Services

    private readonly ServerProfileService _serverService = new();
    private readonly ApiClientService _apiClientService = new();
    private readonly HistoryService _historyService = new();
    private readonly ExportService _exportService = new();
    private readonly WorkspaceStateService _workspaceStateService = new();
    private readonly GenerationPresetService _generationPresetService = new();

    private Guid? _pendingSelectedServerId;
    private string? _pendingSelectedModelId;

    #endregion

    #region Collections

    public ObservableCollection<ServerProfile> Servers { get; } = new();
    public ObservableCollection<HistoryItem> HistoryItems { get; } = new();
    public ObservableCollection<HistoryItem> FilteredHistoryItems { get; } = new();
    public ObservableCollection<GenerationPreset> GenerationPresets { get; } = new();
    public ObservableCollection<ModelInfo> AvailableModels { get; } = new();

    public ObservableCollection<string> SelectedImagePaths { get; } = new();
    public ObservableCollection<string> GeneratedImagePaths { get; } = new();

    #endregion

    #region Selected Items

    private ServerProfile? _selectedServer;

    public ServerProfile? SelectedServer
    {
        get => _selectedServer;
        set
        {
            _selectedServer = value;
            OnPropertyChanged();

            IsSelectedModelAvailable = true;
            ModelWarningMessage = string.Empty;

            _ = LoadModelsAsync();
            _ = SaveWorkspaceStateAsync();

            DeleteServerCommand.RaiseCanExecuteChanged();
            EditServerCommand.RaiseCanExecuteChanged();
            SetDefaultServerCommand.RaiseCanExecuteChanged();
            TestConnectionCommand.RaiseCanExecuteChanged();
            AnalyzeAndGenerateCommand.RaiseCanExecuteChanged();
            GenerateFromFinalPromptCommand.RaiseCanExecuteChanged();
            GenerateFromTextCommand.RaiseCanExecuteChanged();
            RefreshModelsCommand.RaiseCanExecuteChanged();
            InstallModelCommand.RaiseCanExecuteChanged();
            InstallNewModelCommand.RaiseCanExecuteChanged();
        }
    }

    private ModelInfo? _selectedModel;

    public ModelInfo? SelectedModel
    {
        get => _selectedModel;
        set
        {
            _selectedModel = value;
            OnPropertyChanged();

            _ = SaveWorkspaceStateAsync();
            InstallModelCommand.RaiseCanExecuteChanged();
        }
    }

    private HistoryItem? _selectedHistoryItem;

    public HistoryItem? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set
        {
            _selectedHistoryItem = value;
            OnPropertyChanged();

            RepeatFromHistoryCommand.RaiseCanExecuteChanged();
            RepeatWithoutAnalyzeCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            DeleteHistoryItemCommand.RaiseCanExecuteChanged();

            if (_selectedHistoryItem == null)
                return;

            LoadHistoryItemToWorkspace(_selectedHistoryItem);
        }
    }

    private string? _selectedImagePath;

    public string? SelectedImagePath
    {
        get => _selectedImagePath;
        set
        {
            _selectedImagePath = value;
            OnPropertyChanged();
            AnalyzeAndGenerateCommand.RaiseCanExecuteChanged();
        }
    }

    private string? _selectedGalleryImagePath;

    public string? SelectedGalleryImagePath
    {
        get => _selectedGalleryImagePath;
        set
        {
            _selectedGalleryImagePath = value;
            OnPropertyChanged();

            if (!string.IsNullOrWhiteSpace(_selectedGalleryImagePath) &&
                File.Exists(_selectedGalleryImagePath) &&
                Application.Current.MainWindow is MainWindow window)
            {
                window.SetSourcePreviewImage(_selectedGalleryImagePath);
            }
        }
    }

    private string? _selectedGeneratedGalleryImagePath;

    public string? SelectedGeneratedGalleryImagePath
    {
        get => _selectedGeneratedGalleryImagePath;
        set
        {
            _selectedGeneratedGalleryImagePath = value;
            OnPropertyChanged();

            if (!string.IsNullOrWhiteSpace(_selectedGeneratedGalleryImagePath) &&
                File.Exists(_selectedGeneratedGalleryImagePath) &&
                Application.Current.MainWindow is MainWindow window)
            {
                window.SetPreviewImage(_selectedGeneratedGalleryImagePath);
            }
        }
    }

    #endregion

    #region Prompt Properties

    private bool _isGeneratingWithProgress;

    public bool IsGeneratingWithProgress
    {
        get => _isGeneratingWithProgress;
        set
        {
            _isGeneratingWithProgress = value;
            OnPropertyChanged();
        }
    }

    private int _generationProgress;

    public int GenerationProgress
    {
        get => _generationProgress;
        set
        {
            _generationProgress = value;
            OnPropertyChanged();
        }
    }

    private string _generationProgressText = string.Empty;

    public string GenerationProgressText
    {
        get => _generationProgressText;
        set
        {
            _generationProgressText = value;
            OnPropertyChanged();
        }
    }

    private int _selectedTabIndex;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged();

            _ = SaveWorkspaceStateAsync();
        }
    }

    private string _promptOverride = string.Empty;

    public string PromptOverride
    {
        get => _promptOverride;
        set
        {
            _promptOverride = value;
            OnPropertyChanged();
        }
    }

    private string _negativePromptOverride = string.Empty;

    public string NegativePromptOverride
    {
        get => _negativePromptOverride;
        set
        {
            _negativePromptOverride = value;
            OnPropertyChanged();
        }
    }

    private string _finalPrompt = string.Empty;

    public string FinalPrompt
    {
        get => _finalPrompt;
        set
        {
            _finalPrompt = value;
            OnPropertyChanged();
            GenerateFromFinalPromptCommand.RaiseCanExecuteChanged();
            GenerateFromTextCommand.RaiseCanExecuteChanged();
            CopyFinalPromptCommand.RaiseCanExecuteChanged();
        }
    }

    private string _finalNegativePrompt = string.Empty;

    public string FinalNegativePrompt
    {
        get => _finalNegativePrompt;
        set
        {
            _finalNegativePrompt = value;
            OnPropertyChanged();
            CopyFinalNegativePromptCommand.RaiseCanExecuteChanged();
        }
    }

    private string _newModelId = string.Empty;

    public string NewModelId
    {
        get => _newModelId;
        set
        {
            _newModelId = value;
            OnPropertyChanged();
            InstallNewModelCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region Generation Parameters

    private int _generationWidth = 512;

    public int GenerationWidth
    {
        get => _generationWidth;
        set
        {
            _generationWidth = value;
            OnPropertyChanged();
        }
    }

    private int _generationHeight = 512;

    public int GenerationHeight
    {
        get => _generationHeight;
        set
        {
            _generationHeight = value;
            OnPropertyChanged();
        }
    }

    private int _generationSteps = 12;

    public int GenerationSteps
    {
        get => _generationSteps;
        set
        {
            _generationSteps = value;
            OnPropertyChanged();
        }
    }

    private double _generationGuidanceScale = 6.5;

    public double GenerationGuidanceScale
    {
        get => _generationGuidanceScale;
        set
        {
            _generationGuidanceScale = value;
            OnPropertyChanged();
        }
    }

    private int _numberOfImages = 1;

    public int NumberOfImages
    {
        get => _numberOfImages;
        set
        {
            _numberOfImages = value < 1 ? 1 : value;
            OnPropertyChanged();
        }
    }

    private bool _useRandomSeed = true;

    public bool UseRandomSeed
    {
        get => _useRandomSeed;
        set
        {
            _useRandomSeed = value;
            OnPropertyChanged();
            CopySeedCommand.RaiseCanExecuteChanged();
        }
    }

    private int _generationSeed = 42;

    public int GenerationSeed
    {
        get => _generationSeed;
        set
        {
            _generationSeed = value;
            OnPropertyChanged();
            CopySeedCommand.RaiseCanExecuteChanged();
        }
    }

    private GenerationPreset? _selectedGenerationPreset;

    public GenerationPreset? SelectedGenerationPreset
    {
        get => _selectedGenerationPreset;
        set
        {
            _selectedGenerationPreset = value;
            OnPropertyChanged();

            if (_selectedGenerationPreset != null)
            {
                ApplyGenerationPreset(_selectedGenerationPreset);
            }

            DeleteGenerationPresetCommand.RaiseCanExecuteChanged();
            RenameGenerationPresetCommand.RaiseCanExecuteChanged();
            _ = SaveWorkspaceStateAsync();
        }
    }

    #endregion

    #region History Filter Properties

    private string _historySearchText = string.Empty;

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            _historySearchText = value;
            OnPropertyChanged();
            ApplyHistoryFilter();
        }
    }

    public List<string> HistoryOperationFilters { get; } = new()
    {
        "All",
        "AnalyzeAndGenerate",
        "GenerateOnly",
        "GenerateFromFinalPrompt",
        "GenerateFromText"
    };

    private string _selectedHistoryOperationFilter = "All";

    public string SelectedHistoryOperationFilter
    {
        get => _selectedHistoryOperationFilter;
        set
        {
            _selectedHistoryOperationFilter = value;
            OnPropertyChanged();
            ApplyHistoryFilter();
        }
    }

    #endregion

    #region Output / Status Properties

    private string _lastResponseJson = string.Empty;

    public string LastResponseJson
    {
        get => _lastResponseJson;
        set
        {
            _lastResponseJson = value;
            OnPropertyChanged();
            CopyJsonCommand.RaiseCanExecuteChanged();
        }
    }

    private string? _generatedImagePath;

    public string? GeneratedImagePath
    {
        get => _generatedImagePath;
        set
        {
            _generatedImagePath = value;
            OnPropertyChanged();
            SaveGeneratedImageAsCommand.RaiseCanExecuteChanged();
            OpenGeneratedImageFolderCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();

            AddServerCommand.RaiseCanExecuteChanged();
            EditServerCommand.RaiseCanExecuteChanged();
            SaveServersCommand.RaiseCanExecuteChanged();
            DeleteServerCommand.RaiseCanExecuteChanged();
            SetDefaultServerCommand.RaiseCanExecuteChanged();
            TestConnectionCommand.RaiseCanExecuteChanged();
            SelectImageCommand.RaiseCanExecuteChanged();
            AnalyzeAndGenerateCommand.RaiseCanExecuteChanged();
            RepeatFromHistoryCommand.RaiseCanExecuteChanged();
            RepeatWithoutAnalyzeCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            DeleteHistoryItemCommand.RaiseCanExecuteChanged();
            ClearHistoryFiltersCommand.RaiseCanExecuteChanged();
            ClearWorkspaceCommand.RaiseCanExecuteChanged();
            GenerateFromFinalPromptCommand.RaiseCanExecuteChanged();
            GenerateFromTextCommand.RaiseCanExecuteChanged();
            CopyFinalPromptCommand.RaiseCanExecuteChanged();
            CopyJsonCommand.RaiseCanExecuteChanged();
            ResetGenerationParametersCommand.RaiseCanExecuteChanged();
            CopyFinalNegativePromptCommand.RaiseCanExecuteChanged();
            RandomizeSeedCommand.RaiseCanExecuteChanged();
            NewSessionCommand.RaiseCanExecuteChanged();
            CopySeedCommand.RaiseCanExecuteChanged();
            SaveGenerationPresetCommand.RaiseCanExecuteChanged();
            DeleteGenerationPresetCommand.RaiseCanExecuteChanged();
            RenameGenerationPresetCommand.RaiseCanExecuteChanged();
            RefreshModelsCommand.RaiseCanExecuteChanged();
            InstallModelCommand.RaiseCanExecuteChanged();
            InstallNewModelCommand.RaiseCanExecuteChanged();
            SaveGeneratedImageAsCommand.RaiseCanExecuteChanged();
            OpenGeneratedImageFolderCommand.RaiseCanExecuteChanged();
        }
    }

    private string _statusMessage = "Ready";

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private bool _isSelectedModelAvailable = true;

    public bool IsSelectedModelAvailable
    {
        get => _isSelectedModelAvailable;
        set
        {
            _isSelectedModelAvailable = value;
            OnPropertyChanged();
        }
    }

    private string _modelWarningMessage = string.Empty;

    public string ModelWarningMessage
    {
        get => _modelWarningMessage;
        set
        {
            _modelWarningMessage = value;
            OnPropertyChanged();
        }
    }

    private bool _isInstallingModel;

    public bool IsInstallingModel
    {
        get => _isInstallingModel;
        set
        {
            _isInstallingModel = value;
            OnPropertyChanged();
        }
    }

    private double _installProgress;

    public double InstallProgress
    {
        get => _installProgress;
        set
        {
            _installProgress = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Commands

    public RelayCommand AddServerCommand { get; }
    public RelayCommand EditServerCommand { get; }
    public RelayCommand SaveServersCommand { get; }
    public RelayCommand DeleteServerCommand { get; }
    public RelayCommand SetDefaultServerCommand { get; }
    public RelayCommand TestConnectionCommand { get; }

    public RelayCommand SelectImageCommand { get; }
    public RelayCommand AnalyzeAndGenerateCommand { get; }
    public RelayCommand GenerateFromFinalPromptCommand { get; }
    public RelayCommand GenerateFromTextCommand { get; }
    public RelayCommand ClearWorkspaceCommand { get; }

    public RelayCommand RepeatFromHistoryCommand { get; }
    public RelayCommand RepeatWithoutAnalyzeCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand DeleteHistoryItemCommand { get; }
    public RelayCommand ClearHistoryFiltersCommand { get; }

    public RelayCommand CopyFinalPromptCommand { get; }
    public RelayCommand CopyJsonCommand { get; }

    public RelayCommand ResetGenerationParametersCommand { get; }
    public RelayCommand CopyFinalNegativePromptCommand { get; }
    public RelayCommand RandomizeSeedCommand { get; }
    public RelayCommand NewSessionCommand { get; }
    public RelayCommand CopySeedCommand { get; }
    public RelayCommand SaveGenerationPresetCommand { get; }
    public RelayCommand DeleteGenerationPresetCommand { get; }
    public RelayCommand RenameGenerationPresetCommand { get; }
    public RelayCommand RefreshModelsCommand { get; }
    public RelayCommand InstallModelCommand { get; }
    public RelayCommand InstallNewModelCommand { get; }
    public RelayCommand SaveGeneratedImageAsCommand { get; }
    public RelayCommand OpenGeneratedImageFolderCommand { get; }

    #endregion

    #region Constructor

    public MainViewModel()
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
            async _ => await GenerateFromFinalPromptAsync(),
            _ => !IsBusy && SelectedServer is not null && !string.IsNullOrWhiteSpace(FinalPrompt));

        GenerateFromTextCommand = new RelayCommand(
            async _ => await GenerateFromTextAsync(),
            _ => !IsBusy && SelectedServer is not null && !string.IsNullOrWhiteSpace(FinalPrompt));

        ClearWorkspaceCommand = new RelayCommand(
            async _ => await ClearWorkspaceAsync(),
            _ => !IsBusy);

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
        
        GenerateFromFinalPromptCommand = new RelayCommand(
            async _ => await GenerateFromFinalPromptWithProgressAsync(),
            _ => !IsBusy && SelectedServer is not null && !string.IsNullOrWhiteSpace(FinalPrompt));
        

        LoadGenerationPresets();
        LoadWorkspaceStateAsync();
        LoadServersAsync();
        LoadHistoryAsync();
    }

    #endregion

    #region Initialization

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

    #endregion

    #region Server Methods

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

    #endregion

    #region History Methods

    private void LoadHistoryItemToWorkspace(HistoryItem item)
    {
        ClearWorkspaceState();

        PromptOverride = item.PromptOverride ?? string.Empty;
        NegativePromptOverride = item.NegativePromptOverride ?? string.Empty;
        FinalPrompt = item.FinalPrompt ?? string.Empty;
        FinalNegativePrompt = item.FinalNegativePrompt ?? string.Empty;
        LastResponseJson = item.ResponseJson ?? string.Empty;
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

    #endregion

    #region Generation Methods

    private async Task GenerateFromFinalPromptWithProgressAsync()
    {
        if (SelectedServer == null || string.IsNullOrWhiteSpace(FinalPrompt))
            return;

        if (!ValidateGenerationParameters())
            return;

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

            GenerationJobStatus? status = null;

            while (true)
            {
                await Task.Delay(700);

                status = await _apiClientService.GetJobStatusAsync(SelectedServer.BaseUrl, jobId);
                if (status == null)
                    continue;

                GenerationProgress = status.Progress;
                GenerationProgressText = string.IsNullOrWhiteSpace(status.Message)
                    ? status.Status
                    : status.Message;

                if (status.Status == "completed")
                    break;

                if (status.Status == "failed")
                {
                    MessageBox.Show(status.Error ?? "Generování selhalo.", "Chyba", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
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
            IsGeneratingWithProgress = false;
            GenerationProgress = 0;
            GenerationProgressText = string.Empty;
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
        LastResponseJson = string.Empty;

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

        UpdateStatusUi("Analyzuji a generuji obrázek...", true);

        try
        {
            var result = await _apiClientService.AnalyzeAndGenerateAsync(
                SelectedImagePaths.Any() ? SelectedImagePaths : new[] { SelectedImagePath! },
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

            LastResponseJson = result.ResponseJson ?? string.Empty;
            var (finalPrompt, finalNegativePrompt) = ExtractFinalPrompts(LastResponseJson);
            FinalPrompt = finalPrompt;
            FinalNegativePrompt = finalNegativePrompt;

            if (Application.Current.MainWindow is MainWindow jsonWindow)
            {
                jsonWindow.SetLastResponseJson(LastResponseJson);
                jsonWindow.SetPromptOverride(PromptOverride);
                jsonWindow.SetNegativePromptOverride(NegativePromptOverride);
                jsonWindow.SetFinalPrompt(FinalPrompt);
                jsonWindow.SetFinalNegativePrompt(FinalNegativePrompt);
            }

            if (!result.Success)
            {
                UpdateStatusUi("Generování selhalo", false);

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
        if (SelectedServer == null || string.IsNullOrWhiteSpace(FinalPrompt))
            return;

        if (!ValidateGenerationParameters())
            return;

        UpdateStatusUi("Generuji z textového promptu...", true);

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

    #endregion

    #region Export Methods

    private async Task ExportAsync()
    {
        if (SelectedHistoryItem == null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "ZIP archive (*.zip)|*.zip",
            FileName = $"image_export_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != true)
            return;

        UpdateStatusUi("Exportuji...", true);

        try
        {
            await _exportService.ExportAsync(SelectedHistoryItem, dialog.FileName);

            UpdateStatusUi("Export hotov", false);

            MessageBox.Show(
                $"Export uložen:\n{dialog.FileName}",
                "Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UpdateStatusUi("Export selhal", false);

            MessageBox.Show(
                ex.ToString(),
                "Export chyba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (IsBusy)
            {
                UpdateStatusUi("Ready", false);
            }
        }
    }

    #endregion

    #region Helper Methods

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
            SelectedModelId = SelectedModel?.Id ?? string.Empty
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

    #endregion
}