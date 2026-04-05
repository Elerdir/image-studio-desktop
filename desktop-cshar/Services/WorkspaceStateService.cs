using System.IO;
using System.Text.Json;
using desktop_cshar.Infrastructure;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class WorkspaceStateService
{
    private readonly string _filePath = AppPaths.WorkspaceStateFilePath;

    public async Task<WorkspaceState> LoadAsync()
    {
        try
        {
            AppPaths.EnsureDirectories();

            if (!File.Exists(_filePath))
                return new WorkspaceState();

            var json = await File.ReadAllTextAsync(_filePath);
            var state = JsonSerializer.Deserialize<WorkspaceState>(json);

            return state ?? new WorkspaceState();
        }
        catch
        {
            return new WorkspaceState();
        }
    }

    public async Task SaveAsync(WorkspaceState state)
    {
        AppPaths.EnsureDirectories();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(state, options);
        await File.WriteAllTextAsync(_filePath, json);
    }
}