using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexQuotaWidget;

public static class WidgetPaths
{
    public static string DataDirectory { get; } = ResolveDataDirectory();
    private static string ResolveDataDirectory()
    {
        var isolated = Environment.GetEnvironmentVariable("CODEX_QUOTA_WIDGET_DATA_DIR");
        return Path.GetFullPath(string.IsNullOrWhiteSpace(isolated)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaWidget", "data")
            : isolated);
    }
    public static void Initialize()
    {
        Directory.CreateDirectory(DataDirectory);
        // Import only recognized legacy preferences; never account credentials or usage records.
        Import(() =>
        {
            var legacy = Path.Combine(AppContext.BaseDirectory,"settings.json");
            var target = Path.Combine(DataDirectory,"settings.json");
            if (!File.Exists(target) && File.Exists(legacy) && new FileInfo(legacy).Length <= 4096)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(legacy));
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("language",out var value) && value.ValueKind == JsonValueKind.String
                    && Regex.IsMatch(value.GetString() ?? "",@"\A[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8}){0,3}\z"))
                    File.WriteAllText(target,JsonSerializer.Serialize(new { language=value.GetString() }));
            }
        });
        Import(() =>
        {
            var target=Path.Combine(DataDirectory,"position.json");
            if (!File.Exists(target) && WindowPlacement.Load(Path.Combine(AppContext.BaseDirectory,"position.json")) is { } point)
                WindowPlacement.Save(target,point);
        });
        Import(() =>
        {
            var languageFolder=Path.Combine(AppContext.BaseDirectory,"Languages");
            if (Directory.Exists(languageFolder))
            {
                foreach(var file in Directory.EnumerateFiles(languageFolder,"*.json"))
                {
                    Import(() =>
                    {
                    if(new FileInfo(file).Length > 65536) return;
                    using var document=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions { MaxDepth=8 });
                    if(document.RootElement.ValueKind!=JsonValueKind.Object || !document.RootElement.TryGetProperty("id",out var id)
                        || id.ValueKind!=JsonValueKind.String || !Regex.IsMatch(id.GetString() ?? "",@"\A[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8}){0,3}\z")) return;
                    var folder=Path.Combine(DataDirectory,"Languages");
                    Directory.CreateDirectory(folder);
                    var target=Path.Combine(folder,id.GetString()+".json");
                    if(!File.Exists(target)) File.Copy(file,target);
                    });
                }
            }
        });
    }
    private static void Import(Action operation)
    {
        try { operation(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
    }
}
