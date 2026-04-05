using System.IO;
using System.Text.Json;
using desktop_cshar.Infrastructure;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class ServerProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ServerProfileService()
    {
        AppPaths.EnsureDirectories();
    }

    public async Task<List<ServerProfile>> LoadAsync()
    {
        if (!File.Exists(AppPaths.ServersFilePath))
        {
            return new List<ServerProfile>();
        }

        var json = await File.ReadAllTextAsync(AppPaths.ServersFilePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ServerProfile>();
        }

        var servers = JsonSerializer.Deserialize<List<ServerProfile>>(json, JsonOptions);
        return servers ?? new List<ServerProfile>();
    }

    public async Task SaveAsync(List<ServerProfile> servers)
    {
        var json = JsonSerializer.Serialize(servers, JsonOptions);
        await File.WriteAllTextAsync(AppPaths.ServersFilePath, json);
    }

    public async Task<bool> HasAnyServerAsync()
    {
        var servers = await LoadAsync();
        return servers.Any(s => s.IsEnabled);
    }
}