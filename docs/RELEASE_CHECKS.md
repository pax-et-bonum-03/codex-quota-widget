# 发布核查记录 · Release validation record

[中文](#中文) · [English](#english)

## 中文

当前版本：插件 1.1.6，程序 1.4.7，作者 suqi456。

### 2026-10-31 安全加固核查

1.4.7 新增发送凭据前的 Windows 签名及 OpenAI 发布者核验，优先官方目录并锁定已验证的文件。31 项模拟账户隔离检查通过；本机 13 项原生信任检查通过，涵盖真实 OpenAI 签名、篡改副本、错误发布者及文件锁。受本机权限限制，创建符号链接的检查明确跳过。未连接真实账户执行安全测试。

发布流程从源码构建、运行两组安全检查，固定两个 Action 的提交 SHA，并要求成功生成签名构建来源证明。没有可用的 Windows 发布者代码签名证书，此项仍未完成。以下旧版本记录保留原始日期和版本。

2026-10-06 防抖回归：旧版 1.4.4 在开合过程中出现窗口边界与文字位置漂移；1.4.5 对顶部、移动后、向上展开、屏幕边缘和中途反向切换共采样 659 个合成器画面，漂移为 0，窗口宽高未附着动画。21 个收起背景画面、27 项界面检查与 20 项交互检查通过。已替换并重启现用程序，版本为 1.4.5.0。原生窗口检查使用当前显示器 100% 缩放，其他档位为 WPF 离屏预览。

2026-10-06 修复回归：21 个收起过渡画面全部使用正确的固定尺寸背景，包括快速反复切换、向上展开及复原；27 项界面检查与 20 项拖动/菜单/托盘检查通过。已替换并重启当前电脑正在使用的悬浮窗，程序版本核验为 1.4.4.0。

以下安装与数据基线于 2026-10-05 在 1.4.3 执行，本次未更改这些实现；界面检查已在 1.4.4 重跑。

| 检查 | 结果 |
| --- | --- |
| .NET Release 构建 | 通过，无编译警告或错误 |
| 额度窗口识别、缺失值、今日/累计及零值处理 | 12 项通过 |
| 安装、升级、SHA-256 拦截、目录边界、重复启动、正常退出、卸载保留数据 | 20 项通过；在 Windows PowerShell 5.1 中执行发布脚本 |
| 旧版偏好/位置迁移、坏 JSON 隔离、语言路径约束 | 9 项通过 |
| 读数动画、排版、完整 token 单位、状态提示、长译文 | 27 项通过 |
| 拖动、展开/收起保持位置、向上展开、按钮/菜单/托盘复原、焦点、位置保存 | 20 项通过 |
| 简体中文、繁體中文、English，100/125/150/200% | 24 张 WPF 离屏渲染，已检查中文原尺寸预览；未改变实际显示器设置 |
| 技能格式 | Skill Creator 校验通过 |
| 作者、图标、marketplace 和 payload 引用 | 逐项检查文件存在、版本与哈希一致 |
| 分发内容 | 排除登录文件、个人设置、用量诊断、PDB 和本机专用路径；包含原始字体许可证 |

测试入口位于 `tests`；具体断言与运行方法公开在源码包内。安装测试用中文及空格路径，测试窗口使用 `--preview-window`，不会连接真实账户。程序首次发布默认不启用开机启动；本次没有修改现用工具的启动项。

未完成的外部验证：另一台全新 Windows 实机、Windows ARM64、正式发布者数字签名、官方插件目录审核。GitHub 发布不代表已通过官方插件目录审核。SHA-256 用于校验包完整性，不是发布者数字签名。账户数据接口仍可能随 Codex 版本改变。

### 1.4.6 账户隔离修复核查

- 31 项本地模拟后台回归检查通过，覆盖主程序文件不变、凭据续期回调、双向请求 ID、账户切换、失败连接及退出清理。
- 真实账户 5h、周额度、累计 token 读取成功，读取前后登录、配置、会话文件 SHA-256 均未改变。
- 本机已更新并运行 1.4.6；用户确认开启悬浮窗时，设置中的个人资料页面恢复正常加载。此项为用户实机确认，不是自动 UI 操作结果。
- 主程序后台继续运行，插件所有已安装备用程序均已同步。


### 2026-10-30 双语文档更新

1.1.4 发行包补齐各类说明和插件介绍的中英双语，中文与英文另起段落、左对齐。沿用已验证的桌面程序 1.4.6 文件；本次核查针对文档清单、文件哈希和压缩包内容。以上界面及真实账户结果属于历史记录，不作为本次文档更新新执行的测试。

---

## English

Current distribution: **plugin 1.1.6 / desktop 1.4.7**, author **suqi456**.

### Security hardening checks, 2026-10-31

Desktop 1.4.7 verifies the Windows signature and OpenAI publisher before passing credentials, prefers the official installation, and locks the verified executable. All 31 mock-account isolation checks passed. Thirteen local native trust checks passed, covering a real OpenAI signature, a tampered copy, another publisher, and file locks. Creating a symbolic-link fixture was explicitly skipped because of local permissions. Security tests did not connect to a real account.

The release workflow builds from source, runs both security suites, pins both Actions to full commit SHAs, and requires signed build provenance. A Windows publisher code-signing certificate is unavailable, so formal publisher signing remains outstanding. Older records below retain their original dates and versions.

### Historical interface regressions

**2026-10-06, anti-jitter regression:** version 1.4.4 showed native-window boundary and text-position drift during expansion/collapse. Version 1.4.5 sampled **659 compositor frames** across the top position, moved position, upward expansion, screen edges, and direction reversal, with zero drift and no animation attached to native-window dimensions. All **21 collapsed-background frames, 27 UI checks, and 20 interaction checks** passed. The installed version was replaced and restarted as **1.4.5.0**. Native-window checks used the current monitor at 100% scaling; other scale factors used WPF off-screen previews.

**2026-10-06, collapse-background regression:** all **21 transition frames** used the correct fixed-size background, including rapid toggling, upward expansion, and reset. All **27 UI checks** and **20 drag/menu/tray checks** passed. The installed executable was verified as **1.4.4.0**.

The installation/data baseline below was run on **2026-10-05 against 1.4.3**. Those implementations were unchanged in the subsequent interface fixes; UI checks were rerun for 1.4.4.

| Check | Recorded result |
| --- | --- |
| .NET Release build | Passed without compiler warnings or errors |
| Quota-window identification, missing values, daily/lifetime values, and zero handling | 12 checks passed |
| Installation, upgrade, SHA-256 rejection, directory boundaries, duplicate launch, normal exit, and retaining data on uninstall | 20 checks passed; release scripts executed with Windows PowerShell 5.1 |
| Legacy preference/position migration, malformed JSON isolation, and language-path restrictions | 9 checks passed |
| Value animation, layout, complete token units, status messages, and long translations | 27 checks passed |
| Dragging, stable position during expansion/collapse, upward expansion, button/menu/tray reset, focus, and saved position | 20 checks passed |
| Simplified Chinese, Traditional Chinese, and English at 100/125/150/200% | 24 WPF off-screen renders; Chinese previews inspected at native size without changing the actual display configuration |
| Skill format | Passed Skill Creator validation |
| Author, icons, marketplace, and payload references | Referenced files, versions, and hashes checked |
| Distribution contents | Login files, personal settings, usage diagnostics, PDBs, and machine-specific paths excluded; original font licenses included |

Test entry points and assertions are in `tests` in the source package. Installation checks use paths containing Chinese characters and spaces. Test windows use `--preview-window` and do not connect to a real account. Startup at sign-in is disabled by default on first installation. The historical checks did not change the current widget's startup entry.

External checks still outstanding: another clean Windows machine, Windows ARM64, publisher code signing, and official plugin-directory review. Publishing to GitHub does not imply official-directory acceptance. SHA-256 verifies package integrity, not publisher identity. Account interfaces may change with Codex versions.

### Desktop 1.4.6 account-isolation checks

- **31 local simulated-backend regression checks** passed, covering unchanged desktop-app files, credential-renewal callbacks, bidirectional request IDs, account changes, failed connections, and cleanup on exit.
- Live-account reads of 5-hour quota, weekly quota, and lifetime tokens succeeded. SHA-256 hashes of the desktop app's login, configuration, and session files were unchanged before and after reading.
- The local tool was updated to 1.4.6. The user confirmed that the profile page loaded normally while the widget was running. This was user confirmation on their machine, not an automated UI test.
- The desktop app's backend continued running, and installed plugin fallback copies were synchronized.

### Documentation update: 2026-10-30

The 1.1.4 distribution makes the user guide, privacy notice, language-pack guide, release notes, changelog, publishing instructions, test guide, and plugin descriptions bilingual. Chinese and English sections start on separate, left-aligned paragraphs.

The update preserves the desktop 1.4.6 program files. Package validation checks the updated documentation manifest and archive contents. The historical UI and live-account results above are retained as historical evidence, not claimed as new executions for this documentation update.
