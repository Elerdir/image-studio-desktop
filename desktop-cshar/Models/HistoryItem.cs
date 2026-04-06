using System.Text.Json;

namespace desktop_cshar.Models;

public class HistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string OperationType { get; set; } = "AnalyzeAndGenerate";

    public string SourceImagePath { get; set; } = string.Empty;

    public string GeneratedImagePath { get; set; } = string.Empty;

    public string PromptOverride { get; set; } = string.Empty;

    public string NegativePromptOverride { get; set; } = string.Empty;

    public string FinalPrompt { get; set; } = string.Empty;

    public string FinalNegativePrompt { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;
    
    public int? Seed { get; set; }
    public bool UsedRandomSeed { get; set; }
    
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    
    public string GeneratedImagePathsJson { get; set; } = string.Empty;

    public string ShortOperationType => OperationType switch
    {
        "AnalyzeAndGenerate" => "Analyze+Generate",
        "GenerateOnly" => "Generate Only",
        "GenerateFromFinalPrompt" => "Final Prompt",
        "GenerateFromText" => "Text Only",
        _ => OperationType
    };

    public string CreatedAtLocalText => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string DisplayName =>
        $"{CreatedAtUtc:yyyy-MM-dd HH:mm:ss} | {ServerName} | {OperationType}";
    
    public int GeneratedImagesCount
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GeneratedImagePathsJson))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(GeneratedImagePathsJson);
                    if (list != null && list.Any())
                        return list.Count;
                }
                catch
                {
                    // ignore
                }
            }

            return string.IsNullOrWhiteSpace(GeneratedImagePath) ? 0 : 1;
        }
    }
}