namespace desktop_cshar.Models;

public class GenerationJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> GeneratedFilenames { get; set; } = new();
    public string? Prompt { get; set; }
    public string? NegativePrompt { get; set; }
    public int? Seed { get; set; }
    public string? ModelId { get; set; }
    public string? Error { get; set; }
}