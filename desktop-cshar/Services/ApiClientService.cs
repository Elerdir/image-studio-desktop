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

                // Backend očekává "files"
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
            content.Add(new StringContent(modelId ?? string.Empty), "model_id");
            content.Add(new StringContent(Math.Max(1, numberOfImages).ToString()), "num_images");

            if (seed.HasValue)
            {
                content.Add(new StringContent(seed.Value.ToString()), "seed");
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
                prompt = prompt,
                negative_prompt = negativePrompt,
                width = width,
                height = height,
                num_inference_steps = numInferenceSteps,
                guidance_scale = guidanceScale,
                seed = seed,
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

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<ModelInfo>>(json, options)
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
            prompt = prompt,
            negative_prompt = negativePrompt,
            width = width,
            height = height,
            num_inference_steps = numInferenceSteps,
            guidance_scale = guidanceScale,
            seed = seed,
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

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<GenerationJobStatus>(json, options);
    }
}