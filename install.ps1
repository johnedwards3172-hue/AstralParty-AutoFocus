# AstralParty AutoFocus 安装脚本：把插件部署到游戏目录
# 只添加文件，不改动游戏原有可执行文件内容。
#
# 前置条件（详见 README「二、安装前必读」）：
#   1. BepInEx 6 必须是支持 IL2CPP metadata v31 的 bleeding-edge build（已验证 be.788）；
#      官方 6.0.0-pre.2 只支持到 v29，会直接失败。
#   2. BepInEx\core\Il2CppInterop.Runtime.dll 必须替换为打过 PR #251 补丁的版本，
#      否则 HybridCLR 会在 MetadataCache_GetTypeInfoFromTypeDefinitionIndex 上 AccessViolation。
param(
    # 游戏根目录。留空则自动探测 Steam 常见安装位置。
    [string]$GameDir = "",
    [string]$BepInExSource = "",
    [string]$PluginDll = ""
)

$ErrorActionPreference = "Stop"

if (-not $PluginDll) {
    $PluginDll = Join-Path $PSScriptRoot "src\bin\Release\AstralPartyAutoFocus.dll"
}

if (-not $GameDir) {
    $candidates = @()
    # 从 Steam 安装位置推断
    $steamReg = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam"
    )
    foreach ($k in $steamReg) {
        try {
            $p = (Get-ItemProperty -Path $k -ErrorAction Stop).SteamPath
            if ($p) { $candidates += (Join-Path $p "steamapps\common\Astral Party") }
        } catch { }
    }
    $candidates += "C:\Program Files (x86)\Steam\steamapps\common\Astral Party"
    $candidates += "D:\Program Files (x86)\Steam\steamapps\common\Astral Party"
    $candidates += "D:\Steam\steamapps\common\Astral Party"
    $candidates += "E:\Steam\steamapps\common\Astral Party"

    foreach ($c in $candidates) {
        if (-not (Test-Path $c)) { continue }
        # 国服和国际服分别在不同子目录
        foreach ($sub in @("8vJXn6CN", "8vJXnINT")) {
            $full = Join-Path $c $sub
            if (Test-Path (Join-Path $full "AstralParty_CN.exe")) { $GameDir = $full; break }
            if (Test-Path (Join-Path $full "AstralParty.exe")) { $GameDir = $full; break }
        }
        if ($GameDir) { break }
    }
    if ($GameDir) { Write-Host "自动探测到游戏目录: $GameDir" -ForegroundColor Cyan }
}

if (-not $GameDir) { throw "未指定游戏目录，且自动探测失败。请用 -GameDir 指定，例如：-GameDir `"D:\Steam\steamapps\common\Astral Party\8vJXn6CN`"" }
if (-not (Test-Path $GameDir)) { throw "找不到游戏目录: $GameDir" }
if (-not (Test-Path (Join-Path $GameDir "AstralParty_CN.exe")) -and -not (Test-Path (Join-Path $GameDir "AstralParty.exe"))) { throw "该目录不像游戏根目录（缺 AstralParty_CN.exe / AstralParty.exe）: $GameDir" }
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
$dst = Join-Path $pluginsDir "AstralPartyAutoFocus.dll"
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
Write-Host '  & "<你的Steam目录>\Steam.exe" -applaunch 2622000'