using System.IO;
using System.Text.Json;
using desktop_cshar.Infrastructure;
using desktop_cshar.Models;

namespace desktop_cshar.Services;

public class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public HistoryService()
    {
        AppPaths.EnsureDirectories();
    }

    public async Task<List<HistoryItem>> LoadAsync()
    {
        if (!File.Exists(AppPaths.HistoryFilePath))
        {
            return new List<HistoryItem>();
        }

        var json = await File.ReadAllTextAsync(AppPaths.HistoryFilePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<HistoryItem>();
        }

        var items = JsonSerializer.Deserialize<List<HistoryItem>>(json, JsonOptions);
        return items ?? new List<HistoryItem>();
    }

    public async Task SaveAsync(List<HistoryItem> items)
    {
        var json = JsonSerializer.Serialize(items, JsonOptions);
        await File.WriteAllTextAsync(AppPaths.HistoryFilePath, json);
    }

    public async Task AddAsync(HistoryItem item)
    {
        var items = await LoadAsync();
        items.Insert(0, item);
        await SaveAsync(items);
    }
}