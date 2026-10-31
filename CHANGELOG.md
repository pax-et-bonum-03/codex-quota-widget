# 更新记录 · Changelog

[中文](#中文) · [English](#english)

## 中文

### 插件 1.1.6 / 桌面程序 1.4.7 — 2026-10-31

- 使用独立的公开仓库历史与 GitHub 匿名提交邮箱，更新安装、下载和验证链接。
- 重新构建并验证源码、Windows 安装包和插件包，保留中英双语说明及展示图。

### 插件 1.1.5 / 桌面程序 1.4.7 — 2026-10-31

- 在读取或发送账户凭据前核验 Codex 的 Windows 签名和 OpenAI 发布者，优先官方目录；拒绝不可信和链接/网络路径。
- 后台存续期间锁定已验证的文件，阻止验证后写入或替换；没有运行时绕过签名的选项。
- 新增独立的原生签名与程序选择测试；原有账户隔离检查继续使用仅测试程序集中的模拟后台。
- 固定 GitHub Actions 完整提交 SHA，关闭 checkout 凭据持久化，从源码构建并通过安全测试后打包，生成签名构建来源证明。
- 增加中英双语安全说明与三种内置语言的验证失败提示。Windows 发布者代码签名仍未完成。

### 插件 1.1.4 / 桌面程序 1.4.6 — 2026-10-30

- 补齐使用、隐私、语言包、发布、更新、测试与插件说明的中英双语版本。
- 中文和英文各自另起段落、左对齐，修正首页的中英混排和英文缩进。
- Windows 包、插件包和源码包同步双语文档并更新 SHA-256 校验。
- 沿用已验证的桌面程序 1.4.6 二进制，保持面向 ChatGPT Plus 用户的深度定制定位。

### 插件 1.1.3 / 桌面程序 1.4.6

- 账户读取后台与 Codex 主程序隔离，不共用配置、数据库、插件和 Code Mode host。
- 只读已有访问凭据，使用内存凭据存储，不获取或使用刷新凭据，不修改主程序登录、配置和会话文件。
- 应答服务端续期请求，修复客户端与服务端 ID 冲突；账户切换后拒绝提交旧读数。
- 接口失败后退避重试，失败连接及时关闭，成功后恢复一分钟更新。
- 新增 31 项账户隔离回归检查；真实账户读取成功，主程序三个关键文件读取前后不变。

### 插件 1.1.2 / 桌面程序 1.4.5

- 修复展开、收起时窗口抖动，移除独立的系统窗口宽高动画。
- 一次同步窗口位置和尺寸，位置计算使用目标尺寸，避免布局更新与位置校正冲突。
- 保留面板淡入、背景装甲动效、稳定收起背景及拖动复原。

### 插件 1.1.1 / 桌面程序 1.4.4

- 修复收起动画中出现展开态头部、底部两条长条的现象。
- 背景形态采用明确的展开状态，收起背景与读数固定尺寸，不随过渡中的窗口拉伸。
- 保留收起动画、向上展开、自由拖动及一键复原。

### 插件 1.1.0 / 桌面程序 1.4.3

- 发布署名 suqi456；准备本地和 Git marketplace 的目录结构。
- 去除本机路径配置。程序安装到用户目录，语言包、位置、语言选择独立保存。
- 增加安装、状态检查、启动、关闭及保留数据的卸载入口。
- 安装前检查 Windows x64、.NET 8 Desktop Runtime 和包内文件 SHA-256；拒绝覆盖不属于本工具的目录。升级写入失败时恢复原程序文件。
- 只关闭指定安装路径的进程；检测到其他位置的副本时提示先从托盘退出。
- 增加原始字体许可证、隐私说明、构建步骤和演示预览。
- 较长 token 数值使用统一的自适应字号，避免单位被省略。
- 沿用 1.4.2 已确认的橙红机甲界面；未合入暂停中的背景改版。

### 桌面程序 1.4.2

收起两片切角装甲；展开圆角卡片，统一 token 排版，修正数据动画。支持拖动、恢复顶部居中、语言包与可选开机启动。

---

## English

### Plugin 1.1.6 / Desktop 1.4.7 — 2026-10-31

- Publish with independent public history and GitHub noreply commit addresses; update installation, download, and verification links.
- Rebuild and verify the source, Windows distribution, and plugin archives while retaining bilingual guides and screenshots.

### Plugin 1.1.5 / Desktop 1.4.7 — 2026-10-31

- Verify the Codex Windows signature and OpenAI publisher before reading or sending account credentials. Prefer the official installation and reject untrusted, linked, and network paths.
- Lock the verified executable for the backend lifetime to prevent subsequent writes or replacement. No runtime signature-bypass option is provided.
- Add native signature and executable-selection tests. Account-isolation tests retain a mock backend confined to their test assembly.
- Pin GitHub Actions to full commit SHAs, disable checkout credential persistence, build from source, require security checks, and generate signed build provenance for release archives.
- Add bilingual security guidance and verification-failure messages in all three built-in languages. Windows publisher code signing remains outstanding.

### Plugin 1.1.4 / Desktop 1.4.6 — 2026-10-30

- Completed Chinese/English user, privacy, language-pack, release, publishing, test, and plugin documentation.
- Put Chinese and English in separate, left-aligned paragraphs and fixed the README's mixed or indented language layout.
- Updated the Windows, plugin, and source packages with bilingual documentation and refreshed SHA-256 manifests.
- Retained the same verified desktop 1.4.6 executable and DLL. The tool remains deeply tailored to ChatGPT Plus users' Codex workflows.

### Plugin 1.1.3 / Desktop 1.4.6

- Isolated the account-reading backend from the Codex desktop app's configuration, databases, plugins, and Code Mode host.
- Read existing access credentials into memory without obtaining or using refresh credentials or modifying the desktop app's login, configuration, or session files.
- Answered server renewal requests, fixed client/server request-ID collisions, and rejected old readings after account changes.
- Added retry backoff after interface failures, promptly closed failed connections, and restored one-minute refreshes after success.
- Added 31 account-isolation regression checks. Live-account reading succeeded, and three key desktop-app files were unchanged before and after reading.

### Plugin 1.1.2 / Desktop 1.4.5

- Fixed native-window jitter during expansion and collapse by removing separate width/height animations.
- Applied position and size together, using target dimensions for position calculations to avoid conflicts between layout updates and position correction.
- Retained panel fades, background armor effects, stable collapsed backgrounds, dragging, and position reset.

### Plugin 1.1.1 / Desktop 1.4.4

- Fixed two long expanded-header/footer strips appearing during collapse.
- Gave the background an explicit expanded state. The collapsed background and readings retain fixed dimensions rather than stretching with the transitioning window.
- Retained collapse effects, upward expansion, free dragging, and one-click reset.

### Plugin 1.1.0 / Desktop 1.4.3

- Added author attribution to suqi456 and prepared the local/Git marketplace directory structure.
- Removed machine-specific path configuration. Program files install into a user directory, while language packs, position, and language choice are stored separately.
- Added install, status, start, stop, and data-preserving uninstall entry points.
- Checked Windows x64, .NET 8 Desktop Runtime, and bundled-file SHA-256 hashes before installation. Rejected unrelated target directories and restored previous program files after failed upgrades.
- Stopped only tool processes at the specified installation path; prompted users to quit copies running elsewhere through their tray menu.
- Added original font licenses, privacy documentation, build instructions, and demo previews.
- Used a consistent adaptive font size for long token values so their units remain visible.
- Retained the approved 1.4.2 orange-red mecha interface; the paused background redesign was not merged.

### Desktop 1.4.2

Introduced two chamfered armor panels in the collapsed view and rounded cards with consistent token typography in the expanded view; corrected value animation. Supported dragging, top-center reset, language packs, and optional startup at sign-in.
