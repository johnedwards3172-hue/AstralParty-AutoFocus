# AstralFocus — 星引擎(Astral Party) 行动前置插件

轮到 **我** 操作时，自动把游戏窗口抢到前台并短暂置顶，省掉"低头看手机错过自己的回合"。

覆盖三类事件：

| 事件 | 判定依据（全部只读） |
|---|---|
| 我的回合 | `BattleLogic.CurPlayerId == 自己` |
| 怪物攻击要我防御/闪避 | `FightWindow.dodgeChoice != 0` 且 `battleFightData.defenderInfo.playerId == 自己` 且 `UIFightWindow.btn_Defend.visible` |
| 随机弹出的回合奖励卡（遗物三选一） | `UIManager._cachedRelicWindow` 正在显示（`GObject.parent != null`） |

## 核心行为：每个事件只前置一次

这一点是刻意设计的，避免"被前置后一直前置、切到别的程序又被拉回来"。

- 事件开始（激活沿）后尝试抢前台，**抢到一次就标记完成**；
- 完成后即使窗口失去前台也**绝不再抢回来** —— 用户主动切走必须被尊重；
- 只有一次都没抢到（`SetForegroundWindow` 被前台锁拒绝）才重试，最多 `MaxFocusAttempts` 次；
- 事件结束时重置，等待下一个事件（按事件身份区分，如防御请求的 `sn`）。

## 红线

- **只读**游戏状态：不写内存、不改数值、不发封包。
- **只做窗口操作**：还原 → 抢前台 → 临时置顶（`TopMostMs` 后自动撤掉）。
- 不自动输入、不自动出牌、不代打。

## 原理

### 为什么要直连原始 il2cpp API

游戏逻辑不在 `GameAssembly.dll`，而在 **HybridCLR 热更程序集 `AstralParty.Runtime.dll`** 里。
BepInEx 预生成的 interop 代理看不到热更类型，因此本插件直接 P/Invoke `GameAssembly.dll`
导出的原始 il2cpp API（`il2cpp_domain_get_assemblies` / `il2cpp_class_from_name` / 裸指针读字段），
无论类落在 AOT 还是热更程序集都能读到。

同时，插件在**后台线程**上工作（先 `il2cpp_thread_attach`）；MonoBehaviour 注入方案在
HybridCLR 上会崩。

### 两个必须的版本要求

1. **BepInEx 6 必须支持 IL2CPP metadata v31**（本游戏 Unity 2021.3.45f2）。
   官方 `6.0.0-pre.2` 只支持到 v29，**会直接失败**；需用 bleeding-edge build（已验证 `be.788`）。
2. **Il2CppInterop 需要打补丁**上使用，否则 HybridCLR 会崩。
   根因见上游 [Il2CppInterop #251](https://github.com/BepInEx/Il2CppInterop/issues/251)：
   HybridCLR 在 `MetadataCache_GetTypeInfoFromTypeDefinitionIndex` 头部插了短跳转，
   detour 迁移指令时偏移失效，调用即 `AccessViolation`。
   本项目使用 PR #251 分支重编译的 `Il2CppInterop.Runtime.dll`，
   日志会出现 `HybridCLR runtime detected - using compatibility mode` 表示生效。

### 必须用 Steam 启动

直接运行 `AstralParty_CN.exe` 会因 `[SteamManager] 非steam客户端启动` 自杀，请用：

```
"D:\Program Files (x86)\Steam\Steam.exe" -applaunch 2622000
```

## 安装

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

默认游戏目录：`D:\Program Files (x86)\Steam\steamapps\common\Astral Party\8vJXn6CN`
（`8vJXn6CN` = 国服，`8vJXnINT` = 国际服；本插件**只针对国服 CN**。）

安装会向游戏根目录添加 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx\`、`dotnet\`，
插件本体放到 `BepInEx\plugins\AstralFocus.dll`。**首次启动**要生成 IL2CPP interop 文件，比平时慢，属正常。

## 两步走（推荐）

### 第一步：探查模式（默认，不动窗口）

1. 启动游戏，打完一局（包含：自己的回合、被怪物攻击需防御、弹回合奖励卡）。
2. 打开 `BepInEx\AstralFocus.diag.log`（立即刷盘，崩溃不丢），应看到：

```
激活: 轮到我的回合 (playerId=123456)
激活: 怪物攻击，需要我防御/闪避 (sn=149004949 防守方=2994854)
激活: 轮到我选回合奖励卡(遗物三选一)
```

3. 三类事件都出现且**没有误报**后，再进行第二步。

### 第二步：开启前置

编辑 `BepInEx\config\astralfocus.local.turnnotify.cfg`，把 `LogOnly` 改成 `false`，重启游戏生效。

## 配置项

| 键 | 默认 | 说明 |
|---|---|---|
| `LogOnly` | `true` | 探查模式，只写日志不前置 |
| `PollIntervalMs` | `150` | 状态轮询间隔（毫秒） |
| `MaxFocusAttempts` | `6` | 单个事件最多尝试几次抢前台；抢到一次即停止 |
| `MinHoldSeconds` | `1` | 事件开始后多少秒内忽略判定抖动，避免同一事件被切成两段重复抢 |
| `BringToFront` | `true` | 是否抢前台焦点 |
| `TopMostMs` | `700` | 临时置顶毫秒数，`0` = 不置顶 |
| `OnlyWhileGameRunning` | `false` | 仅在游戏窗口可见时动作 |
| `ProbeEnabled` | `true` | 关闭后插件完全不碰 il2cpp（排查用） |
| `SelfTest` | `false` | 逐个测试 il2cpp API 可用性（排查用） |

分辨率无关：抢焦点与置顶走 Win32 `SetForegroundWindow` / `SetWindowPos`，
任意分辨率（含手动拉伸的非预设分辨率）都成立。

## 诊断日志

- `BepInEx\AstralFocus.diag.log` —— 插件自己的诊断日志，**每次写入立即刷盘**，游戏崩溃也不丢；首行会打印**构建指纹**（MVID + DLL 写入时间），便于确认实际加载的版本。
- `BepInEx\LogOutput.log` —— BepInEx 标准日志。

## 构建

```powershell
cd src
dotnet build -c Release
```

`AstralFocus.csproj` 里两个路径需按本机调整：

- `<BepInExDir>` —— BepInEx 6 (IL2CPP) 的 `BepInEx\core` 目录
- `<InteropDir>` —— 游戏 `BepInEx\interop` 目录

## 游戏更新后如何修

热更类型/字段改名会导致定位失败，日志出现
`等待游戏就绪… (未找到热更类型…)`，此时插件**静默失效、不动作**（不会误抢焦点）。

修复方式：用 Cpp2IL dump 新的 `AstralParty.Runtime.dll`，核对 `GameProbe.cs` 里的类名/字段名，
其余代码不用动。

## 免责声明

仅供个人学习与自用。插件只读取游戏状态并操作窗口，不修改游戏数据、不参与自动化操作。
请自行确认是否符合游戏用户协议。