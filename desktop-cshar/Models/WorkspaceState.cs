namespace desktop_cshar.Models;

public class WorkspaceState
{
    public string PromptOverride { get; set; } = string.Empty;
    public string NegativePromptOverride { get; set; } = string.Empty;
    public string FinalPrompt { get; set; } = string.Empty;
    public string FinalNegativePrompt { get; set; } = string.Empty;

    public int GenerationWidth { get; set; } = 512;
    public int GenerationHeight { get; set; } = 512;
    public int GenerationSteps { get; set; } = 12;
    public double GenerationGuidanceScale { get; set; } = 6.5;

    public bool UseRandomSeed { get; set; } = true;
    public int GenerationSeed { get; set; } = 42;

    public string SelectedPresetName { get; set; } = string.Empty;
    
    public Guid? SelectedServerId { get; set; }
    
    public int SelectedTabIndex { get; set; } = 0;
    
    public string SelectedModelId { get; set; } = string.Empty;
    
    public WorkspaceMode SelectedWorkspaceMode { get; set; } = WorkspaceMode.TextToImage;

    public string MainPrompt { get; set; } = string.Empty;
    public string MainNegativePrompt { get; set; } = string.Empty;
}