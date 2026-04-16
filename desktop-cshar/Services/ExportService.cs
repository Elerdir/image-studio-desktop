using System.IO;
using System.IO.Compression;
using System.Text.Json;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class ExportService
{
    public async Task ExportAsync(HistoryItem item, string zipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"image_export_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        // Text files – truly async I/O.
        var jsonPath = Path.Combine(tempDir, "response.json");
        await File.WriteAllTextAsync(jsonPath, item.ResponseJson ?? string.Empty);

        var prompt = ExtractFromJson(item.ResponseJson ?? string.Empty, "prompt");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "prompt.txt"), prompt ?? "");

        var negative = ExtractFromJson(item.ResponseJson ?? string.Empty, "negative_prompt");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "negative_prompt.txt"), negative ?? "");

        // File.Copy and ZipFile.CreateFromDirectory are synchronous and can
        // block for several seconds on large images.  Offload to thread pool
        // so the UI thread stays responsive.
        await Task.Run(() =>
        {
            if (File.Exists(item.SourceImagePath))
            {
                File.Copy(
                    item.SourceImagePath,
                    Path.Combine(tempDir, "input" + Path.GetExtension(item.SourceImagePath)),
                    overwrite: true);
            }

            if (File.Exists(item.GeneratedImagePath))
            {
                File.Copy(
                    item.GeneratedImagePath,
                    Path.Combine(tempDir, "output" + Path.GetExtension(item.GeneratedImagePath)),
                    overwrite: true);
            }

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(tempDir, zipPath);
            Directory.Delete(tempDir, recursive: true);
        });
    }

    private string? ExtractFromJson(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty(key, out var val)
                ? val.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}