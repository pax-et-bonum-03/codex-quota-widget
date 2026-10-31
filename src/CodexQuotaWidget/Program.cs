using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace CodexQuotaWidget;

public static class Startup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "CodexQuotaWidget";
    private static string Entry => "\"" + Path.Combine(AppContext.BaseDirectory, "CodexQuotaWidget.exe") + "\"";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return string.Equals(key?.GetValue(Name) as string, Entry, StringComparison.OrdinalIgnoreCase); } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        if (enabled)
        {
            key.SetValue(Name, Entry, RegistryValueKind.String);
        }
        else if(string.Equals(key.GetValue(Name) as string,Entry,StringComparison.OrdinalIgnoreCase)) key.DeleteValue(Name, false);
    }
}

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            WidgetPaths.Initialize();
            if (args.Length > 0 && args[0] == "--install-startup") { Startup.Set(true); return 0; }
            if (args.Length > 0 && args[0] == "--remove-startup") { Startup.Set(false); return 0; }
            if (args.Length == 2 && args[0] == "--self-test") { SelfTest(args[1]); return 0; }
            if (args.Length == 2 && args[0] == "--check-data") { CheckData(args[1]).GetAwaiter().GetResult(); return 0; }
            if (args.Length == 2 && args[0] == "--render-preview")
            {
                var previewApp = new System.Windows.Application();
                var preview = new MainWindow(preview: true);
                preview.RenderPreview(args[1]);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--preview-window")
            {
                // Local integration checks can exercise the window without opening account connections.
                var previewApp = new System.Windows.Application();
                previewApp.Run(new MainWindow(preview: true));
                return 0;
            }
            using var mutex = new Mutex(true, @"Local\CodexQuotaWidget-v1", out bool first);
            if (!first) return 0;
            var app = new System.Windows.Application();
            app.Run(new MainWindow());
            return 0;
        }
        catch (Exception error)
        {
            if (args.Length == 2)
            {
                File.WriteAllText(args[1], JsonSerializer.Serialize(new { ok = false, error = error is InvalidOperationException ? error.Message : error.GetType().Name }));
            }
            else MessageBox.Show(Localization.Current.Text("error.start", error.GetType().Name), Localization.Current.Text("app.title"));
            return 1;
        }
    }

    private static async Task CheckData(string path)
    {
        using var rpc = await CodexRpc.Connect();
        var account = await rpc.Call("account/read", new { refreshToken = false });
        var limits = Metrics.ParseLimits(await rpc.Call("account/rateLimits/read", new { excludeResetCreditDetails = true }));
        var usage = Metrics.ParseUsage(await rpc.Call("account/usage/read"), Metrics.LocalDay);
        var a = account.GetProperty("account");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            ok = true, accountType = Metrics.Text(a, "type"), plan = Metrics.Text(a, "planType"),
            fiveHourRemainingPercent = limits.Five == null ? (double?)null : Metrics.Remaining(limits.Five),
            weeklyRemainingPercent = limits.Week == null ? (double?)null : Metrics.Remaining(limits.Week),
            fiveHourResetUnixSeconds = limits.Five?.ResetsAt, weeklyResetUnixSeconds = limits.Week?.ResetsAt,
            fiveHourResetBeijing = Metrics.ResetTime(limits.Five?.ResetsAt, full: true),
            weeklyResetBeijing = Metrics.ResetTime(limits.Week?.ResetsAt, full: true),
            todayDate = Metrics.LocalDay, todayTokens = usage.Today, lifetimeTokens = usage.Lifetime,
            latestOfficialDailyDate = usage.LatestDate, firstOfficialDailyDate = usage.FirstDate,
            startupEnabled = Startup.Enabled
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SelfTest(string path)
    {
        Localization.Current.Select("zh-CN", persist: false);
        var checks = 0;
        void Assert(bool ok, string name) { if (!ok) throw new InvalidOperationException("检查失败：" + name); checks++; }
        JsonElement Parse(string json) { using var d = JsonDocument.Parse(json); return d.RootElement.Clone(); }
        var limits = Metrics.ParseLimits(Parse("""
            {"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":18,"windowDurationMins":10080,"resetsAt":1791350196},"secondary":{"usedPercent":8,"windowDurationMins":300,"resetsAt":1790920076}},"base_model_inference":{"primary":{"usedPercent":90,"windowDurationMins":10080}}},"rateLimits":{"primary":{"usedPercent":99,"windowDurationMins":300}}}
            """));
        Assert(limits.Five != null && Metrics.Remaining(limits.Five) == 92, "按窗口时长识别 5h，优先 Codex 桶");
        Assert(limits.Week != null && Metrics.Remaining(limits.Week) == 82, "忽略其他模型桶，按窗口识别周限额");
        var legacy = Metrics.ParseLimits(Parse("""{"rateLimits":{"primary":{"usedPercent":25,"windowDurationMins":300},"secondary":null}}"""));
        Assert(legacy.Five != null && legacy.Week == null, "兼容旧响应和缺失周数据");
        var missing = Metrics.ParseLimits(Parse("""{"rateLimits":{"primary":{"usedPercent":null,"windowDurationMins":300}}}"""));
        Assert(missing.Five == null, "缺失百分比不能当作 0");
        Assert(Metrics.Remaining(new Quota(120, null)) == 0 && Metrics.Remaining(new Quota(-8, null)) == 100, "比例范围限制");
        var usage = Metrics.ParseUsage(Parse("""{"summary":{"lifetimeTokens":1087118362},"dailyUsageBuckets":[{"startDate":"2026-10-01","tokens":1935530},{"startDate":"2026-09-07","tokens":11718575}]}"""), "2026-10-02");
        Assert(usage.Today == null && usage.LatestDate == "2026-10-01", "缺失今日数据不能显示 0");
        Assert(usage.Lifetime == 1087118362 && usage.FirstDate == "2026-09-07", "账户官方累计与无序日期桶");
        var zero = Metrics.ParseUsage(Parse("""{"summary":{"lifetimeTokens":null},"dailyUsageBuckets":[{"startDate":"2026-10-02","tokens":0}]}"""), "2026-10-02");
        Assert(zero.Today == 0 && zero.Lifetime == null, "显式零与未知累计有区别");
        var absent = Metrics.ParseUsage(Parse("""{"summary":{"lifetimeTokens":null},"dailyUsageBuckets":null}"""), "2026-10-02");
        Assert(absent.Today == null && absent.LatestDate == null, "空每日统计兼容");
        Assert(Metrics.Compact(1087118362) == "1.09B" && Metrics.Compact(null) == "—", "大数显示与未知值");
        Assert(Metrics.Compact(162997886) == "163.00M" && Metrics.Compact(900) == "900", "token 显示精度");
        Assert(Metrics.Countdown(null) == "重置时间未提供" && Metrics.Countdown(1) == "等待额度更新", "未知和过期重置时间");
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok = true, checksPassed = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
