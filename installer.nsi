; MBD 组件全生命周期 Git 工具 v1.0.0 NSIS 安装脚本
; 用法（仓库根目录）：
;   makensis installer.nsi
; 前置：artifacts\release\win-x64 已由 dotnet publish 生成

Unicode true
!include "MUI2.nsh"
!include "x64.nsh"

!define APP_NAME      "MBD 组件全生命周期 Git 工具"
!define APP_SHORT     "MBD Git Tool"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "MBD Tooling"
!define APP_EXE       "MbdGitTool.Host.exe"
!define APP_ID        "MbdGitTool"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "MbdGitTool-Setup-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\MBD Git Tool"
InstallDirRegKey HKLM "Software\${APP_ID}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP_NAME} v${APP_VERSION}"

VIProductVersion "1.0.0.0"
VIAddVersionKey /LANG=2052 "ProductName"     "${APP_NAME}"
VIAddVersionKey /LANG=2052 "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion"     "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright"  "Copyright (C) 2026 ${APP_PUBLISHER}"

!define MUI_ICON "docs\logo\app.ico"
!define MUI_UNICON "docs\logo\app.ico"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "运行 ${APP_NAME}"
!define MUI_WELCOMEPAGE_TITLE "安装 ${APP_NAME} ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT "本向导将安装 ${APP_NAME} ${APP_VERSION}。$\r$\n$\r$\n系统要求：Windows 10/11 x64；需要 Microsoft Edge WebView2 Runtime。$\r$\n$\r$\n安装包为自包含 .NET 10 运行时，无需另装 .NET。"
!define MUI_DIRECTORYPAGE_TEXT_TOP "请选择安装目录。默认安装到 Program Files，需要管理员权限。"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "本程序仅支持 64 位 Windows。"
    Abort
  ${EndIf}

  ; WebView2 Runtime 检测（不强制阻止安装，给出明确提示）
  ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F1E7BB9A-1A9E-4B7A-9D0A-1F3D2A6B7C11}" "pv"
  ReadRegStr $1 HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F1E7BB9A-1A9E-4B7A-9D0A-1F3D2A6B7C11}" "pv"
  StrCmp $0 "" 0 webview_ok
  StrCmp $1 "" 0 webview_ok
  MessageBox MB_ICONEXCLAMATION|MB_OK "未检测到 Microsoft Edge WebView2 Runtime。$\r$\n$\r$\n程序可安装，但首次启动前请安装 WebView2 Evergreen Runtime，否则界面无法显示。$\r$\n下载：https://developer.microsoft.com/microsoft-edge/webview2/"
webview_ok:
FunctionEnd

Section "主程序" SecMain
  SetOutPath "$INSTDIR"
  ; 发布目录整体复制（含 ui\dist、runtimes、WebView2Loader.dll 等）
  File /r "artifacts\release\win-x64\*.*"

  ; 开始菜单
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\卸载 ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
  ; 桌面快捷方式
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; 卸载信息（应用和功能）
  WriteRegStr HKLM "Software\${APP_ID}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"

  ; 只删除安装目录中的程序文件；用户数据在 %LOCALAPPDATA%\MbdGitTool，不随卸载删除
  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
  DeleteRegKey HKLM "Software\${APP_ID}"
SectionEnd
