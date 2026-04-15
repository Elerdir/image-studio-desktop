using System.Text.Json.Serialization;

namespace desktop_cshar.Models;

public class RuntimeInfo
{
    
    [JsonPropertyName("app_name")]
    public string? AppName { get; set; }

    [JsonPropertyName("server_version")]
    public string? ServerVersion { get; set; }
    
    [JsonPropertyName("torch_version")]
    public string TorchVersion { get; set; } = string.Empty;

    [JsonPropertyName("cuda_available")]
    public bool CudaAvailable { get; set; }

    [JsonPropertyName("selected_device")]
    public string SelectedDevice { get; set; } = string.Empty;

    [JsonPropertyName("default_model_id")]
    public string DefaultModelId { get; set; } = string.Empty;

    [JsonPropertyName("cached_models")]
    public List<string> CachedModels { get; set; } = new();

    [JsonPropertyName("cuda_device_count")]
    public int? CudaDeviceCount { get; set; }

    [JsonPropertyName("cuda_current_device")]
    public int? CudaCurrentDevice { get; set; }

    [JsonPropertyName("cuda_device_name")]
    public string? CudaDeviceName { get; set; }

    [JsonPropertyName("cuda_device_capability")]
    public int[]? CudaDeviceCapability { get; set; }

    [JsonPropertyName("cuda_memory_free_mb")]
    public double? CudaMemoryFreeMb { get; set; }

    [JsonPropertyName("cuda_memory_total_mb")]
    public double? CudaMemoryTotalMb { get; set; }

    [JsonPropertyName("cuda_info_error")]
    public string? CudaInfoError { get; set; }
}