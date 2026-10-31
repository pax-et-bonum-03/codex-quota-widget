using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodexQuotaWidget;

public sealed record Quota(double Used, long? ResetsAt);
public sealed record Usage(long? Lifetime, long? Today, string? LatestDate, string? FirstDate);

public static class Metrics
{
    private static string L(string key, params object[] args) => Localization.Current.Text(key, args);
    public static string LocalDay => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "China Standard Time").ToString("yyyy-MM-dd");
    public static double Remaining(Quota quota) => Math.Clamp(100 - quota.Used, 0, 100);
    public static string Compact(long? value)
    {
        if (!value.HasValue) return "—";
        var n = value.Value;
        if (n >= 1_000_000_000) return (n / 1_000_000_000d).ToString("0.00", CultureInfo.InvariantCulture) + "B";
        if (n >= 1_000_000) return (n / 1_000_000d).ToString("0.00", CultureInfo.InvariantCulture) + "M";
        if (n >= 1_000) return (n / 1_000d).ToString("0.0", CultureInfo.InvariantCulture) + "K";
        return n.ToString("N0", CultureInfo.InvariantCulture);
    }
    public static long? Number(JsonElement element, string name) => element.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt64(out long result) ? result : null;
    public static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    public static (Quota? Five, Quota? Week) ParseLimits(JsonElement element)
    {
        JsonElement bucket;
        if (element.TryGetProperty("rateLimitsByLimitId", out var all) && all.ValueKind == JsonValueKind.Object && all.TryGetProperty("codex", out var codex)) bucket = codex;
        else if (element.TryGetProperty("rateLimits", out var legacy)) bucket = legacy;
        else return (null, null);
        Quota? five = null, week = null;
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            if (!window.TryGetProperty("usedPercent", out var used) || used.ValueKind != JsonValueKind.Number || !used.TryGetDouble(out double percent) || !double.IsFinite(percent)) continue;
            var quota = new Quota(Math.Clamp(percent, 0, 100), Number(window, "resetsAt"));
            switch (Number(window, "windowDurationMins"))
            {
                case 300: five = quota; break;
                case 10080: week = quota; break;
            }
        }
        return (five, week);
    }
    public static Usage ParseUsage(JsonElement element, string day)
    {
        long? lifetime = element.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object ? Number(summary, "lifetimeTokens") : null;
        long? today = null;
        string? latest = null, first = null;
        if (element.TryGetProperty("dailyUsageBuckets", out var buckets) && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in buckets.EnumerateArray())
            {
                var date = Text(bucket, "startDate");
                if (date == null) continue;
                if (first == null || string.CompareOrdinal(date, first) < 0) first = date;
                if (latest == null || string.CompareOrdinal(date, latest) > 0) latest = date;
                if (date == day) today = Number(bucket, "tokens");
            }
        }
        return new Usage(lifetime, today, latest, first);
    }
    public static string Countdown(long? reset)
    {
        if (!reset.HasValue) return L("reset.missing");
        var seconds = reset.Value - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (seconds <= 0) return L("reset.waiting");
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1) return L("reset.days", (int)span.TotalDays, span.Hours);
        if (span.TotalHours >= 1) return L("reset.hours", (int)span.TotalHours, span.Minutes);
        return L("reset.minutes", Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)));
    }
    public static string ResetTime(long? reset, bool full = false, bool compact = false, bool timeOnly = false)
    {
        if (!reset.HasValue) return compact ? "—" : L("reset.missing");
        try
        {
            var time = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.FromUnixTimeSeconds(reset.Value), "China Standard Time");
            if (compact) return time.ToString(timeOnly ? "HH:mm" : "MM-dd HH:mm", CultureInfo.InvariantCulture);
            return full ? L("reset.full", time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                : L("reset.normal", time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture));
        }
        catch (ArgumentOutOfRangeException) { return compact ? "—" : L("reset.missing"); }
    }
}

