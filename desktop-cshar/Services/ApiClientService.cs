using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class ApiClientService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<bool> TestConnectionAsync(ServerProfile server)
    {
        try
        {
            var url = $"{server.BaseUrl.TrimEnd('/')}/health";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> UninstallModelAsync(string baseUrl, string modelId)
    {
        try
        {
            var payload = new
            {
                model_id = modelId
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{baseUrl.TrimEnd('/')}/models/uninstall",
                content);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? ImagePath, string? ResponseJson, string? ErrorMessage)> AnalyzeAndGenerateAsync(
        IEnumerable<string> filePaths,
        string baseUrl,
        string promptOverride,
        string negativePromptOverride,
        int width,
        int height,
        int numInferenceSteps,
        double guidanceScale,
        int? seed,
        string? modelId,
        int numberOfImages)
    {
        var openedStreams = new List<FileStream>();
        var streamContents = new List<StreamContent>();

        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    continue;

                var fileStream = File.OpenRead(filePath);
                openedStreams.Add(fileStream);

                var streamContent = new StreamContent(fileStream);
                streamContents.Add(streamContent);

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(streamContent, "files", Path.GetFileName(filePath));
            }

            if (openedStreams.Count == 0)
            {
                return (false, null, null, "Nebyl nalezen žádný platný vstupní obrázek.");
            }

            content.Add(new StringContent(promptOverride ?? string.Empty), "prompt_override");
            content.Add(new StringContent(negativePromptOverride ?? string.Empty), "negative_prompt_override");
            content.Add(new StringContent(width.ToString()), "width");
            content.Add(new StringContent(height.ToString()), "height");
            content.Add(new StringContent(numInferenceSteps.ToString()), "num_inference_steps");
            content.Add(new StringContent(guidanceScale.ToString(System.Globalization.CultureInfo.InvariantCulture)), "guidance_scale");
            content.Add(new StringContent(Math.Max(1, numberOfImages).ToString()), "num_images");

            if (seed.HasValue)
            {
                content.Add(new StringContent(seed.Value.ToString()), "seed");
            }

            if (!string.IsNullOrWhiteSpace(modelId))
            {
                content.Add(new StringContent(modelId), "model_id");
            }

            var url = $"{baseUrl.TrimEnd('/')}/analyze-and-generate";
            var response = await _httpClient.PostAsync(url, content);

            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, responseText, $"HTTP {(int)response.StatusCode}: {responseText}");
            }

            using var doc = JsonDocument.Parse(responseText);

            if (!doc.RootElement.TryGetProperty("generated_filename", out var filenameElement))
            {
                return (false, null, responseText, $"V odpovědi chybí generated_filename. Response: {responseText}");
            }

            var generatedFilename = filenameElement.GetString();

            if (string.IsNullOrWhiteSpace(generatedFilename))
            {
                return (false, null, responseText, $"generated_filename je prázdný. Response: {responseText}");
            }

            var imageUrl = $"{baseUrl.TrimEnd('/')}/images/{generatedFilename}";
            return (true, imageUrl, responseText, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.ToString());
        }
        finally
        {
            foreach (var streamContent in streamContents)
            {
                streamContent.Dispose();
            }

            foreach (var fileStream in openedStreams)
            {
                await fileStream.DisposeAsync();
            }
        }
    }

    public async Task<(string? FilePath, string? ErrorMessage)> DownloadImageToTempFileAsync(string imageUrl)
    {
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(imageUrl);

            if (bytes.Length == 0)
            {
                return (null, "Stažený obrázek má 0 bajtů.");
            }

            var extension = ".png";
            try
            {
                var uri = new Uri(imageUrl);
                var maybeExt = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(maybeExt))
                {
                    extension = maybeExt;
                }
            }
            catch
            {
                // fallback zůstane .png
            }

            var tempFilePath = Path.Combine(
                Path.GetTempPath(),
                $"image_studio_{Guid.NewGuid()}{extension}");

            await File.WriteAllBytesAsync(tempFilePath, bytes);

            return (tempFilePath, null);
        }
        catch (Exception ex)
        {
            return (null, ex.ToString());
        }
    }

    public async Task<(bool Success, string? ImagePath, string? ResponseJson, string? ErrorMessage)> GenerateAsync(
        string baseUrl,
        string prompt,
        string negativePrompt,
        int width,
        int height,
        int numInferenceSteps,
        double guidanceScale,
        int? seed,
        string? modelId,
        int numberOfImages)
    {
        try
        {
            var payload = new
            {
                prompt,
                negative_prompt = negativePrompt,
                width,
                height,
                num_inference_steps = numInferenceSteps,
                guidance_scale = guidanceScale,
                seed,
                model_id = modelId,
                num_images = Math.Max(1, numberOfImages)
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{baseUrl.TrimEnd('/')}/generate";
            var response = await _httpClient.PostAsync(url, content);

            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, responseText, $"HTTP {(int)response.StatusCode}: {responseText}");
            }

            using var doc = JsonDocument.Parse(responseText);

            if (!doc.RootElement.TryGetProperty("filename", out var filenameElement))
            {
                return (false, null, responseText, $"V odpovědi chybí filename. Response: {responseText}");
            }

            var generatedFilename = filenameElement.GetString();

            if (string.IsNullOrWhiteSpace(generatedFilename))
            {
                return (false, null, responseText, $"filename je prázdný. Response: {responseText}");
            }

            var imageUrl = $"{baseUrl.TrimEnd('/')}/images/{generatedFilename}";
            return (true, imageUrl, responseText, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.ToString());
        }
    }

    public async Task<List<ModelInfo>> GetModelsAsync(string baseUrl)
    {
        var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/models");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<ModelInfo>>(json, JsonOptions)
               ?? new List<ModelInfo>();
    }

    public async Task<bool> InstallModelAsync(string baseUrl, string modelId)
    {
        try
        {
            var payload = new
            {
                model_id = modelId
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{baseUrl.TrimEnd('/')}/models/install",
                content);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> StartGenerateJobAsync(
        string baseUrl,
        string prompt,
        string negativePrompt,
        int width,
        int height,
        int numInferenceSteps,
        double guidanceScale,
        int? seed,
        string? modelId,
        int numberOfImages)
    {
        var payload = new
        {
            prompt,
            negative_prompt = negativePrompt,
            width,
            height,
            num_inference_steps = numInferenceSteps,
            guidance_scale = guidanceScale,
            seed,
            model_id = modelId,
            num_images = Math.Max(1, numberOfImages)
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/generate-job", content);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseText);

        if (!doc.RootElement.TryGetProperty("job_id", out var jobIdElement))
            return null;

        return jobIdElement.GetString();
    }

    public async Task<GenerationJobStatus?> GetJobStatusAsync(string baseUrl, string jobId)
    {
        var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/jobs/{jobId}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<GenerationJobStatus>(json, JsonOptions);
    }

    public async Task<string?> StartAnalyzeAndGenerateJobAsync(
        IEnumerable<string> imagePaths,
        string baseUrl,
        string promptOverride,
        string negativePromptOverride,
        int width,
        int height,
        int steps,
        double guidanceScale,
        int? seed,
        string? modelId,
        int numImages)
    {
        var openedStreams = new List<FileStream>();
        var streamContents = new List<StreamContent>();

        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var imagePath in imagePaths)
            {
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                    continue;

                var fileStream = File.OpenRead(imagePath);
                openedStreams.Add(fileStream);

                var fileContent = new StreamContent(fileStream);
                streamContents.Add(fileContent);

                var extension = Path.GetExtension(imagePath).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };

                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(fileContent, "files", Path.GetFileName(imagePath));
            }

            if (openedStreams.Count == 0)
                return null;

            content.Add(new StringContent(promptOverride ?? string.Empty), "prompt_override");
            content.Add(new StringContent(negativePromptOverride ?? string.Empty), "negative_prompt_override");
            content.Add(new StringContent(width.ToString()), "width");
            content.Add(new StringContent(height.ToString()), "height");
            content.Add(new StringContent(steps.ToString()), "num_inference_steps");
            content.Add(new StringContent(guidanceScale.ToString(System.Globalization.CultureInfo.InvariantCulture)), "guidance_scale");
            content.Add(new StringContent(Math.Max(1, numImages).ToString()), "num_images");

            if (seed.HasValue)
                content.Add(new StringContent(seed.Value.ToString()), "seed");

            if (!string.IsNullOrWhiteSpace(modelId))
                content.Add(new StringContent(modelId), "model_id");

            var response = await _httpClient.PostAsync(
                $"{baseUrl.TrimEnd('/')}/analyze-and-generate-job",
                content);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("job_id", out var jobIdElement))
                return jobIdElement.GetString();

            return null;
        }
        finally
        {
            foreach (var streamContent in streamContents)
            {
                streamContent.Dispose();
            }

            foreach (var fileStream in openedStreams)
            {
                await fileStream.DisposeAsync();
            }
        }
    }

    public async Task CancelJobAsync(string baseUrl, string jobId)
    {
        var response = await _httpClient.PostAsync(
            $"{baseUrl.TrimEnd('/')}/jobs/{jobId}/cancel",
            null);

        if (!response.IsSuccessStatusCode)
        {
            var responseText = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to cancel job. HTTP {(int)response.StatusCode}: {responseText}");
        }
    }
    
    public async Task<RuntimeInfo?> GetRuntimeInfoAsync(string baseUrl)
    {
        var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/runtime-info");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<RuntimeInfo>(json, JsonOptions);
    }
    
    public async Task<List<DiscoverModelItem>> GetInstalledDiscoverModelsAsync(string baseUrl, int limit = 50)
    {
        var response = await _httpClient.GetAsync(
            $"{baseUrl.TrimEnd('/')}/models/discover?installed_only=true&limit={limit}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<DiscoverModelItem>>(json, options)
               ?? new List<DiscoverModelItem>();
    }

    public async Task<List<DiscoverModelItem>> DiscoverModelsAsync(
        string baseUrl,
        string search,
        int limit = 20,
        string? task = "text-to-image",
        bool onlyDiffusers = true,
        bool onlySdxl = false)
    {
        var query = new List<string>
        {
            $"search={Uri.EscapeDataString(search ?? string.Empty)}",
            $"limit={limit}"
        };

        if (!string.IsNullOrWhiteSpace(task))
            query.Add($"task={Uri.EscapeDataString(task)}");

        query.Add($"only_diffusers={onlyDiffusers.ToString().ToLowerInvariant()}");
        query.Add($"only_sdxl={onlySdxl.ToString().ToLowerInvariant()}");

        var url = $"{baseUrl.TrimEnd('/')}/models/discover?{string.Join("&", query)}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<DiscoverModelItem>>(json, options)
               ?? new List<DiscoverModelItem>();
    }
    
    public async Task<bool> UnloadModelAsync(string baseUrl, string? modelId, bool unloadAll)
    {
        var payload = new
        {
            model_id = modelId,
            unload_all = unloadAll
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/models/unload", content);

        return response.IsSuccessStatusCode;
    }
    
    public async Task<List<GenerationJobStatus>> GetJobsAsync(string baseUrl)
    {
        var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/jobs");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<GenerationJobStatus>>(json, options)
               ?? new List<GenerationJobStatus>();
    }
}