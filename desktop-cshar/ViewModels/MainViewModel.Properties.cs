using System.IO;
using System.Windows;
using desktop_cshar.Models;

namespace desktop_cshar.ViewModels;

public partial class MainViewModel
{
    #region Selected Items
    
    public List<AppThemeMode> AvailableThemeModes { get; } = new()
    {
        AppThemeMode.Auto,
        AppThemeMode.Light,
        AppThemeMode.Dark
    };

    private AppThemeMode _selectedThemeMode = AppThemeMode.Auto;

    public AppThemeMode SelectedThemeMode
    {
        get => _selectedThemeMode;
        set
        {
            if (_selectedThemeMode == value)
                return;

            _selectedThemeMode = value;
            OnPropertyChanged();

            _ = ApplyThemeAsync(value);
        }
    }

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

            if (_selectedServer != null)
            {
                _ = LoadModelsAsync();
                _ = LoadRuntimeInfoAsync();
                _ = LoadInstalledDiscoverModelsAsync();
                _ = LoadDiscoverModelsAsync();
                _ = LoadJobsAsync();
                _ = SaveWorkspaceStateAsync();
            }
            else
            {
                RuntimeInfo = null;
                JobItems.Clear();
                OnPropertyChanged(nameof(JobsSummaryText));
            }

            DeleteServerCommand?.RaiseCanExecuteChanged();
            EditServerCommand?.RaiseCanExecuteChanged();
            SetDefaultServerCommand?.RaiseCanExecuteChanged();
            TestConnectionCommand?.RaiseCanExecuteChanged();
            AnalyzeAndGenerateCommand?.RaiseCanExecuteChanged();
            GenerateFromFinalPromptCommand?.RaiseCanExecuteChanged();
            GenerateFromTextCommand?.RaiseCanExecuteChanged();
            RefreshModelsCommand?.RaiseCanExecuteChanged();
            InstallModelCommand?.RaiseCanExecuteChanged();
            InstallNewModelCommand?.RaiseCanExecuteChanged();
            CancelGenerationCommand?.RaiseCanExecuteChanged();
            RefreshJobsCommand?.RaiseCanExecuteChanged();
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
            
            UpdateActiveModelFlags();

            _ = SaveWorkspaceStateAsync();
            InstallModelCommand.RaiseCanExecuteChanged();
            DeleteModelCommand.RaiseCanExecuteChanged();
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
            CancelGenerationCommand.RaiseCanExecuteChanged();
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

    private string? _currentJobId;

    public string? CurrentJobId
    {
        get => _currentJobId;
        set
        {
            _currentJobId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentJobText));
            OnPropertyChanged(nameof(HasCurrentJob));
            CancelGenerationCommand.RaiseCanExecuteChanged();
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

    private RuntimeInfo? _runtimeInfo;

