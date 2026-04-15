using System.Text.Json.Serialization;

namespace desktop_cshar.Models;

public class DiscoverModelItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("downloads")]
    public int? Downloads { get; set; }

    [JsonPropertyName("likes")]
    public int? Likes { get; set; }

    [JsonPropertyName("pipeline_tag")]
    public string? PipelineTag { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("installed")]
    public bool Installed { get; set; }

    public string DisplayTitle
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Author))
                return Name;

            return $"{Name} ({Author})";
        }
    }

    public string DisplayMeta
    {
        get
        {
            var downloads = Downloads.HasValue ? Downloads.Value.ToString("N0") : "?";
            var likes = Likes.HasValue ? Likes.Value.ToString("N0") : "?";
            var pipeline = string.IsNullOrWhiteSpace(PipelineTag) ? "unknown" : PipelineTag;

            return $"Pipeline: {pipeline} | Downloads: {downloads} | Likes: {likes}";
        }
    }

    public string TagsText =>
        Tags == null || Tags.Count == 0
            ? string.Empty
            : string.Join(", ", Tags.Take(8));

    public string InstallStateText => Installed ? "Installed" : "Not installed";
    
    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
        }
    }
}