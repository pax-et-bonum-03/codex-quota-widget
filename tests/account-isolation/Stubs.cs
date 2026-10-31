namespace CodexQuotaWidget;

public sealed class WidgetException(string key) : System.InvalidOperationException
{
    public string Key { get; } = key;
}
public sealed class Localization
{
    public static Localization Current { get; } = new();
    public string Text(string key, params object[] args) => key;
}
public static class WidgetPaths
{
    public static string DataDirectory { get; } = System.Environment.GetEnvironmentVariable("CODEX_QUOTA_WIDGET_DATA_DIR")!;
}

// This class is compiled only into the fake-backend test executable. Production
// compiles TrustedCodex.cs instead; there is no runtime trust bypass switch.
internal sealed class TrustedCodex : System.IDisposable
{
    internal string Path { get; }
    private TrustedCodex(string path) { Path = path; }
    internal static TrustedCodex Open() => new(System.IO.Path.Combine(
        System.Environment.GetEnvironmentVariable("QUOTA_TEST_FAKE_CLI")!, "codex.exe"));
    public void Dispose() { }
}
