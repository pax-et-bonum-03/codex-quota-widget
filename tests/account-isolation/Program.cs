using System.Diagnostics;
using System.Text.Json;
using CodexQuotaWidget;

if (args.FirstOrDefault() == "app-server") { await MockServer.Run(args); return; }
if (args.Length != 1) throw new ArgumentException("Supply an empty test directory.");
var directory = Path.GetFullPath(args[0]);
if (Directory.Exists(directory)) throw new InvalidOperationException("Test directory must be new.");
Directory.CreateDirectory(directory);
var source = Path.Combine(directory, "desktop"); Directory.CreateDirectory(source);
var auth = Path.Combine(source, "auth.json");
var report = Path.Combine(directory, "mock-summary.json");
var phase = Path.Combine(directory, "phase.json");
void Credentials(string access, string account = "fixture-account") => File.WriteAllText(auth,
    JsonSerializer.Serialize(new { auth_mode = "chatgpt", tokens = new { access_token = access, account_id = account, refresh_token = "fixture-refresh-token-must-never-be-sent" } }));
Credentials("fixture-access-one");
File.WriteAllText(Path.Combine(source, "config.toml"), "# untouched desktop config");
File.WriteAllText(Path.Combine(source, "cap_sid"), "untouched desktop session");
var before = Directory.GetFiles(source).ToDictionary(file => Path.GetFileName(file)!, File.ReadAllBytes);
Environment.SetEnvironmentVariable("CODEX_HOME", source);
Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", source);
Environment.SetEnvironmentVariable("OPENAI_API_KEY", "fixture-api-key-must-not-be-inherited");
Environment.SetEnvironmentVariable("CODEX_API_KEY", "fixture-codex-key-must-not-be-inherited");
Environment.SetEnvironmentVariable("QUOTA_TEST_SOURCE", source);
Environment.SetEnvironmentVariable("QUOTA_TEST_REPORT", report);
Environment.SetEnvironmentVariable("QUOTA_TEST_PHASE", phase);
Environment.SetEnvironmentVariable("CODEX_QUOTA_WIDGET_DATA_DIR", Path.Combine(directory, "widget"));
var apphost = Path.Combine(AppContext.BaseDirectory, "AccountIsolationChecks.exe");
var fake = Path.Combine(directory, "fake-cli"); Directory.CreateDirectory(fake);
foreach (var name in new[] { "AccountIsolationChecks.dll", "AccountIsolationChecks.deps.json", "AccountIsolationChecks.runtimeconfig.json" })
    File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(fake, name));
