using System.Text.Json;
using CodexQuotaWidget;

if (args.Length < 1) throw new ArgumentException("Supply a new results directory and optionally an installed OpenAI codex.exe.");
var root = Path.GetFullPath(args[0]);
if (Directory.Exists(root)) throw new ArgumentException("Results directory must be new.");
Directory.CreateDirectory(root);
var checks = new List<string>();
void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
var official = Path.Combine(root, "installed");
var version = Path.Combine(official, "version"); Directory.CreateDirectory(version);
var installed = Path.Combine(version, "codex.exe"); File.WriteAllText(installed, "unsigned fixture");
var poison = Path.Combine(root, "poison"); Directory.CreateDirectory(poison);
var fake = Path.Combine(poison, "codex.exe"); File.WriteAllText(fake, "unsigned executable; never run");
var candidates = TrustedCodex.Candidates(official, poison + ";;.;relative;\\\\server\\share;" + poison);
Check(candidates[0] == installed, "Official installation precedes PATH");
Check(candidates.Count(p => p == fake) == 1, "Duplicate PATH candidates are removed");
Check(candidates.All(Path.IsPathFullyQualified), "Relative and empty PATH entries are ignored");
Check(candidates.All(p => !p.StartsWith("\\\\")), "UNC PATH entries are ignored without network access");
Check(TrustedCodex.TryOpen(fake) == null, "Unsigned executable is rejected");
Check(TrustedCodex.TryOpen("codex.exe") == null, "Relative executable path is rejected");
Check(TrustedCodex.TryOpen("\\\\server\\share\\codex.exe") == null, "UNC executable path is rejected");
using (File.Open(fake, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(true, "Rejected file handle is released");
var wrong = Path.Combine(root, "other-publisher"); Directory.CreateDirectory(wrong);
var wrongFile = Path.Combine(wrong, "codex.exe");
File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), wrongFile);
Check(TrustedCodex.TryOpen(wrongFile) == null, "Microsoft-signed executable is not accepted as OpenAI");
var skipped = new List<string>();
if (args.Length > 1)
{
    var trustedDir = Path.Combine(root, "trusted"); Directory.CreateDirectory(trustedDir);
    var trustedCopy = Path.Combine(trustedDir, "codex.exe"); File.Copy(args[1], trustedCopy);
    using (var trusted = TrustedCodex.TryOpen(trustedCopy))
    {
        Check(trusted != null, "Installed OpenAI-signed Codex passes Windows trust verification");
        bool locked = false;
        try { using var write = File.Open(trustedCopy, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }
        catch (IOException) { locked = true; }
        Check(locked, "Verified executable cannot be opened for replacement while trusted");
    }
    using (File.Open(trustedCopy, FileMode.Open, FileAccess.Write, FileShare.None)) Check(true, "Verified file lock is released on dispose");
    var alteredDir = Path.Combine(root, "tampered"); Directory.CreateDirectory(alteredDir);
    var altered = Path.Combine(alteredDir, "codex.exe"); File.Copy(args[1], altered);
    using (var stream = File.Open(altered, FileMode.Open, FileAccess.ReadWrite))
    {
        stream.Position = 4096; int value = stream.ReadByte(); stream.Position = 4096; stream.WriteByte((byte)(value ^ 1));
    }
    Check(TrustedCodex.TryOpen(altered) == null, "Modified OpenAI executable fails signature verification");
}
else skipped.Add("Positive OpenAI signature, locked file and tampered OpenAI file checks require an installed Codex path.");
var junction = Path.Combine(root, "junction");
try
{
    Directory.CreateSymbolicLink(junction, poison);
    Check(TrustedCodex.TryOpen(Path.Combine(junction, "codex.exe")) == null, "Reparse-point candidate is rejected");
}
catch (Exception e) when (e is UnauthorizedAccessException or IOException) { skipped.Add("Symlink creation is unavailable for this test user."); }
var json = JsonSerializer.Serialize(new { ok = true, checksPassed = checks.Count, checks, skipped }, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(root, "checks.json"), json); Console.WriteLine(json);

namespace CodexQuotaWidget
{
    internal sealed class WidgetException(string key) : InvalidOperationException(key) { }
}
