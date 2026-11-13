# 开发环境记录（M0 已开工，2026-11-14）

| 项 | 内容 |
|---|---|
| 机器 | Windows 11 专业版 build 26300 x64 / Ryzen 5 5600G / 27.9GB RAM |
| 目的 | 按 SRS v0.2.1 NFR-COMPAT 核对 M0 开发工具链；记录已安装/已验证状态 |
| 结论 | **已就绪，无需补装**：.NET 10 SDK **10.0.404** 已安装并由 `global.json` 固定；VS 2026 编译/测试/发布链路已实际跑通 |

## 1. 体检结果

| 要求（SRS） | 实测 | 结论 |
|---|---|---|
| Windows 11 x64（NFR-COMPAT-01） | Windows 11 Pro build 26300 x64 | ✅ |
| .NET 10 LTS（NFR-COMPAT-02） | **SDK 10.0.404 已安装**（另有 11.0.100）；运行时 `WindowsDesktop.App 10.0.15`；仓库根 `global.json` 固定 `10.0.100 / rollForward=latestFeature` → 仓库内 `dotnet --version` = 10.0.404 | ✅ |
| VS 2026 + 桌面工作负载 | Visual Studio Community 2026 **18.12** + `.NET 桌面开发` 工作负载；`devenv.com` / MSBuild / vstest 均可用 | ✅ |
| WPF + WebView2 | WebView2 Runtime **157.0.4341.46**；NuGet `Microsoft.Web.WebView2 1.0.4314.35` 可还原；`net10.0-windows` + WPF 编译通过 | ✅ |
| Git ≥ 2.40 + LFS + GCM | git **2.56.1.windows.1**、git-lfs **3.8.1**、GCM 随 Git 安装 | ✅ |
| 长路径（NFR-COMPAT-03） | `LongPathsEnabled=1`；Host `app.manifest` 已声明 `longPathAware=true` | ✅ |
| 前端工具链 | Node **24.19.0**、npm **11.17.0**、corepack 0.35.0；pnpm 未装（npm 够用） | ✅ |
| Windows SDK / 打包 | Win11 SDK **10.0.26100.0**；`makeappx.exe`/`signtool.exe`/`makepri.exe`（x64）存在；VS 未装 “MSIX Packaging Tools” | ✅ 脚本化打包可用；M3 前再决定是否装 VS 组件 |
| 磁盘 | C: 57.6GB 空 / D: 2.2TB 空 | ✅ |
| NuGet 源 | `api.nuget.org` 可访问；另有 VS 离线源 | ✅ |
| 开发者模式 | 关闭 | ⚠️ 仅 MSIX 松散部署需要；可选 |

## 2. 构建探针（历史记录）

首次体检时在临时目录验证过：`dotnet new wpf -f net10.0` + `Microsoft.Web.WebView2` + `dotnet build` → 成功。
此后已安装 .NET 10 SDK 并在真实仓库完成编译/测试/发布，探针目录已清理。

## 3. 安装与验证记录

### .NET 10 SDK（已完成）

```powershell
dotnet --list-sdks
# 10.0.404 [C:\Program Files\dotnet\sdk]
# 11.0.100 [C:\Program Files\dotnet\sdk]

# 仓库根 global.json：
# { "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
dotnet --version   # 在仓库根执行 → 10.0.404
```

### VS 工具链（已验证）

| 操作 | 命令/工具 | 结果 |
|---|---|---|
| VS IDE 编译 | `devenv.com MbdGitTool.sln /Build "Debug|Any CPU"` | 4 成功 / 0 失败 |
| MSBuild 编译 | VS `MSBuild.exe /restore` | 0 warning / 0 error |
| 测试 | VS `vstest.console.exe` | 10/10 通过 |
| 发布 | VS `MSBuild /t:Publish`（Host + Git，Release/win-x64） | exit 0，产物在 `artifacts/publish/` |
| 发布产物运行 | 隐藏启动 + 日志 | WebView2 初始化成功，`navigation completed: success=True` |

完整命令见 [vs-workflow.md](vs-workflow.md)。

## 4. 当前 M0 基线（已落地）

```powershell
# 仓库根 D:\Workspace\git_1
# 1) 前端产物（Host 从 ui/dist 加载）
npm --prefix ui install
npm --prefix ui run build

# 2) 编译 / 测试（dotnet CLI 或 VS MSBuild）
dotnet build MbdGitTool.sln
dotnet test  MbdGitTool.sln

# 3) 运行
dotnet run --project src\MbdGitTool.Host

# 4) VS 打开
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" "D:\Workspace\git_1\MbdGitTool.sln"
```

工程文件：根 `MbdGitTool.sln`、`global.json`、`Directory.Build.props`、`Directory.Packages.props`（中央包版本）、`.editorconfig`、`.vsconfig`；项目在 `src/`、`tests/`、`ui/`。

## 5. 可选补装（不阻塞 M0/M1）

- **MSIX Packaging Tools**（VS Installer → 单个组件）：若 M3 使用 VS “Windows Application Packaging Project”；否则直接用 Windows SDK 的 `makeappx`/`signtool` 脚本。
- **开发者模式**（设置 → 系统 → 开发者选项）：仅 MSIX 松散部署/侧载方便，可不开。
- **pnpm**：`corepack enable pnpm`，仅当前端切 pnpm workspace 时需要。
- **不需要**：`.NET MAUI` / `UWP` / `WinAppSDK` / C++ 工作负载（本项目是 WPF + WebView2）。

## 6. 复核命令（环境变化后重跑）

```powershell
dotnet --list-sdks; dotnet --list-runtimes; dotnet --version
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationVersion
git --version; git lfs version
node -v; npm -v
Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' -Name pv
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name LongPathsEnabled
```