File.Copy(apphost, Path.Combine(fake, "codex.exe"));
Environment.SetEnvironmentVariable("PATH", fake + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
Environment.SetEnvironmentVariable("QUOTA_TEST_FAKE_CLI", fake);
var checks = new List<string>();
void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks.Add(name); }
var rpc = await CodexRpc.Connect();
try
{
    var watch = Stopwatch.StartNew();
    var result = await rpc.Call("account/read", new { refreshToken = false });
    Check(result.GetProperty("account").GetProperty("type").GetString() == "chatgpt", "Incoming request ID does not consume client response");
    Check(watch.Elapsed < TimeSpan.FromSeconds(3), "Unchanged-token refresh request receives an immediate error");
    Check(rpc.AccountIdentity == "fixture-account", "Uses source account identity");
    foreach (var entry in before) Check(File.ReadAllBytes(Path.Combine(source, entry.Key!)).SequenceEqual(entry.Value), "Desktop file unchanged: " + entry.Key);
    var state = JsonDocument.Parse(File.ReadAllText(report)).RootElement;
    foreach (var name in new[] { "isolatedHome", "isolatedSqlite", "isolatedCwd", "ephemeralAuth", "pluginsOff", "codeHostOff", "apiKeysAbsent", "noRefreshToken", "refreshErrorAnswered", "unknownRequestAnswered" })
        Check(state.GetProperty(name).GetBoolean(), name);
    var backendPath = state.GetProperty("backendPath").GetString()!;
    Check(!File.Exists(Path.Combine(backendPath, "auth.json")), "No credential file in backend");
    Credentials("fixture-access-two"); File.WriteAllText(phase, "renewed");
    await rpc.Call("account/read", new { refreshToken = false });
    state = JsonDocument.Parse(File.ReadAllText(report)).RootElement;
    Check(state.GetProperty("newAccessSupplied").GetBoolean(), "Reuses a newer token supplied by desktop without renewal");
    Credentials("fixture-access-two", "fixture-other-account"); File.WriteAllText(phase, "switched");
    await rpc.Call("account/read", new { refreshToken = false });
    state = JsonDocument.Parse(File.ReadAllText(report)).RootElement;
    Check(state.GetProperty("accountSwitchRejected").GetBoolean(), "Cannot refresh into a different account");
    var changed = false;
    try { rpc.EnsureAccountUnchanged(); } catch (WidgetException error) { changed = error.Key == "error.accountChanged"; }
    Check(changed, "Detects account switch before committing usage");
    await rpc.SynchronizeAccount();
    Check(rpc.AccountIdentity == "fixture-other-account", "Next refresh synchronizes selected account");
    rpc.Dispose();
    Check(!Directory.Exists(backendPath), "Own session directory removed on shutdown");
    Check(File.Exists(Path.Combine(source, "auth.json")), "Desktop auth remains present on shutdown");
    File.Delete(auth);
    var loggedOut = false;
    try { _ = AccountTokens.Read(); } catch (WidgetException error) { loggedOut = error.Key == "error.login"; }
    Check(loggedOut, "Missing auth is reported without creating login state");
    File.WriteAllText(auth, "{");
    var malformed = false;
    try { _ = AccountTokens.Read(); } catch (WidgetException error) { malformed = error.Key == "error.login"; }
    Check(malformed, "Partial auth update does not expose parse errors");
    var directoriesBefore = Directory.GetDirectories(Path.Combine(WidgetPaths.DataDirectory, "backend")).Length;
    var failedConnect = false;
    try { using var invalid = await CodexRpc.Connect(); } catch (WidgetException error) { failedConnect = error.Key == "error.login"; }
    Check(failedConnect, "Failed account initialization returns a fixed login error");
    Check(Directory.GetDirectories(Path.Combine(WidgetPaths.DataDirectory, "backend")).Length == directoriesBefore, "Failed initialization cleans up its backend");
    Credentials("fixture-access-three");
    using (var disconnected = await CodexRpc.Connect())
    {
        watch.Restart();
        var failedCall = false;
        try { await disconnected.Call("account/usage/read"); } catch (WidgetException error) { failedCall = error.Key == "error.disconnected"; }
        Check(failedCall, "Unexpected backend exit fails pending requests");
        Check(watch.Elapsed < TimeSpan.FromSeconds(3), "Backend exit does not wait for request timeout");
        Check(!disconnected.IsAlive, "Exited backend is not reused");
    }
    Check(Directory.GetDirectories(Path.Combine(WidgetPaths.DataDirectory, "backend")).Length == directoriesBefore, "Disconnected backend is cleaned up");
    var output = JsonSerializer.Serialize(new { ok = true, checksPassed = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(directory, "checks.json"), output); Console.WriteLine(output);
}
finally { rpc.Dispose(); }

static class MockServer
{
    internal static async Task Run(string[] args)
    {
        var home = Environment.GetEnvironmentVariable("CODEX_HOME")!;
        var source = Environment.GetEnvironmentVariable("QUOTA_TEST_SOURCE")!;
        var report = Environment.GetEnvironmentVariable("QUOTA_TEST_REPORT")!;
        var phase = Environment.GetEnvironmentVariable("QUOTA_TEST_PHASE")!;
        var flags = new Dictionary<string, object>
        {
            ["backendPath"] = home, ["isolatedHome"] = home != source,
            ["isolatedSqlite"] = Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME") == home,
            ["isolatedCwd"] = Directory.GetCurrentDirectory() == home,
            ["ephemeralAuth"] = args.Contains("cli_auth_credentials_store=\"ephemeral\""),
            ["pluginsOff"] = args.Contains("features.plugins=false"),
            ["codeHostOff"] = args.Contains("features.code_mode_host=false"),
            ["apiKeysAbsent"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") == null && Environment.GetEnvironmentVariable("CODEX_API_KEY") == null
        };
        void Save() => File.WriteAllText(report, JsonSerializer.Serialize(flags));
        void Send(object message) { Console.WriteLine(JsonSerializer.Serialize(message)); Console.Out.Flush(); }
        while (await Console.In.ReadLineAsync() is { } line)
        {
            var request = JsonDocument.Parse(line).RootElement;
            if (!request.TryGetProperty("method", out var method)) continue;
            if (method.GetString() == "initialized") continue;
            var id = request.GetProperty("id").Clone();
            if (method.GetString() == "initialize") Send(new { id, result = new { } });
            else if (method.GetString() == "account/login/start")
            {
                var parameters = request.GetProperty("params");
                flags["noRefreshToken"] = parameters.GetProperty("type").GetString() == "chatgptAuthTokens" && !line.Contains("fixture-refresh-token");
                Send(new { id, result = new { type = "chatgptAuthTokens" } }); Save();
            }
            else if (method.GetString() == "account/read")
            {
                Send(new { id, method = "account/chatgptAuthTokens/refresh", @params = new { reason = "unauthorized", previousAccountId = "fixture-account" } });
                var answer = JsonDocument.Parse((await Console.In.ReadLineAsync())!).RootElement;
                var stage = File.Exists(phase) ? File.ReadAllText(phase) : "initial";
                if (stage == "renewed") flags["newAccessSupplied"] = answer.TryGetProperty("result", out var renewed) && renewed.GetProperty("accessToken").GetString() == "fixture-access-two";
                else if (stage == "switched") flags["accountSwitchRejected"] = answer.TryGetProperty("error", out _);
                else flags["refreshErrorAnswered"] = answer.TryGetProperty("error", out _);
                Send(new { id = "unknown-server-id", method = "unsupported/request", @params = new { } });
                var unknown = JsonDocument.Parse((await Console.In.ReadLineAsync())!).RootElement;
                flags["unknownRequestAnswered"] = unknown.GetProperty("id").GetString() == "unknown-server-id" && unknown.GetProperty("error").GetProperty("code").GetInt32() == -32601;
                Save(); Send(new { id, result = new { account = new { type = "chatgpt", planType = "plus" } } });
            }
            else if (method.GetString() == "account/usage/read") return;
        }
    }
}
