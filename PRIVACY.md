# 隐私说明 · Privacy notice

[中文](#中文) · [English](#english)

## 中文

本工具面向 ChatGPT Plus 用户的 Codex 工作流深度定制。

本工具是独立开发的 Windows 悬浮窗，与 OpenAI 官方产品没有隶属关系。

工具启动本机 Codex 的独立 app-server，通过本地标准输入/输出读取当前登录账户信息、5h/周限额与 Codex 账户 token 统计。后台使用本工具独立的临时目录，不共用主程序的配置、SQLite 数据库、插件和 Code Mode host。账户请求由 Codex 处理，并会连接 OpenAI 服务。

1.4.7 在读取账户凭据前，先验证 Codex 可执行文件的 Windows Authenticode 签名与 OpenAI 发布者身份，并在后台存续期间锁定该文件，防止验证后被写入或替换。优先选择官方安装目录；PATH 回退也执行同样验证。不接受网络驱动器、UNC、相对路径及符号链接/联接点。验证失败时不会启动该后台或向其传递凭据。

工具只读 Codex 的 `auth.json`，取出当前访问凭据和账户标识，通过本地管道传给临时后台；凭据仅保存在进程内存中，不复制登录文件，不写账户凭据，不使用刷新凭据。凭据续期、登录和退出仍由 Codex 主程序处理。当前支持文件形式的 Codex 登录缓存；仅存于系统凭据库的登录不受支持。访问凭据过期且主程序尚未续期时，会暂停读取并提示在 Codex 中登录。工具不要求 API key，不发送模型生成请求，不读取聊天正文。

界面所需用量保存在进程内存中；没有开发者自建的统计服务器、广告或遥测。临时后台可能生成 Codex 内部状态和诊断文件，位于 `data/backend/<随机会话标识>`，正常退出后关闭该后台并删除本会话目录；强制结束进程时可能留下临时状态。手动执行诊断命令可能生成包含账户用量的文件，分享前请检查内容。

默认本地文件位于 `%LOCALAPPDATA%/CodexQuotaWidget`：`app` 保存程序、说明和许可证，`install.json` 保存安装版本和管理文件清单，`data` 保存语言选择、窗口位置及用户导入的 JSON 语言包。升级保留 `data`；卸载程序后也保留，用户可自行删除该目录。开机启动仅在用户主动启用时写入当前用户的 Windows 启动项，不要求管理员权限。

累计 token 来自官方 Codex 账户统计，包括服务端记录的其他设备 Codex 用量；不包含普通 ChatGPT 网页聊天的所有 token。今日日期桶可能延迟、其日界时区未公开；缺失时显示“尚未更新”。

来源：[Codex App Server](https://learn.chatgpt.com/docs/app-server#7-token-usage-chatgpt)。官方接口变化可能影响可用性。

---

## English

This is an independently developed Windows widget, deeply tailored to ChatGPT Plus users' Codex workflows. It is not affiliated with OpenAI.

The widget starts a separate local Codex app-server and reads the currently signed-in account, 5-hour/weekly limits, and Codex account token statistics through local standard input/output. Its backend uses a temporary directory belonging to this tool and does not share the desktop app's configuration, SQLite database, plugins, or Code Mode host. Codex handles account requests and connects to OpenAI services.

Version 1.4.7 verifies the Codex executable’s Windows Authenticode signature and OpenAI publisher before reading account credentials, and holds a read-only file lock for the backend lifetime to prevent writing or replacing the verified file. The official installation directory is preferred; PATH fallbacks receive the same checks. Network drives, UNC and relative paths, symbolic links, and junctions are rejected. Verification failure prevents that backend from starting or receiving credentials.

The widget reads Codex's `auth.json` without modifying it. It extracts the current access credential and account identifier and passes them to the temporary backend through a local pipe. Credentials remain in process memory: the login file is not copied, account credentials are not written to disk, and refresh credentials are not used. Credential renewal, sign-in, and sign-out remain the responsibility of the Codex desktop app. File-based login caches are supported; logins stored only in the system credential store are not supported. If the access credential expires before the desktop app renews it, reading pauses and the widget asks you to sign in through Codex. No API key is required, no model-generation requests are sent, and chat messages are not read.

Usage data needed by the interface stays in process memory. There is no developer-operated analytics server, advertising, or telemetry. The temporary backend may generate internal Codex state and diagnostic files under `data/backend/<random-session-id>`. On normal exit, its backend is closed and that session directory is deleted; forcibly terminating the process may leave temporary state behind. Manually running diagnostic commands may produce files containing account usage; inspect those files before sharing them.

Local files default to `%LOCALAPPDATA%/CodexQuotaWidget`. The `app` directory holds program files, documentation, and licenses; `install.json` records the installed version and managed files; `data` stores language choice, window position, and imported JSON language packs. Upgrades preserve `data`, as does uninstalling. You may remove that directory yourself after quitting. Startup at sign-in is added to the current Windows user's startup entries only when explicitly enabled by the user. Administrator privileges are not required.

Lifetime token counts come from official Codex account statistics, including usage recorded for other devices. They are not the total for ordinary ChatGPT web chats. Daily buckets may be delayed, and their day-boundary time zone is not documented. Missing current-day data is displayed as “Not updated yet.”

Source: [Codex App Server](https://learn.chatgpt.com/docs/app-server#7-token-usage-chatgpt). Changes to the upstream interfaces may affect availability.
