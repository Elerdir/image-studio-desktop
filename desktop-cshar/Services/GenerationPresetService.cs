using System.IO;
using System.Text.Json;
using desktop_cshar.Infrastructure;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class GenerationPresetService
{
    public async Task<List<GenerationPreset>> LoadAsync()
    {
        try
        {
            AppPaths.EnsureDirectories();

            if (!File.Exists(AppPaths.GenerationPresetsFilePath))
                return new List<GenerationPreset>();

            var json = await File.ReadAllTextAsync(AppPaths.GenerationPresetsFilePath);
            var presets = JsonSerializer.Deserialize<List<GenerationPreset>>(json);

            return presets ?? new List<GenerationPreset>();
        }
        catch
        {
            return new List<GenerationPreset>();
        }
    }

    public async Task SaveAsync(List<GenerationPreset> presets)
    {
        AppPaths.EnsureDirectories();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(presets, options);
        await File.WriteAllTextAsync(AppPaths.GenerationPresetsFilePath, json);
    }
}