# VS 开发 / 测试 / 编译 / 发布工作流

> 本仓库采用标准 Visual Studio 布局：根目录 `MbdGitTool.sln` + `src/` + `tests/` + `ui/`。
> VS 2026 Community 18.12（已装 .NET 桌面开发工作负载；团队补装见 `.vsconfig`）。

## 1. 打开与日常开发

```powershell
# 双击根目录 MbdGitTool.sln，或：
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" "D:\Workspace\git_1\MbdGitTool.sln"
```

- 解决方案视图：`src`（Core / Host / Git）+ `tests`（Core.Tests）两个解决方案文件夹；
- 启动项目：`MbdGitTool.Host`（WPF + WebView2）；
- 首次/前端变更后先构建前端（Host 的 `ui/` 产物来自 `ui/dist`）：

```powershell
npm --prefix ui install
npm --prefix ui run build
```

## 2. 编译（VS IDE 命令行 / MSBuild）

```powershell
$vs = "C:\Program Files\Microsoft Visual Studio\18\Community"

# A) 走 VS IDE（devenv.com），等价于在 VS 里 Ctrl+Shift+B
& "$vs\Common7\IDE\devenv.com" "D:\Workspace\git_1\MbdGitTool.sln" /Build "Debug|Any CPU" /Out "$env:TEMP\mbd-devenv.log"

# B) 直接 MSBuild（CI 推荐）
& "$vs\MSBuild\Current\Bin\MSBuild.exe" "D:\Workspace\git_1\MbdGitTool.sln" `
    /restore /m /nologo /v:minimal /p:Configuration=Debug /p:Platform="Any CPU"
```

> 已配置：`Directory.Build.props`（Nullable / analyzers / TreatWarningsAsErrors / win-x64 RID）、
> `Directory.Packages.props`（中央包版本）、`.editorconfig`、`global.json`（.NET 10.0）。

## 3. 测试

- VS：测试资源管理器（Test Explorer）→ 运行 `MbdGitTool.Core.Tests`；
- 命令行（VS 自带 vstest）：

```powershell
& "$vs\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" `
  "D:\Workspace\git_1\tests\MbdGitTool.Core.Tests\bin\Debug\net10.0\MbdGitTool.Core.Tests.dll" /Logger:console
```

## 4. 发布（Release / win-x64 / framework-dependent）

```powershell
$pub = "D:\Workspace\git_1\artifacts\publish"
& "$vs\MSBuild\Current\Bin\MSBuild.exe" "D:\Workspace\git_1\src\MbdGitTool.Host\MbdGitTool.Host.csproj" `
  /restore /t:Publish /m /nologo /v:minimal `
  /p:Configuration=Release /p:Platform=AnyCPU /p:RuntimeIdentifier=win-x64 /p:SelfContained=false `
  "/p:PublishDir=$pub\MbdGitTool.Host\"

& "$vs\MSBuild\Current\Bin\MSBuild.exe" "D:\Workspace\git_1\src\MbdGitTool.Git\MbdGitTool.Git.csproj" `
  /restore /t:Publish /m /nologo /v:minimal `
  /p:Configuration=Release /p:Platform=AnyCPU /p:RuntimeIdentifier=win-x64 /p:SelfContained=false `
  "/p:PublishDir=$pub\MbdGitTool.Git\"
```

打包（当前阶段）：

```powershell
Compress-Archive -Path "D:\Workspace\git_1\artifacts\publish\*" `
  -DestinationPath "D:\Workspace\git_1\artifacts\MbdGitTool-0.1.0-m0-win-x64.zip" -Force
```

> M3 再切 MSIX（Windows SDK `makeappx`/`signtool` 已就绪；或安装 VS “MSIX Packaging Tools”）。
> 目标机若无 .NET 10 Desktop Runtime，发布时改 `/p:SelfContained=true`。

## 5. 运行

```powershell
# 开发：从源码运行（需先 npm run build 生成 ui/dist）
dotnet run --project src\MbdGitTool.Host

# 发布产物
Start-Process artifacts\publish\MbdGitTool.Host\MbdGitTool.Host.exe
```

Host 启动日志：`%LOCALAPPDATA%\MbdGitTool\logs\host-m0.log`。

## 6. 本机验证记录（2026-11-14）

| 步骤 | 命令/工具 | 结果 |
|---|---|---|
| VS IDE 编译 | `devenv.com MbdGitTool.sln /Build "Debug|Any CPU"` | **4 成功 / 0 失败**（6.9s） |
| MSBuild 编译 | VS `MSBuild.exe /restore` | **0 warning / 0 error** |
| 测试 | VS `vstest.console.exe` | **10/10 通过**（0.84s） |
| 发布 | VS `MSBuild /t:Publish`（Host + Git） | exit 0，`artifacts/publish/` |
| 发布产物运行 | 隐藏启动 8s + 日志 | `webview2 initialized: 157.0.4341.46` → `navigation completed: success=True` |
| 压缩包 | `Compress-Archive` | `artifacts/MbdGitTool-0.1.0-m0-win-x64.zip`（约 0.7MB） |

## 7. 备注

- `artifacts/`、`src|tests/**/bin|obj`、`ui/node_modules`、`ui/dist` 已加入 `.gitignore`。
- `ui/dist` 不入库：CI/本机构建 Host 前必须先构建前端（或后续加 MSBuild target 自动执行 npm）。
- 前端 dev server（`npm run dev`）当前未被 Host 加载；M1 再做 dev/prod 双模式（`APP_UI_URL`）。
