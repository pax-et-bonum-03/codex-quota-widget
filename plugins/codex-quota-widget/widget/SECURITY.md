# 安全说明 · Security

## 中文

桌面程序 1.4.7 / 插件 1.1.6 面向 ChatGPT Plus 用户的 Codex 工作流深度定制，是独立项目，与 OpenAI 无隶属关系。

### 凭据接收程序的核验

在读取登录令牌前，工具优先查找 `%LOCALAPPDATA%/OpenAI/Codex/bin` 中的正式安装，再检查 PATH。所有候选都必须通过 Windows Authenticode 验证，且已验证的签名发布者须为 `OpenAI OpCo, LLC`。签名无效、无法验证或发布者不符的程序不会运行，也不会收到令牌。没有关闭此检查的运行时开关。

只接受本地固定驱动器上的绝对路径；不接受相对路径、网络驱动器、UNC、符号链接或联接点。核验成功的文件在后台存续期间保持只读锁定，阻止验证后写入、删除或替换。证书信任与吊销检查由 Windows 处理，必要时可能查询证书服务；检查在后台线程执行。若出现核验错误，请从官方来源修复或更新 Codex，勿通过关闭系统证书保护解决。

工具使用现有登录令牌查询用量。该令牌是敏感凭据，不能当作只能读额度的独立权限。工具不使用刷新令牌；后台凭据采用内存存储，通过本地管道传递。其他具有本机账户权限的恶意软件仍可能读取原有登录缓存或进程内存，因此这些检查不能代替本机安全防护。

### 核验下载来源

GitHub 发布流程从源码构建，执行账户隔离和程序信任检查，再打包；所用 Action 固定为完整提交 SHA，checkout 不保存 GitHub 凭据。下载包附 SHA-256 清单和 GitHub/Sigstore 签名的构建来源证明 `build-provenance.sigstore.json`。来源证明关联仓库、工作流及下载包摘要，用来核验构建来源；它不代表漏洞审计认证。

安装 GitHub CLI 后，可对实际下载的包执行：

```powershell
gh attestation verify ./CodexQuotaWidget-1.4.7-windows-x64.zip --repo pax-et-bonum-03/codex-quota-widget
```

本机未配置正式代码签名证书，悬浮窗 EXE/DLL 与 PowerShell 脚本仍没有 Windows 发布者签名。构建来源证明不等同于 Authenticode，可能仍出现 Windows 来源提示。今后配置真实证书时再补签，不使用自签名证书制造“已受信任”提示。

### 隐私与报告问题

不要在公开 issue、截图或日志中贴登录文件、令牌、密钥、邮箱或账户标识。错误提示仅显示固定文案。分享诊断文件前先检查其中的用量及本机信息；正常退出会清理本次后台，强制结束可能留下本地状态。详见 [隐私说明](PRIVACY.md)。

## English

Desktop 1.4.7 / plugin 1.1.6 is deeply tailored to ChatGPT Plus users' Codex workflows. It is an independent project, unaffiliated with OpenAI.

### Verifying the credential recipient

Before reading a login token, the widget prefers the official installation under `%LOCALAPPDATA%/OpenAI/Codex/bin`, then checks PATH. Every candidate must pass Windows Authenticode verification, and its verified signing publisher must be `OpenAI OpCo, LLC`. Invalid, unverifiable, or other-publisher executables are not launched and receive no token. There is no runtime option to disable verification.

Only absolute paths on local fixed drives are accepted. Relative paths, network drives, UNC paths, symbolic links, and junctions are rejected. The verified file remains read-locked for the backend lifetime to prevent subsequent writing, deletion, or replacement. Windows handles certificate trust and revocation checks and may contact certificate services when needed. Verification runs on a background thread. If verification fails, repair or update Codex from its official source; do not disable system certificate protections.

The widget queries usage with an existing login token. This is sensitive authentication material, not a separate quota-only permission. Refresh tokens are not used; backend credentials remain in memory and pass through a local pipe. Malware with equivalent local-account access could still read the original login cache or process memory, so these checks do not replace endpoint security.

### Verifying release provenance

The GitHub workflow builds from source, runs account-isolation and executable-trust checks, then packages the release. Actions are pinned to full commit SHAs, and checkout does not retain GitHub credentials. Downloads include SHA-256 checksums and signed GitHub/Sigstore build provenance in `build-provenance.sigstore.json`. Provenance binds the repository, workflow, and package digests; it is not a vulnerability-audit certification.

After installing GitHub CLI, verify the actual downloaded archive with:

```powershell
gh attestation verify ./CodexQuotaWidget-1.4.7-windows-x64.zip --repo pax-et-bonum-03/codex-quota-widget
```

No formal code-signing certificate is configured. The widget EXE/DLL and PowerShell scripts remain unsigned for Windows. Build provenance is different from Authenticode and does not remove Windows publisher warnings. Formal signing requires a real certificate; a self-signed certificate is not presented as trusted publisher verification.

### Privacy and reporting issues

Do not put login files, tokens, keys, email addresses, or account identifiers in public issues, screenshots, or logs. Errors use fixed messages. Inspect diagnostics for usage and local information before sharing. Normal exit cleans up its backend session; forced termination may leave local state. See [Privacy](PRIVACY.md).
