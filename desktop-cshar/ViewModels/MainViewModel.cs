using System.Collections.ObjectModel;
using desktop_cshar.Infrastructure;
using desktop_cshar.Models;
using desktop_cshar.Services;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    #region Services

    private readonly ServerProfileService _serverService = new();
    private readonly ApiClientService _apiClientService = new();
    private readonly HistoryService _historyService = new();
    private readonly ExportService _exportService = new();
    private readonly WorkspaceStateService _workspaceStateService = new();
    private readonly GenerationPresetService _generationPresetService = new();
    private readonly AppSettingsService _appSettingsService = new();

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
    
    public ObservableCollection<DiscoverModelItem> InstalledDiscoverModels { get; } = new();
    public ObservableCollection<DiscoverModelItem> DiscoverModelItems { get; } = new();
    public ObservableCollection<GenerationJobStatus> JobItems { get; } = new();

    #endregion

    public MainViewModel()
    {
        InitializeCommands();

        LoadAppSettingsAsync();
        LoadGenerationPresets();
        LoadWorkspaceStateAsync();
        LoadServersAsync();
        LoadHistoryAsync();
    }
}