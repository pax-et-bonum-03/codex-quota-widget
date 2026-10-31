using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CodexQuotaWidget;

// Keep the verified file locked until the backend exits. Never send credentials
// to an executable selected only by its name or its position in PATH.
internal sealed class TrustedCodex : IDisposable
{
    private readonly FileStream file;
    internal string Path { get; }
    private TrustedCodex(string path, FileStream file) { Path = path; this.file = file; }

    internal static TrustedCodex Open()
    {
        var root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        bool found = false;
        foreach (var path in Candidates(root, Environment.GetEnvironmentVariable("PATH")))
        {
            if (!File.Exists(path)) continue;
            found = true;
            var verified = TryOpen(path);
            if (verified != null) return verified;
        }
        throw new WidgetException(found ? "error.codexUntrusted" : "error.codexMissing");
    }

    internal static IReadOnlyList<string> Candidates(string root, string? searchPath)
    {
        var paths = new List<string>();
        try
        {
            if (Directory.Exists(root) && !HasReparsePoint(root))
            {
                var installed = Directory.EnumerateDirectories(root)
                    .Where(dir => !HasReparsePoint(dir))
                    .Select(dir => System.IO.Path.Combine(dir, "codex.exe"))
                    .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
                paths.AddRange(installed);
                paths.Add(System.IO.Path.Combine(root, "codex.exe"));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        foreach (var segment in (searchPath ?? "").Split(System.IO.Path.PathSeparator))
        {
            var dir = segment.Trim().Trim('"');
            // Empty/relative PATH entries and network paths are never credential recipients.
            if (!IsLocalAbsolute(dir)) continue;
            try { paths.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "codex.exe"))); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException) { }
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static TrustedCodex? TryOpen(string path)
    {
        FileStream? locked = null;
        try
        {
            if (!OperatingSystem.IsWindows() || !IsLocalAbsolute(path)) return null;
            var full = System.IO.Path.GetFullPath(path);
            if (!string.Equals(System.IO.Path.GetFileName(full), "codex.exe", StringComparison.OrdinalIgnoreCase) || HasReparsePoint(full)) return null;
            locked = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!VerifyPublisher(full, locked)) return null;
            var result = new TrustedCodex(full, locked);
            locked = null;
            return result;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or CryptographicException or DllNotFoundException or EntryPointNotFoundException) { return null; }
        finally { locked?.Dispose(); }
    }

    private static bool IsLocalAbsolute(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path) || path.Length < 3 || !char.IsAsciiLetter(path[0])
            || path[1] != ':' || path[2] != '\\' && path[2] != '/') return false;
        // Reject mapped network drives before even probing for a file on them.
        try { return new DriveInfo(path.Substring(0, 3)).DriveType == DriveType.Fixed; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    private static bool HasReparsePoint(string path)
    {
        for (var current = System.IO.Path.GetFullPath(path); current != null; current = System.IO.Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    private static bool VerifyPublisher(string path, FileStream file)
    {
        var info = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), FilePath = path, FileHandle = file.SafeFileHandle.DangerousGetHandle() };
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, FileInfo = buffer, StateAction = 1,
            // Use Windows Authenticode policy and check revocation, excluding the root.
            ProviderFlags = 0x80 };
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            Marshal.StructureToPtr(info, buffer, false);
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
            var provider = WTHelperProvDataFromStateData(data.StateData);
            var signer = provider == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            var cert = signer == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvCertFromChain(signer, 0);
            if (cert == IntPtr.Zero) return false;
            var context = Marshal.PtrToStructure<ProviderCertificate>(cert).Certificate;
            if (context == IntPtr.Zero) return false;
            using var certificate = new X509Certificate2(context);
            // Read the signer accepted by WinVerifyTrust, not an arbitrary embedded certificate.
            return string.Equals(certificate.GetNameInfo(X509NameType.SimpleName, false), "OpenAI OpCo, LLC", StringComparison.Ordinal);
        }
        finally
        {
            data.StateAction = 2;
            WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFile>(buffer);
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose() => file.Dispose();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile { internal uint Size; [MarshalAs(UnmanagedType.LPWStr)] internal string FilePath; internal IntPtr FileHandle; internal IntPtr KnownSubject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        internal uint Size; internal IntPtr PolicyCallback; internal IntPtr SipClient; internal uint UiChoice; internal uint Revocation;
        internal uint UnionChoice; internal IntPtr FileInfo; internal uint StateAction; internal IntPtr StateData; internal IntPtr Url;
        internal uint ProviderFlags; internal uint UiContext; internal IntPtr SignatureSettings;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProviderCertificate { internal uint Size; internal IntPtr Certificate; }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint index, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterIndex);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint index);
}
