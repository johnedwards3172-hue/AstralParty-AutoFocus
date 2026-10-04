# 构建打过 PR #251 补丁的 Il2CppInterop.Runtime.dll
#
# 为什么需要：本游戏用 HybridCLR，BepInEx 自带的 Il2CppInterop 会在
# MetadataCache_GetTypeInfoFromTypeDefinitionIndex 上 AccessViolation 崩溃。
# 详见 https://github.com/BepInEx/Il2CppInterop/issues/251
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File tools\build-il2cppinterop.ps1
#   powershell -ExecutionPolicy Bypass -File tools\build-il2cppinterop.ps1 -GameDir "D:\...\8vJXn6CN"
#
# 加 -Deploy 可（先备份原文件再）直接替换到游戏 BepInEx\core\ 下。
param(
    [string]$WorkDir = "$env:TEMP\Il2CppInterop-pr251",
    [string]$GameDir = "",
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

function Need($exe) {
    if (-not (Get-Command $exe -ErrorAction SilentlyContinue)) {
        throw "找不到 $exe，请先安装（$exe 属于 .NET SDK / Git）。"
    }
}

Write-Host "== 0/5 检查依赖 ==" -ForegroundColor Cyan
Need git
Need dotnet
dotnet --version | ForEach-Object { Write-Host "  dotnet $_" }

Write-Host "== 1/5 克隆上游仓库 ==" -ForegroundColor Cyan
if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
git clone --depth 1 https://github.com/BepInEx/Il2CppInterop $WorkDir
if ($LASTEXITCODE -ne 0) { throw "克隆失败" }

Push-Location $WorkDir
try {
    Write-Host "== 2/5 拉取 PR #251 ==" -ForegroundColor Cyan
    git fetch --depth 1 origin pull/251/head:pr-251
    if ($LASTEXITCODE -ne 0) { throw "拉取 PR #251 失败（可能已被合并，见 README）" }
    git checkout pr-251
    if ($LASTEXITCODE -ne 0) { throw "切换分支失败" }

    Write-Host "== 3/5 编译 Il2CppInterop.Runtime ==" -ForegroundColor Cyan
    dotnet build -c Release Il2CppInterop.Runtime/Il2CppInterop.Runtime.csproj
    if ($LASTEXITCODE -ne 0) { throw "编译失败" }

    Write-Host "== 4/5 定位产物 ==" -ForegroundColor Cyan
    $built = Get-ChildItem -Recurse -Filter "Il2CppInterop.Runtime.dll" -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -match 'bin\\Release\\net6\.0\\Il2CppInterop\.Runtime\.dll$' } |
             Select-Object -First 1
    if (-not $built) {
        $built = Get-ChildItem -Recurse -Filter "Il2CppInterop.Runtime.dll" -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.Length -gt 200KB } |
                 Select-Object -First 1
    }
    if (-not $built) { throw "没找到编译产物 Il2CppInterop.Runtime.dll" }
    Write-Host "  产物: $($built.FullName) ($([math]::Round($built.Length/1KB)) KB)"

    Write-Host "== 5/5 结果 ==" -ForegroundColor Green
    Write-Host "  把这个文件覆盖到游戏目录的 BepInEx\core\Il2CppInterop.Runtime.dll："
    Write-Host "    $($built.FullName)"
    Write-Host "  （覆盖前务必备份原文件）"

    if ($Deploy) {
        if (-not $GameDir) { throw "-Deploy 需要同时指定 -GameDir" }
        $target = Join-Path $GameDir "BepInEx\core\Il2CppInterop.Runtime.dll"
        if (-not (Test-Path $target)) { throw "目标不存在: $target" }
        $backup = "$target.orig"
        if (-not (Test-Path $backup)) {
            Copy-Item $target $backup
            Write-Host "  已备份原文件 -> $backup" -ForegroundColor Yellow
        }
        Copy-Item $built.FullName $target -Force
        Write-Host "  已替换: $target" -ForegroundColor Green
        Write-Host "  验证：启动游戏后 LogOutput.log 应出现 'HybridCLR runtime detected - using compatibility mode'"
    }
}
finally {
    Pop-Location
}