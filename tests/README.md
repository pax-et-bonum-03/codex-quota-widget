# 测试入口 · Test guide

[中文](#中文) · [English](#english)

## 中文

需要 Windows x64 和 .NET 8 Desktop Runtime。先按根目录说明构建，或使用包内程序。测试不连接账户；所有文件写到 `.build` 或显式指定的隔离目录。运行交互/启动检查前先退出其他悬浮窗副本。

```powershell
# Windows PowerShell 5.1，校验安装、升级、失败拦截及卸载保留设置
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\install-smoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\migration-smoke.ps1

# PowerShell 7 的 .NET 环境，检查 WPF 排版、动画、语言和原生窗口交互
pwsh -NoProfile -STA -File .\tests\ui-smoke.ps1
pwsh -NoProfile -STA -File .\tests\interaction-smoke.ps1
pwsh -NoProfile -STA -File .\tests\fold-transition.ps1
pwsh -NoProfile -STA -File .\tests\window-motion.ps1
```

UI 测试生成三种内置语言、四档缩放的离屏预览，使用虚构固定数据。交互测试会短暂显示测试窗口、打开自己的菜单和托盘图标；不会模拟点击其他软件。安装/迁移测试要求全新的输出目录，保留测试结果供核查。

发行包检查同时确认未附带 PDB、私人设置、登录凭据或本机路径。它不能证明所有安全风险均不存在，也不能代替另一台干净 Windows 电脑的实机验证。

账户隔离回归检查需要 .NET 8 SDK，编译真实 RPC/账户读取源文件并使用本地模拟后台，测试不连接真实账户：

```powershell
dotnet publish .\tests\account-isolation\AccountIsolationChecks.csproj -c Release -o .\.build\account-isolation
# 输出目录必须尚未存在
& .\.build\account-isolation\AccountIsolationChecks.exe .\.build\account-isolation-results
```

覆盖后台目录与主程序隔离、访问凭据仅经管道传递、未传刷新凭据、服务端请求 ID 与客户端请求 ID 冲突、续期回调立即应答、跨账户刷新拒绝、退出清理及登录缓存损坏。

文档更新应核对双语覆盖、Markdown 结构、重复文档一致性、payload 哈希与压缩包内容；不得把历史界面或真实账户记录声称为本次重新执行的测试。

---

## English

Requires Windows x64 and the .NET 8 Desktop Runtime. Build using the root instructions or use the bundled program. Tests do not connect to an account. All output goes to `.build` or an explicitly selected isolated directory. Quit other widget copies before running interaction or startup checks.

```powershell
# Windows PowerShell 5.1: installation, upgrades, tamper rejection,
# and uninstall while retaining preferences
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\install-smoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\migration-smoke.ps1

# PowerShell 7: WPF layout, animation, languages, and native interaction
pwsh -NoProfile -STA -File .\tests\ui-smoke.ps1
pwsh -NoProfile -STA -File .\tests\interaction-smoke.ps1
pwsh -NoProfile -STA -File .\tests\fold-transition.ps1
pwsh -NoProfile -STA -File .\tests\window-motion.ps1
```

UI tests generate off-screen previews for three built-in languages at four scale factors, using fixed fictional data. Interaction tests briefly display their own test window and open their own menus and tray icon; they do not simulate clicks in other applications. Installation and migration tests require a fresh output directory and retain their results for inspection.

Release-package checks also look for accidental PDB files, private preferences, login credentials, and machine-specific paths. They do not establish that every security risk is absent or replace testing on another clean Windows machine.

Account-isolation regression checks require the .NET 8 SDK. They compile the actual RPC/account-reading source and use a locally simulated backend without connecting to a real account:

```powershell
dotnet publish .\tests\account-isolation\AccountIsolationChecks.csproj -c Release -o .\.build\account-isolation
# The results directory must not already exist.
& .\.build\account-isolation\AccountIsolationChecks.exe .\.build\account-isolation-results
```

Coverage includes separation from the desktop app's backend directories, passing access credentials only through a pipe, excluding refresh credentials, collisions between client and server request IDs, immediate renewal-callback replies, rejection of cross-account refreshes, cleanup on exit, and malformed login caches.

For a documentation-only update, check bilingual coverage, Markdown structure, duplicate documentation consistency, payload hashes, and package contents. Do not present historical UI or live-account results as newly rerun tests.

## 程序来源核验 · Executable trust checks

在 Windows 运行以下检查。第二个程序参数可选，必须是从官方来源安装的 OpenAI 签名 codex.exe；检查仅读取签名，并对隔离副本做锁定与篡改测试，不启动 Codex、不读取账户。不提供该参数时会明确列出跳过的正向签名检查。模拟后台只存在于账户隔离测试程序集，正式程序没有绕过核验的开关。

Run the following on Windows. The optional second program argument must point to an officially installed, OpenAI-signed codex.exe. The checks read its signature and test locking/tampering on isolated copies; they do not launch Codex or read an account. Omitting it explicitly reports the positive-signature checks as skipped. The mock backend exists only in the account-isolation test assembly; the production program has no verification-bypass switch.

```powershell
dotnet publish ./tests/executable-trust/ExecutableTrustChecks.csproj -c Release -o ./.build/executable-trust
& ./.build/executable-trust/ExecutableTrustChecks.exe ./.build/trust-results
```