    public RuntimeInfo? RuntimeInfo
    {
        get => _runtimeInfo;
        set
        {
            _runtimeInfo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RuntimeDeviceText));
            OnPropertyChanged(nameof(RuntimeGpuText));
            OnPropertyChanged(nameof(RuntimeMemoryText));
            OnPropertyChanged(nameof(RuntimeModelsText));
            OnPropertyChanged(nameof(IsRuntimeOnGpu));
            OnPropertyChanged(nameof(RuntimeWarningText));
            OnPropertyChanged(nameof(RuntimeTorchText));
        }
    }

    public bool IsRuntimeOnGpu => RuntimeInfo?.CudaAvailable == true &&
                                  string.Equals(RuntimeInfo.SelectedDevice, "cuda", StringComparison.OrdinalIgnoreCase);

    public string RuntimeDeviceText
    {
        get
        {
            if (RuntimeInfo == null)
                return "Device: unknown";

            return $"Device: {RuntimeInfo.SelectedDevice}";
        }
    }

    public string RuntimeGpuText
    {
        get
        {
            if (RuntimeInfo == null)
                return "GPU: unknown";

            if (!RuntimeInfo.CudaAvailable)
                return "GPU: not available";

            return $"GPU: {RuntimeInfo.CudaDeviceName ?? "unknown"}";
        }
    }

    public string RuntimeMemoryText
    {
        get
        {
            if (RuntimeInfo == null)
                return "VRAM: unknown";

            if (!RuntimeInfo.CudaAvailable)
                return "VRAM: CPU mode";

            if (RuntimeInfo.CudaMemoryFreeMb.HasValue && RuntimeInfo.CudaMemoryTotalMb.HasValue)
            {
                var freeGb = RuntimeInfo.CudaMemoryFreeMb.Value / 1024.0;
                var totalGb = RuntimeInfo.CudaMemoryTotalMb.Value / 1024.0;
                return $"VRAM free: {freeGb:F1} GB / {totalGb:F1} GB";
            }

            return "VRAM: unavailable";
        }
    }

    public string RuntimeModelsText
    {
        get
        {
            if (RuntimeInfo == null)
                return "Loaded models: unknown";

            if (RuntimeInfo.CachedModels == null || RuntimeInfo.CachedModels.Count == 0)
                return "Loaded models: none";

            return $"Loaded models: {string.Join(", ", RuntimeInfo.CachedModels)}";
        }
    }

    public string RuntimeWarningText
    {
        get
        {
            if (RuntimeInfo == null)
                return string.Empty;

            if (!RuntimeInfo.CudaAvailable)
                return "Warning: backend is running on CPU. Generation will be much slower.";

            if (!string.IsNullOrWhiteSpace(RuntimeInfo.CudaInfoError))
                return $"CUDA info warning: {RuntimeInfo.CudaInfoError}";

            return string.Empty;
        }
    }
    
    public string RuntimeTorchText
    {
        get
        {
            if (RuntimeInfo == null || string.IsNullOrWhiteSpace(RuntimeInfo.TorchVersion))
                return "Torch: unknown";

            return $"Torch: {RuntimeInfo.TorchVersion}";
        }
    }

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
            CancelGenerationCommand.RaiseCanExecuteChanged();
            DeleteModelCommand.RaiseCanExecuteChanged();
            SelectModelCommand.RaiseCanExecuteChanged();
            RefreshJobsCommand.RaiseCanExecuteChanged();
            CancelJobByIdCommand.RaiseCanExecuteChanged();
        }
    }
    
    public string CurrentJobText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrentJobId))
                return "Current job: none";

            return $"Current job: {CurrentJobId}";
        }
    }

    public bool HasCurrentJob => !string.IsNullOrWhiteSpace(CurrentJobId);

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
    
    private string _discoverSearchText = "realistic";

    public string DiscoverSearchText
    {
        get => _discoverSearchText;
        set
        {
            _discoverSearchText = value;
            OnPropertyChanged();
            RefreshDiscoverModelsCommand.RaiseCanExecuteChanged();
        }
    }
    
    private bool _discoverOnlySdxl = true;

    public bool DiscoverOnlySdxl
    {
        get => _discoverOnlySdxl;
        set
        {
            _discoverOnlySdxl = value;
            OnPropertyChanged();
        }
    }
    
    private DiscoverModelItem? _selectedDiscoverModel;

    public DiscoverModelItem? SelectedDiscoverModel
    {
        get => _selectedDiscoverModel;
        set
        {
            _selectedDiscoverModel = value;
            OnPropertyChanged();

            InstallDiscoveredModelCommand.RaiseCanExecuteChanged();
            UninstallDiscoveredModelCommand.RaiseCanExecuteChanged();
        }
    }
    
    public string RuntimeServerVersionText =>
        string.IsNullOrWhiteSpace(RuntimeInfo?.ServerVersion)
            ? "Server: unknown"
            : $"Server: {RuntimeInfo.ServerVersion}";
    
    public string RuntimeAppNameText =>
        string.IsNullOrWhiteSpace(RuntimeInfo?.AppName)
            ? string.Empty
            : RuntimeInfo.AppName;
    
    public string ClientVersionText =>
        $"Client: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
    
    private GenerationJobStatus? _selectedJobItem;

    public GenerationJobStatus? SelectedJobItem
    {
        get => _selectedJobItem;
        set
        {
            _selectedJobItem = value;
            OnPropertyChanged();
        }
    }
    
    public string JobsSummaryText =>
        JobItems.Count == 0
            ? "Jobs: none"
            : $"Jobs: {JobItems.Count}";

    private string _originalPromptPreview = string.Empty;

    public string OriginalPromptPreview
    {
        get => _originalPromptPreview;
        set
        {
            _originalPromptPreview = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOriginalPromptPreview));
        }
    }

    private string _promptTranslationInfo = string.Empty;

    public string PromptTranslationInfo
    {
        get => _promptTranslationInfo;
        set
        {
            _promptTranslationInfo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPromptTranslationInfo));
        }
    }

    public bool HasOriginalPromptPreview =>
        !string.IsNullOrWhiteSpace(OriginalPromptPreview);
    
    public bool HasPromptTranslationInfo =>
        !string.IsNullOrWhiteSpace(PromptTranslationInfo);
    
    public List<WorkspaceMode> AvailableWorkspaceModes { get; } = new()
    {
        WorkspaceMode.TextToImage,
        WorkspaceMode.ImageAnalyzeGenerate
    };
    
    public string SelectedWorkspaceModeDisplay =>
        SelectedWorkspaceMode switch
        {
            WorkspaceMode.TextToImage => "Text to Image",
            WorkspaceMode.ImageAnalyzeGenerate => "Image Analyze + Generate",
            _ => SelectedWorkspaceMode.ToString()
        };
    
    private WorkspaceMode _selectedWorkspaceMode = WorkspaceMode.TextToImage;

    public WorkspaceMode SelectedWorkspaceMode
    {
        get => _selectedWorkspaceMode;
        set
        {
            if (_selectedWorkspaceMode == value)
                return;

            _selectedWorkspaceMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTextToImageMode));
            OnPropertyChanged(nameof(IsImageAnalyzeMode));
            OnPropertyChanged(nameof(SelectedWorkspaceModeDisplay));

            AnalyzeAndGenerateCommand?.RaiseCanExecuteChanged();
            GenerateFromFinalPromptCommand?.RaiseCanExecuteChanged();
            GenerateFromTextCommand?.RaiseCanExecuteChanged();

            _ = SaveWorkspaceStateAsync();
        }
    }
    
    public bool IsTextToImageMode => SelectedWorkspaceMode == WorkspaceMode.TextToImage;
    public bool IsImageAnalyzeMode => SelectedWorkspaceMode == WorkspaceMode.ImageAnalyzeGenerate;
    
    private string _mainPrompt = string.Empty;

    public string MainPrompt
    {
        get => _mainPrompt;
        set
        {
            _mainPrompt = value;
            OnPropertyChanged();
            GenerateFromTextCommand?.RaiseCanExecuteChanged();
        }
    }
    
    private string _mainNegativePrompt = string.Empty;

    public string MainNegativePrompt
    {
        get => _mainNegativePrompt;
        set
        {
            _mainNegativePrompt = value;
            OnPropertyChanged();
        }
    }
    
    #endregion
}