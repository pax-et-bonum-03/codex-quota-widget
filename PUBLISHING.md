# 发布说明 · Publishing guide

[中文](#中文) · [English](#english)

## 中文

作者 **suqi456**；仓库：[pax-et-bonum-03/codex-quota-widget](https://github.com/pax-et-bonum-03/codex-quota-widget)。当前发行版本为插件 **1.1.6**、桌面程序 **1.4.7**，标签为 `plugin-v1.1.6`。本项目面向 ChatGPT Plus 用户的 Codex 工作流深度定制，未提交官方插件目录。

### 发行内容

仓库包含源码、说明、原始字体许可证、插件与 marketplace 清单。不要发布账户诊断、登录记录、个人偏好、临时输出或机器专用路径。

Release 提供 Windows 安装包、Codex 插件包、源码包和 `SHA256SUMS.txt`。发布正文来自 `docs/RELEASE_NOTES.md`；README 使用实际仓库和下载地址。

原始 `LICENSE` 与字体许可证保留英文原文，`LICENSE.zh-CN.md` 提供 MIT 许可证中文参考译文。双语组件说明位于 `THIRD_PARTY_NOTICES.md`。

### 本地打包

在 Windows 的仓库根目录运行：

```powershell
pwsh -NoProfile -File .\scripts\package.ps1
```

文件写入 `dist`；输出目录中不得已有同名文件。脚本从插件与 payload 清单读取版本，核对受管文件的大小和 SHA-256，使用 Git 跟踪的文件创建压缩包，并生成校验文件。`.gitattributes` 保留文件字节和换行，防止安装时的哈希检查失效。

调整 payload 中的文档后，先同步仓库与插件里的副本，再更新 `payload.json` 中对应文件的大小和哈希。文档分发内容变更时提升插件版本；桌面程序版本仅在实际程序变更时调整。1.1.6 / 1.4.7 使用独立公开历史和匿名提交邮箱；保留既有信任核验，并在新仓库重新构建程序。

### GitHub 发布

`.github/workflows/publish.yml` 的工作流负责校验、打包和上传。源码、脚本、测试、插件或指定文档变更可触发，也可在 Actions 页面手动运行。先从源码构建并运行两组安全检查，再打包；两个 Action 均固定完整提交 SHA，checkout 不持久化凭据。新发布必须先成功生成 GitHub 签名构建来源证明，另附 build-provenance.sigstore.json。

工作流使用当次运行提供的 GitHub token，发布作业仅授予仓库内容写入、构建来源证明写入与短期 OIDC 身份权限；不需要把个人 token 写入源码或工作流。新标签先创建草稿，上传并校验附件大小和服务端可用的 SHA-256 后再公开。已有标签只更新介绍，不替换原附件；需要新版下载包时，应先提高插件版本再发布。

从 GitHub 添加 marketplace：

```powershell
codex plugin marketplace add pax-et-bonum-03/codex-quota-widget
codex plugin add codex-quota-widget@codex-quota-widget-releases
```

### 官方目录与兼容范围

包中包含技能、说明、图标和作者署名，没有 MCP 服务。功能是管理本地 Windows 悬浮程序，运行范围为 Windows 上的 Codex；ChatGPT 网页与手机端不能直接运行这个 Windows 程序。

GitHub 发布不代表已提交或通过官方目录审核。若准备提交，应重新核对届时的官方身份验证、打包和审核要求。历史文档链接：[打包](https://developers.openai.com/plugins/build/plugins)、[提交](https://developers.openai.com/plugins/deploy/submission)；原记录核对日期为 **2026-10-05**，不是本次重新核对。

### 已知分发限制

- exe 与 PowerShell 脚本尚未进行发布者数字签名。
- 依赖 Windows x64、.NET 8 Desktop Runtime 和 Codex 登录状态，不捆绑系统运行时。
- Codex 账户用量接口及可用字段可能变化；统计范围、延迟和时区见双语 README 与使用说明。
- 另一台全新 Windows 实机和 Windows ARM64 的验证尚未完成；现有历史检查记录见 `docs/RELEASE_CHECKS.md`。

---

## English

Author: **suqi456**. Repository: [pax-et-bonum-03/codex-quota-widget](https://github.com/pax-et-bonum-03/codex-quota-widget). Current distribution: **plugin 1.1.6 / desktop 1.4.7**, tagged `plugin-v1.1.6`. This project is deeply tailored to ChatGPT Plus users' Codex workflows and has not been submitted to the official plugin directory.

### Distribution contents

The repository includes source code, documentation, original font licenses, the plugin, and the marketplace manifest. Do not publish account diagnostics, login records, personal preferences, temporary output, or machine-specific paths.

A release contains the Windows installation package, Codex plugin package, source package, and `SHA256SUMS.txt`. Its description comes from `docs/RELEASE_NOTES.md`. The README uses actual repository and download links.

The original `LICENSE` and font licenses remain in English. `LICENSE.zh-CN.md` provides a Chinese reference translation of the MIT License. Bilingual component notices are in `THIRD_PARTY_NOTICES.md`.

### Package locally

Run from the repository root on Windows:

```powershell
pwsh -NoProfile -File .\scripts\package.ps1
```

Files are written to `dist`; files with the same names must not already exist there. The script reads versions from the plugin and payload manifests, checks managed-file sizes and SHA-256 hashes, packages Git-tracked files, and writes checksums. `.gitattributes` preserves exact file bytes and line endings so installation hash checks remain valid.

After editing payload documentation, synchronize the repository/plugin copies and update the corresponding sizes and hashes in `payload.json`. Increment the plugin version when distribution documentation changes. Change the desktop version only when the program itself changes. Plugin 1.1.6 / desktop 1.4.7 uses independent public history and noreply commit addresses, retains executable-trust hardening, and rebuilds in this repository.

### Publish on GitHub

The workflow in `.github/workflows/publish.yml` validates, packages, and uploads the release. Source, scripts, tests, plugin, and specified documentation changes trigger it; it can also be started manually. It builds from source and runs both security test suites before packaging. Both Actions are pinned to full commit SHAs, and checkout does not persist credentials. New releases require a successful signed GitHub provenance attestation and include build-provenance.sigstore.json.

The workflow uses the GitHub token supplied for that run, with contents-write, attestations-write, and short-lived OIDC identity permissions restricted to the publishing job. Personal tokens do not belong in source or workflow files. A new tag is first published as a draft. Assets are uploaded and checked for size and, when provided by the server, SHA-256 digest before the release becomes public. An existing tag updates its introduction only and does not replace its original assets. Increment the plugin version before publishing updated download packages.

Add the GitHub marketplace with:

```powershell
codex plugin marketplace add pax-et-bonum-03/codex-quota-widget
codex plugin add codex-quota-widget@codex-quota-widget-releases
```

### Official directory and supported environment

The package includes a skill, documentation, icons, and author attribution, with no MCP service. It manages a local Windows widget and targets Codex on Windows. ChatGPT in a web browser or on a phone cannot directly run this Windows program.

GitHub publication does not mean the project has been submitted to or accepted by the official directory. Before any submission, recheck the then-current identity-verification, packaging, and review requirements. Historical documentation links: [packaging](https://developers.openai.com/plugins/build/plugins) and [submission](https://developers.openai.com/plugins/deploy/submission). Their original recorded check date was **2026-10-05**; they were not rechecked for this translation.

### Known distribution limitations

- The executable and PowerShell scripts are not publisher-signed.
- Windows x64, the .NET 8 Desktop Runtime, and an active Codex login are required. System runtimes are not bundled.
- Codex account-usage interfaces and available fields may change. See the bilingual README and user guide for statistical scope, reporting delays, and time zone.
- Another clean Windows machine and Windows ARM64 remain unverified. Historical validation records are in `docs/RELEASE_CHECKS.md`.
