# AstralFocus 安装脚本：把 BepInEx 6 (IL2CPP) + 插件部署到游戏目录
# 只添加文件，不改动游戏原有可执行文件内容。
#
# 前置条件（见 README「原理」一节）：
#   1. BepInEx 6 必须是支持 IL2CPP metadata v31 的 bleeding-edge build（已验证 be.788）；
#      官方 6.0.0-pre.2 只支持到 v29，会直接失败。
#   2. BepInEx\core\Il2CppInterop.Runtime.dll 必须替换为打过 PR #251 补丁的版本，
#      否则 HybridCLR 会在 MetadataCache_GetTypeInfoFromTypeDefinitionIndex 上 AccessViolation。
param(
    [string]$GameDir = "D:\Program Files (x86)\Steam\steamapps\common\Astral Party\8vJXn6CN",
    [string]$BepInExSource = "",
    [string]$PluginDll = ""
)

$ErrorActionPreference = "Stop"

if (-not $PluginDll) {
    $PluginDll = Join-Path $PSScriptRoot "src\bin\Release\AstralFocus.dll"
}

if (-not (Test-Path $GameDir)) { throw "找不到游戏目录: $GameDir" }
if (-not (Test-Path (Join-Path $GameDir "AstralParty_CN.exe"))) { throw "该目录不像游戏根目录（缺 AstralParty_CN.exe）: $GameDir" }
if (-not (Test-Path $PluginDll)) { throw "找不到插件程序集（请先 dotnet build -c Release）: $PluginDll" }

# BepInEx 源：可以显式传入，或直接用游戏目录里已部署好的 BepInEx
$useExisting = [string]::IsNullOrWhiteSpace($BepInExSource) -or -not (Test-Path $BepInExSource)

if ($useExisting) {
    Write-Host "== 1/3 跳过 BepInEx 部署（使用游戏目录内已有的 BepInEx）==" -ForegroundColor Yellow
    if (-not (Test-Path (Join-Path $GameDir "BepInEx"))) {
        throw "游戏目录里没有 BepInEx，请用 -BepInExSource 指定 BepInEx 6 (IL2CPP) 所在目录"
    }
} else {
    Write-Host "== 1/3 部署 BepInEx 6 ==" -ForegroundColor Cyan
    foreach ($item in @("winhttp.dll", "doorstop_config.ini", ".doorstop_version")) {
        $src = Join-Path $BepInExSource $item
        if (Test-Path $src) { Copy-Item $src $GameDir -Force }
    }
    Copy-Item (Join-Path $BepInExSource "BepInEx") $GameDir -Recurse -Force
    Copy-Item (Join-Path $BepInExSource "dotnet") $GameDir -Recurse -Force
}

Write-Host "== 2/3 部署插件程序集 ==" -ForegroundColor Cyan
$pluginsDir = Join-Path $GameDir "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
$dst = Join-Path $pluginsDir "AstralFocus.dll"
Copy-Item $PluginDll $dst -Force

$srcHash = (Get-FileHash $PluginDll -Algorithm SHA1).Hash
$dstHash = (Get-FileHash $dst -Algorithm SHA1).Hash
if ($srcHash -ne $dstHash) { throw "部署校验失败：目标 DLL 与构建产物不一致" }

Write-Host "== 3/3 完成 ==" -ForegroundColor Green
Write-Host "插件: $dst"
Write-Host "SHA1: $dstHash"
Write-Host "首次启动会生成 IL2CPP interop 文件，比平时慢，属正常。"
Write-Host ""
Write-Host "默认是【探查模式】：只写日志、不前置窗口。" -ForegroundColor Yellow
Write-Host "请登录并打一局，然后查看 BepInEx\AstralFocus.diag.log 核对触发点。"
Write-Host "确认无误后，编辑 BepInEx\config\astralfocus.local.turnnotify.cfg，把 LogOnly 改成 false 即可启用前置。"
Write-Host ""
Write-Host "启动方式（直接跑 exe 会因非 Steam 客户端自杀）：" -ForegroundColor Yellow
Write-Host '  "D:\Program Files (x86)\Steam\Steam.exe" -applaunch 2622000'