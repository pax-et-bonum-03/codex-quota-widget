---
name: quota-widget
description: "安装、启动、停止、检查或卸载 Windows x64 的 Codex 用量悬浮窗；用于顶部 5h/周额度小窗。 Manage the Windows Codex quota widget: install, start, stop, inspect or uninstall the floating 5h/weekly usage display."
---

# Codex 用量悬浮窗 · Codex Quota Widget

## 中文

本插件管理独立 Windows 程序，需要 .NET 8 Desktop Runtime 和已登录的 Codex。先根据用户请求确定动作；启动可自动安装或升级，不自动启用开机启动。

在 Windows PowerShell 中运行本技能目录的 `scripts/widget.ps1`，传入 `-Action start`、`status`、`stop`、`install` 或 `uninstall`。使用文件的实际绝对路径。读取返回 JSON 后报告真实结果；启动已获授权时不用再确认。失败时说明报错，不把命令执行成功当作窗口已运行。

程序安装到 `%LOCALAPPDATA%/CodexQuotaWidget/app`，偏好保存在 `data`。脚本校验包内文件和运行环境，只关闭指定路径的本工具进程，不接管其他路径的副本。不要为了绕过检测而强制结束其他副本；可提示用户从其托盘退出。缺少 Desktop Runtime 时提供脚本返回的微软官方地址，不默默下载或全局安装依赖。

`uninstall` 仅删除所记录程序文件及指向它的启动项，保留用户数据；只有用户明确要求卸载程序时才调用。插件启用或移除与桌面进程、开机启动是独立状态，不能声称关闭插件开关就结束程序。不要使用开发测试的 `-Preview` 代替真实账户窗口。

语言、拖动复原和开机启动可通过悬浮窗或托盘菜单设置。用户只问操作入口时，简短说明即可。

额度来自当前 Codex 登录的 ChatGPT 订阅账户，使用 Codex 账户级统计。显示剩余百分比，所有恢复时间用北京时间。没有今日数据时显示尚未更新；累计不是普通 ChatGPT 网页聊天的 token 总数。不要把演示图片、历史数值或诊断文件当作当前账户数据。工具无需 API key，不发送模型请求。

---

## English

This plugin manages a separate Windows application, deeply tailored to ChatGPT Plus users. It requires the .NET 8 Desktop Runtime and an active Codex login. Select the action requested by the user. Starting can install or upgrade the application, but must not automatically enable startup at sign-in.

Run this skill's `scripts/widget.ps1` in Windows PowerShell with `-Action start`, `status`, `stop`, `install`, or `uninstall`, using the script's actual absolute path. Read the returned JSON and report the observed result. Do not reconfirm a start that the user has already authorized. On failure, explain the error; successful command execution alone does not establish that the widget is running.

The application installs under `%LOCALAPPDATA%/CodexQuotaWidget/app`; preferences are under `data`. The script checks bundled files and runtime requirements and stops only this tool's processes at its specified installation path. It does not take over copies at other paths. Do not forcibly stop other copies to bypass detection; ask the user to quit those copies through their tray menu. If the Desktop Runtime is missing, provide the official Microsoft link returned by the script rather than silently downloading or globally installing dependencies.

`uninstall` removes only recorded application files and the startup entry pointing to that application, preserving user data. Use it only when the user explicitly requests removal of the desktop program. Plugin enablement/removal, the desktop process, and Windows startup are independent states; do not claim that disabling the plugin ends the program. Do not substitute the development `-Preview` option for the real-account widget.

Language, dragging/reset, and startup options are available from the widget or tray menu. If the user asks only where to find an option, a brief explanation is enough.

Readings come from the ChatGPT subscription account currently signed in to Codex and use Codex account-level statistics. Show remaining percentages and use Beijing time for reset times. If today's data is missing, show “Not updated yet.” Lifetime tokens are not the total for ordinary ChatGPT web chats. Do not treat demo images, historical readings, or diagnostic files as current account data. The tool requires no API key and sends no model-generation requests.
