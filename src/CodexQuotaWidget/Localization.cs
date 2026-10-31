using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexQuotaWidget;

public sealed record LanguagePack(string Id, string DisplayName, Dictionary<string, string> Strings, bool BuiltIn);

public sealed class Localization
{
    public static Localization Current { get; } = new(WidgetPaths.DataDirectory);
    private readonly string directory;
    private readonly Dictionary<string, LanguagePack> packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> fallback;
    public string LanguageId { get; private set; } = "zh-CN";
    public IEnumerable<LanguagePack> Packs => packs.Values.OrderBy(p => p.BuiltIn ? 0 : 1).ThenBy(p => p.Id, StringComparer.Ordinal);
    public IEnumerable<string> Keys => fallback.Keys;

    public Localization(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        foreach (var id in new[] { "en", "zh-CN", "zh-TW" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"CodexQuotaWidget.Languages.{id}.json")!;
            using var reader = new StreamReader(stream);
            var pack = Parse(reader.ReadToEnd(), true);
            packs.Add(pack.Id, pack);
        }
        fallback = packs["en"].Strings;
        var languageDirectory = Path.Combine(this.directory, "Languages");
        if (Directory.Exists(languageDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(languageDirectory, "*.json").Take(100))
            {
                try
                {
                    var pack = ReadPack(path);
                    if (!packs.ContainsKey(pack.Id)) packs.Add(pack.Id, pack);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { }
            }
        }
        try
        {
            var path = Path.Combine(this.directory, "settings.json");
            if (File.Exists(path) && new FileInfo(path).Length <= 4096)
            {
                using var settings = JsonDocument.Parse(File.ReadAllText(path));
                if (settings.RootElement.ValueKind == JsonValueKind.Object && settings.RootElement.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.String && packs.TryGetValue(language.GetString() ?? "", out var selected)) LanguageId = selected.Id;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public string Text(string key, params object[] arguments)
    {
        var value = packs[LanguageId].Strings.TryGetValue(key, out var translated) ? translated : fallback.GetValueOrDefault(key, key);
        return arguments.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, arguments);
    }

    public void Select(string id, bool persist = true)
    {
        if (!packs.TryGetValue(id, out var pack)) throw new InvalidDataException("Unknown language");
        if (persist)
        {
            var path = Path.Combine(directory, "settings.json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { language = pack.Id }));
            File.Move(temporary, path, true);
        }
        LanguageId = pack.Id;
    }

    public LanguagePack Install(string path)
    {
        var pack = ReadPack(path);
        if (packs.TryGetValue(pack.Id, out var existing) && existing.BuiltIn) throw new InvalidDataException("Built-in language ID is reserved");
        var folder = Path.Combine(directory, "Languages");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, pack.Id + ".json");
        var temporary = target + ".tmp";
        // Serialize only the validated schema. Language packs never load assemblies, fonts, or markup.
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { schemaVersion = 1, id = pack.Id, displayName = pack.DisplayName, strings = pack.Strings }, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, target, true);
        packs[pack.Id] = pack;
        return pack;
    }

    private LanguagePack ReadPack(string path)
    {
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Language pack exceeds 64 KB");
        var pack = Parse(File.ReadAllText(path), false);
        foreach (var pair in pack.Strings)
        {
            if (!fallback.TryGetValue(pair.Key, out var original)) continue; // Allow keys added by later releases.
            string Signature(string text) => string.Join(",", Regex.Matches(text, @"(?<!\{)\{([0-9]+)\}(?!\})").Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v));
            if (Signature(pair.Value) != Signature(original)) throw new InvalidDataException("Translation placeholders do not match");
        }
        return pack;
    }

    private static LanguagePack Parse(string json, bool builtIn)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1) throw new InvalidDataException("Unsupported schema");
        if (!root.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String || !root.TryGetProperty("displayName", out var nameValue) || nameValue.ValueKind != JsonValueKind.String || !root.TryGetProperty("strings", out var stringsValue) || stringsValue.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Incomplete language pack");
        var id = idValue.GetString()!;
        var name = nameValue.GetString()!;
        if (!Regex.IsMatch(id, @"\A[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8}){0,3}\z") || name.Length is < 1 or > 64 || name.Any(char.IsControl)) throw new InvalidDataException("Invalid language identity");
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in stringsValue.EnumerateObject())
        {
            if (strings.Count >= 200 || property.Name.Length > 80 || property.Value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Invalid translation entry");
            var value = property.Value.GetString()!;
            if (value.Length > 1600 || value.Any(c => char.IsControl(c) && c != '\n' && c != '\t')) throw new InvalidDataException("Invalid translation text");
            try { _ = string.Format(CultureInfo.InvariantCulture, value, new object[8]); }
            catch (FormatException) { throw new InvalidDataException("Invalid translation format"); }
            if (!strings.TryAdd(property.Name, value)) throw new InvalidDataException("Duplicate translation key");
        }
        return new LanguagePack(id, name, strings, builtIn);
    }
}

public sealed class WidgetException(string key) : InvalidOperationException
{
    public string Key { get; } = key;
    public override string Message => Localization.Current.Text(Key);
}
