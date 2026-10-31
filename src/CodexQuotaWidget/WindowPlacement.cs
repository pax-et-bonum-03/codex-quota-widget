using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CodexQuotaWidget;

public static class WindowPlacement
{
    public static Point Calculate(Point anchor, double width, double height, double foldedHeight, Rect bounds, bool upwards)
    {
        double x = Math.Clamp(anchor.X - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        double y = Math.Clamp(upwards ? anchor.Y + foldedHeight - height : anchor.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - height));
        return new Point(x, y);
    }

    public static Point? Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("centerX", out var x) || !root.TryGetProperty("top", out var y)
                || x.ValueKind != JsonValueKind.Number || y.ValueKind != JsonValueKind.Number || !x.TryGetDouble(out double center) || !y.TryGetDouble(out double top)
                || !double.IsFinite(center) || !double.IsFinite(top) || Math.Abs(center) > 1_000_000 || Math.Abs(top) > 1_000_000) return null;
            return new Point(center, top);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public static void Save(string path, Point? anchor)
    {
        try
        {
            if (!anchor.HasValue) { File.Delete(path); return; }
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { centerX = anchor.Value.X, top = anchor.Value.Y }));
            File.Move(temporary, path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // Moving remains available in a read-only folder.
    }
}
