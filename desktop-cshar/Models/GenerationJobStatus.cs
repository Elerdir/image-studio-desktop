using System.Text.Json.Serialization;

namespace desktop_cshar.Models;

public class GenerationJobStatus
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("generated_filenames")]
    public List<string> GeneratedFilenames { get; set; } = new();

    [JsonPropertyName("source_filename")]
    public string? SourceFilename { get; set; }

    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("hair")]
    public string? Hair { get; set; }

    [JsonPropertyName("clothing")]
    public string? Clothing { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("style")]
    public string? Style { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("queue_position")]
    public int? QueuePosition { get; set; }
    
    [JsonPropertyName("started_at")]
    public double? StartedAt { get; set; }
    
    [JsonPropertyName("finished_at")]
    public double? FinishedAt { get; set; }
    
    [JsonPropertyName("original_prompt")]
    public string? OriginalPrompt { get; set; }

    [JsonPropertyName("prompt_source_language")]
    public string? PromptSourceLanguage { get; set; }

    [JsonPropertyName("prompt_was_translated")]
    public bool PromptWasTranslated { get; set; }
    
    public string DisplayStatus => string.IsNullOrWhiteSpace(Status) ? "unknown" : Status;

    public bool IsRunning =>
        string.Equals(Status, "running", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "queued", StringComparison.OrdinalIgnoreCase);

    public bool IsCompleted =>
        string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);

    public bool IsFailed =>
        string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase);

    public bool IsCancelled =>
        string.Equals(Status, "cancelled", StringComparison.OrdinalIgnoreCase);

    public string ShortJobId =>
        string.IsNullOrWhiteSpace(JobId)
            ? string.Empty
            : (JobId.Length > 8 ? JobId[..8] : JobId);
    
    public bool HasOriginalPrompt =>
        !string.IsNullOrWhiteSpace(OriginalPrompt) &&
        !string.Equals(OriginalPrompt, Prompt, StringComparison.Ordinal);

    public bool HasPrompt =>
        !string.IsNullOrWhiteSpace(Prompt);

    public string PromptTranslationLabel =>
        PromptWasTranslated
            ? $"Translated from: {(string.IsNullOrWhiteSpace(PromptSourceLanguage) ? "unknown" : PromptSourceLanguage)}"
            : (!string.IsNullOrWhiteSpace(PromptSourceLanguage)
                ? $"Language: {PromptSourceLanguage}"
                : string.Empty);
}