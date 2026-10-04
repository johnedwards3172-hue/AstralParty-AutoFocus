# AstralParty-AutoFocus

**星引擎 Astral Party 行动前置插件** —— 轮到你操作时，自动把游戏窗口抢到前台，不再因为切出去看视频/聊天而错过自己的回合。

覆盖三类事件：

| 事件 | 判定依据（全部只读） |
|---|---|
| 我的回合 | `BattleLogic.CurPlayerId == 自己` |
| 怪物攻击要我防御 / 闪避 | `FightWindow.dodgeChoice != 0` 且 `battleFightData.defenderInfo.playerId == 自己` 且 `UIFightWindow.btn_Defend.visible` |
| 随机弹出的回合奖励卡（遗物三选一） | `UIManager._cachedRelicWindow` 正在显示 |

> 适用于 **国服 CN**（游戏内 `8vJXn6CN`）。国际服 `8vJXnINT` 未测试。

---

## 一、核心行为：每个事件只前置一次

这是刻意设计的，为了不打扰你：

- 事件开始后尝试抢前台，**抢到一次就结束**；
- 结束后即使窗口失去前台，也**绝不再抢回来** —— 你主动切走会被尊重；
- 只有一次都没抢到（被 Windows 前台锁拒绝）才重试，最多 `MaxFocusAttempts` 次；
- 事件结束时重置，按事件身份区分（例如防御请求的 `sn`），不会漏掉下一个事件。

---

## 二、安装前必读：两个硬性前提

这个游戏用 **HybridCLR 热更**，Unity 版本是 2021.3.45f2，因此有两个坑必须绕开，否则插件要么加载失败、要么直接把游戏带崩。

### 前提 1：BepInEx 6 必须支持 IL2CPP metadata v31

本游戏的 IL2CPP metadata 版本是 **v31**。BepInEx 官方稳定版 `6.0.0-pre.2` **只支持到 v29**，装上会直接失败。

请下载 **bleeding-edge** 版本（本项目实测 `be.788`）：

> https://builds.bepinex.dev/projects/bepinex_be

选 `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+*.zip` 之类的包。

### 前提 2：Il2CppInterop 必须打补丁（不打会崩游戏）

BepInEx 自带的 `Il2CppInterop.Runtime.dll` 在 HybridCLR 上会 **AccessViolation 崩溃**。