public sealed class CodexRpc : IDisposable
{
    private readonly Process process;
    private readonly BackendDirectory backend;
    private readonly TrustedCodex executable;
    private AccountTokens? accountTokens;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly SemaphoreSlim writer = new(1, 1);
    private int requestId;
    private bool disposed;
    public bool IsAlive => !disposed && !process.HasExited;
    public string? AccountIdentity => accountTokens?.AccountId;
    private CodexRpc(TrustedCodex executable)
    {
        this.executable = executable;
        try { backend = new BackendDirectory(); }
        catch { executable.Dispose(); throw; }
        var info = new ProcessStartInfo(executable.Path)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = backend.Path
        };
        // A quota reader must not start a second writer against the desktop's
        // config, auth store, SQLite state, plugins, or Code Mode host.
        foreach (var name in info.Environment.Keys.Where(name => name.StartsWith("CODEX_", StringComparison.OrdinalIgnoreCase)).ToArray())
            info.Environment.Remove(name);
        info.Environment.Remove("OPENAI_API_KEY");
        info.Environment["CODEX_HOME"] = backend.Path;
        info.Environment["CODEX_SQLITE_HOME"] = backend.Path;
        info.Environment["RUST_LOG"] = "off";
        info.ArgumentList.Add("app-server");
        info.ArgumentList.Add("--listen");
        info.ArgumentList.Add("stdio://");
        foreach (var option in new[] { "cli_auth_credentials_store=\"ephemeral\"", "analytics.enabled=false", "feedback.enabled=false", "features.apps=false", "features.plugins=false", "features.code_mode_host=false" })
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(option);
        }
        try { process = Process.Start(info) ?? throw new WidgetException("error.serverStart"); }
        catch { backend.Dispose(); executable.Dispose(); throw; }
        process.StandardInput.AutoFlush = true;
        _ = ReadLoop();
        // Drain diagnostic output without persisting account details or credentials.
        _ = DrainErrors();
    }
    public static async Task<CodexRpc> Connect()
    {
        var executable = await Task.Run(TrustedCodex.Open);
        var rpc = new CodexRpc(executable);
        try
        {
            await rpc.Call("initialize", new { clientInfo = new { name = "codex_quota_widget", title = "Codex Quota Widget", version = typeof(CodexRpc).Assembly.GetName().Version?.ToString() }, capabilities = new { experimentalApi = true } });
            await rpc.Send(new { method = "initialized", @params = new { } });
            await rpc.SynchronizeAccount();
            return rpc;
        }
        catch { rpc.Dispose(); throw; }
    }
    public async Task SynchronizeAccount()
    {
        var current = AccountTokens.Read();
        if (accountTokens?.SameAs(current) == true) return;
        await Call("account/login/start", current.LoginParameters);
        accountTokens = current;
    }
    public void EnsureAccountUnchanged()
    {
        if (AccountTokens.Read().AccountId != accountTokens?.AccountId) throw new WidgetException("error.accountChanged");
    }
    private async Task DrainErrors()
    {
        try { while (await process.StandardError.ReadLineAsync() != null) { } } catch { }
    }
    private async Task ReadLoop()
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var message = document.RootElement;
                    // Server requests have their own ID namespace. Check method first so
                    // a refresh request cannot consume a pending client response with the same ID.
                    if (message.TryGetProperty("method", out _))
                    {
                        if (message.TryGetProperty("id", out _)) _ = RespondToServerRequest(message.Clone());
                        continue;
                    }
                    if (!message.TryGetProperty("id", out var id) || !id.TryGetInt32(out int n) || !pending.TryRemove(n, out var completion)) continue;
                    if (message.TryGetProperty("error", out var error))
                    {
                        var code = Metrics.Number(error, "code");
                        var text = Metrics.Text(error, "message") ?? "";
                        // Only show fixed user-facing messages; never surface server payloads.
                        string key = text.Contains("authentication", StringComparison.OrdinalIgnoreCase) ? "error.login" : code == -32601 ? "error.unsupported" : "error.api";
                        completion.TrySetException(new WidgetException(key));
                    }
                    else if (message.TryGetProperty("result", out var result)) completion.TrySetResult(result.Clone());
                    else completion.TrySetException(new WidgetException("error.response"));
                }
                catch (JsonException) { }
            }
        }
        catch { }
        finally { foreach (var item in pending) if (pending.TryRemove(item.Key, out var completion)) completion.TrySetException(new WidgetException("error.disconnected")); }
    }
    private async Task RespondToServerRequest(JsonElement request)
    {
        var id = request.GetProperty("id");
        object response;
        try
        {
            if (Metrics.Text(request, "method") != "account/chatgptAuthTokens/refresh")
                response = new { id, error = new { code = -32601, message = "Unsupported server request" } };
            else
            {
                var current = AccountTokens.Read();
                var previous = request.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object
                    ? Metrics.Text(parameters, "previousAccountId") : null;
                if (accountTokens == null || current.AccountId != accountTokens.AccountId
                    || previous != null && previous != current.AccountId || accountTokens.SameAs(current))
                    throw new WidgetException("error.login");
                // Supply only a newer access token already renewed by Codex. Never
                // use its refresh token or retry auth renewal from this process.
                response = new { id, result = current.RefreshResponse };
                accountTokens = current;
            }
        }
        catch { response = new { id, error = new { code = -32000, message = "Waiting for desktop authentication" } }; }
        try { await Send(response); } catch { }
    }
    private async Task Send(object message)
    {
        await writer.WaitAsync();
        try { await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message)); }
        finally { writer.Release(); }
    }
    public async Task<JsonElement> Call(string method, object? parameters = null)
    {
        var id = Interlocked.Increment(ref requestId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;
        try
        {
            await Send(new { method, id, @params = parameters ?? new { } });
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (TimeoutException) { throw new WidgetException("error.timeout"); }
        finally { pending.TryRemove(id, out _); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var item in pending) if (pending.TryRemove(item.Key, out var completion)) completion.TrySetException(new WidgetException("error.disconnected"));
        try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(1000); } } catch { }
        process.Dispose();
        executable.Dispose();
        accountTokens = null;
        backend.Dispose();
    }
}
