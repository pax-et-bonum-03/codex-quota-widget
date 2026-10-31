using System;
using System.IO;
using System.Text.Json;

namespace CodexQuotaWidget;

// Only the access token crosses the local pipe. Never renew, copy, or write the
// desktop's auth store: Codex owns the account's sign-in and refresh lifecycle.
internal sealed class AccountTokens
{
    internal string AccessToken { get; }
    internal string AccountId { get; }
    private AccountTokens(string accessToken, string accountId)
    {
        AccessToken = accessToken;
        AccountId = accountId;
    }
    internal static string SourceDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("CODEX_HOME");
            return Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
                : configured);
        }
    }
    internal static AccountTokens Read()
    {
        try
        {
            var path = Path.Combine(SourceDirectory, "auth.json");
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length > 65536) throw new WidgetException("error.login");
            using var document = JsonDocument.Parse(file, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            if (Metrics.Text(root, "auth_mode") is { } mode && mode != "chatgpt") throw new WidgetException("error.login");
            if (!root.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
                throw new WidgetException("error.login");
            var access = Metrics.Text(tokens, "access_token");
            var account = Metrics.Text(tokens, "account_id");
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(account)) throw new WidgetException("error.login");
            return new AccountTokens(access, account);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new WidgetException("error.login");
        }
    }
    internal object LoginParameters => new { type = "chatgptAuthTokens", accessToken = AccessToken, chatgptAccountId = AccountId };
    internal object RefreshResponse => new { accessToken = AccessToken, chatgptAccountId = AccountId, chatgptPlanType = (string?)null };
    internal bool SameAs(AccountTokens other) => AccessToken == other.AccessToken && AccountId == other.AccountId;
}

internal sealed class BackendDirectory : IDisposable
{
    internal string Path { get; }
    private readonly string parent;
    internal BackendDirectory()
    {
        parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(WidgetPaths.DataDirectory, "backend"));
        Path = System.IO.Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        // Only delete this session's directory; never the real Codex home.
        if (!System.IO.Path.GetFullPath(Path).StartsWith(parent + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            if (!Directory.Exists(Path) || (File.GetAttributes(Path) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) return;
            // Avoid traversing any junctions or symlinks during cleanup.
            RemoveContents(new DirectoryInfo(Path));
            Directory.Delete(Path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    private static void RemoveContents(DirectoryInfo directory)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            if (entry is DirectoryInfo child) { RemoveContents(child); child.Delete(); }
            else entry.Delete();
        }
    }
}