原因见上游 issue [Il2CppInterop #251](https://github.com/BepInEx/Il2CppInterop/issues/251)：
HybridCLR 在 `MetadataCache_GetTypeInfoFromTypeDefinitionIndex` 函数头插入了短跳转，
Il2CppInterop 的 detour 在迁移指令时偏移失效，调用即崩溃。

**解决办法**：用 [PR #251](https://github.com/BepInEx/Il2CppInterop/pull/251) 分支重新编译，替换掉原版。

```bash
git clone https://github.com/BepInEx/Il2CppInterop
cd Il2CppInterop
git fetch origin pull/251/head:pr-251
git checkout pr-251
dotnet build -c Release Il2CppInterop.Runtime/Il2CppInterop.Runtime.csproj
```

编译产物在 `bin/Il2CppInterop.Runtime/net6.0/Il2CppInterop.Runtime.dll`（约 302 KB），
用它覆盖游戏目录里的 `BepInEx\core\Il2CppInterop.Runtime.dll`（**先备份原文件**）。

生效标志：`BepInEx\LogOutput.log` 里会出现
`HybridCLR runtime detected - using compatibility mode`。

### 额外提醒：必须用 Steam 启动

直接运行 `AstralParty_CN.exe` 会因 `[SteamManager] 非steam客户端启动` 自动退出。请用：

```powershell
& "D:\Program Files (x86)\Steam\Steam.exe" -applaunch 2622000
```

---

## 三、安装

1. 按上面两个前提处理好 BepInEx 6 与 Il2CppInterop。
2. 编译插件（或到 Releases 页下载编译好的 `AstralPartyAutoFocus.dll`）：

```powershell
cd src
dotnet build -c Release
```

如果 BepInEx 不在默认位置，用参数指定：

```powershell
dotnet build -c Release `
  -p:BepInExDir="<你的BepInEx>\core" `
  -p:InteropDir="<你的游戏目录>\BepInEx\interop"
```

3. 部署：

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -GameDir "你的游戏目录"
```

或手动把 `AstralPartyAutoFocus.dll` 复制到 `BepInEx\plugins\`。

**首次启动**会生成 IL2CPP interop 文件，比平时慢很多，属正常。

---

## 四、两步走（强烈建议）

### 第一步：探查模式（默认，不动窗口）

插件默认 `LogOnly = true`，只写日志、不抢窗口。先跑一局确认触发点正确：

1. 启动游戏，打一局（要包含：自己的回合、被怪物攻击需防御、弹回合奖励卡）。
2. 打开 `BepInEx\AstralFocus.diag.log`，应看到：

```
激活: 轮到我的回合 (playerId=123456)
激活: 怪物攻击，需要我防御/闪避 (sn=149004949 防守方=2994854)
激活: 轮到我选回合奖励卡(遗物三选一)
```

3. 三类都出现、且**没有误报**，再进行第二步。

### 第二步：开启前置

编辑 `BepInEx\config\astralfocus.local.turnnotify.cfg`：

```ini
LogOnly = false
```

重启游戏生效。

---

## 五、配置项

| 键 | 默认 | 说明 |
|---|---|---|
| `LogOnly` | `true` | 探查模式，只写日志不前置 |
| `PollIntervalMs` | `150` | 状态轮询间隔（毫秒） |
| `MaxFocusAttempts` | `6` | 单个事件最多尝试几次抢前台；抢到一次即停止 |
| `MinHoldSeconds` | `1` | 事件开始后多少秒内忽略判定抖动，防止同一事件被切成两段重复抢 |
| `BringToFront` | `true` | 是否抢前台焦点 |
| `TopMostMs` | `700` | 临时置顶毫秒数，`0` = 不置顶 |
| `OnlyWhileGameRunning` | `false` | 仅在游戏窗口可见时动作 |
| `ProbeEnabled` | `true` | 关掉后插件完全不碰 il2cpp（排查用） |
| `SelfTest` | `false` | 逐个测试 il2cpp API 可用性（排查用） |

**分辨率无关**：抢焦点与置顶走 Win32 `SetForegroundWindow` / `SetWindowPos`，
任何分辨率都成立（含手动拉伸的非预设分辨率）。

---

## 六、故障排查

| 现象 | 原因 / 处理 |
|---|---|
| 游戏启动直接闪退 | Il2CppInterop 没打补丁（前提 2）；或 BepInEx 版本不支持 v31（前提 1） |
| 日志出现「等待游戏就绪… 未找到热更类型」 | 游戏更新改了热更类名/字段名，见下一节 |
| 日志出现 `非steam客户端启动` | 没用 Steam 启动 |
| 完全不抢窗口 | 检查配置里 `LogOnly` 是否还是 `true` |
| 切走之后又被拉回来 | 不应该发生。若出现请提 issue，附上 `AstralFocus.diag.log` |

诊断日志：`BepInEx\AstralFocus.diag.log`（**每次写入立即刷盘**，游戏崩溃也不丢）。
首行会打印**构建指纹**（MVID + DLL 写入时间），用来确认到底加载的是哪个版本。

---

## 七、原理

### 为什么直连原始 il2cpp API

游戏逻辑不在 `GameAssembly.dll`，而在 HybridCLR 热更程序集 `AstralParty.Runtime.dll` 里。
BepInEx 预生成的 interop 代理**看不到热更类型**，所以本插件 P/Invoke 直连
`GameAssembly.dll` 导出的原始 il2cpp API（`il2cpp_domain_get_assemblies` /
`il2cpp_class_from_name` / 裸指针读字段），无论类在 AOT 还是热更程序集都能读到。

插件在**后台线程**上工作（先 `il2cpp_thread_attach`）；MonoBehaviour 注入方案在
HybridCLR 上会崩。

### 代码结构

```
src/
  Plugin.cs       BepInEx 入口
  Config.cs       配置项定义
  Poller.cs       后台轮询线程 + 事件状态机（只前置一次的核心逻辑）
  GameProbe.cs    只读读取游戏状态（单例 / 字段链 / 窗口可见性）
  Il2Cpp.cs       极薄 il2cpp 反射层
  Native.cs       直接 P/Invoke 原始 il2cpp 导出
  Win32.cs        窗口前置 / 置顶（含消息队列修复与多轮重试）
  Diagnostics.cs  立即刷盘的诊断日志
  SelfTest.cs     il2cpp API 可用性自检
```

---

## 八、游戏更新后怎么修

热更类型或字段改名会导致定位失败。日志会出现
`等待游戏就绪… (未找到热更类型…)`，此时插件**静默失效、不动作**（不会误抢焦点）。

修复：用 [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) dump 新的
`AstralParty.Runtime.dll`，核对 `GameProbe.cs` 里的类名/字段名即可，其余代码不用动。

---

## 九、红线与免责

- **只读**游戏状态：不写内存、不改数值、不发封包。
- **只做窗口操作**：还原 → 抢前台 → 临时置顶（到期自动撤销）。
- 不自动输入、不自动出牌、不代打。
- 仅供个人学习与自用。请自行确认是否符合游戏用户协议，使用风险自负。

## 许可证

[MIT](LICENSE)