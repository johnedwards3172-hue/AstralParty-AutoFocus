using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Il2CppInterop.Runtime;

namespace AstralParty.AutoFocus
{
    /// <summary>
    /// 后台线程轮询游戏状态。全部走原始 il2cpp API（不经过 Il2CppInterop 的钩子），
    /// 因为本游戏用 HybridCLR 热更，Il2CppInterop 的类注入/class_from_name 钩子会越界崩溃。
    /// 只读状态 + 只做窗口操作，不写游戏内存、不自动输入。
    /// </summary>
    internal static class Poller
    {
        private static Thread _thread;
        private static volatile bool _running;

        private static GameProbe _probe;
        private static IntPtr _hWnd;
        private static int _gamePid;
        private static int _probeFail;
        private static long _tickCount;
        private static float _unTopAt;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static double _lastHeartbeat;
        private static string _lastShownWindows;
        private static long _lastDefendKey;

        /// <summary>
        /// 每个触发点各自的事件状态。核心语义：**每个事件只前置一次**。
        /// 抢到一次就标记 Satisfied，之后即使窗口失去前台也不再抢回来，
        /// 保证用户主动切到别的程序时不会被反复拉回。
        /// </summary>
        private sealed class Trigger
        {
            public readonly string Name;
            public bool WasActive;       // 事件是否正在进行
            public bool Satisfied;       // 本次事件是否已完成前置（或本来就在前台）
            public int Attempts;         // 本次事件已尝试抢前台的次数
            public double ActivatedAt;   // 事件开始时刻，用于抑制假性结束
            public long Key;             // 事件身份（回合=playerId，防御=sn）；变化即视为新事件
            public bool SkipLogged;      // 本次事件是否已记录过跳过原因（防止日志刷屏）
            public readonly Stopwatch AttemptGate = Stopwatch.StartNew();   // 失败重试限流

            public Trigger(string name) { Name = name; }
        }

        private static readonly Trigger TurnTrigger = new Trigger("轮到我的回合");
        private static readonly Trigger DefendTrigger = new Trigger("怪物攻击，需要我防御/闪避");
        private static readonly Trigger CardTrigger = new Trigger("轮到我选回合奖励卡(遗物三选一)");

        public static void Start()
        {
            if (_running) return;
            _running = true;
            RegisterExitSafetyNet();
            _thread = new Thread(Loop) { IsBackground = true, Name = "AstralPartyAutoFocus" };
            _thread.Start();
        }

        private static void Loop()
        {
            try { IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get()); }
            catch (Exception e) { Diagnostics.Log("thread_attach 失败: " + e.Message); }

            // 抢前台需要在有消息队列的线程上调用 AttachThreadInput / SetFocus
            try { Win32.EnsureMessageQueue(); } catch { }

            Diagnostics.Log("轮询线程启动");

            while (_running)
            {
                try { Tick(); }
                catch (Exception e) { Diagnostics.Log("Tick 异常: " + e); }
                Thread.Sleep(Math.Max(50, Config.PollIntervalMs.Value));
            }
        }

        private static void Tick()
        {
            _tickCount++;
            if (_tickCount == 1) Diagnostics.Log("第 1 次 Tick");

            ResolveWindow();
            LateTick();

            if (!Config.ProbeEnabled.Value) return;

            if (_probe == null)
            {
                _probe = new GameProbe();
                _probe.Initialize();
                if (!_probe.Ready)
                {
                    _probeFail++;
                    if (_probeFail <= 3)
                    {
                        Diagnostics.Log("探针未就绪: " + _probe.LastError);
                        Plugin.LogSource?.LogInfo("[AstralPartyAutoFocus] 等待游戏就绪… (" + _probe.LastError + ")");
                    }
                    _probe = null;
                    return;
                }
                Diagnostics.Log("探针就绪");
                Plugin.LogSource?.LogInfo("[AstralPartyAutoFocus] 已定位游戏类型，开始监控。");
            }

            long self;
            try { self = _probe.SelfPlayerId(); }
            catch (Exception e) { Diagnostics.Log("SelfPlayerId 异常: " + e.Message); return; }

            if (_tickCount <= 40 && _tickCount % 10 == 0) Diagnostics.Log("self=" + self);
            if (self == 0) return;

            long cur = 0;
            try { cur = _probe.CurrentPlayerId(); } catch (Exception e) { Diagnostics.Log("CurrentPlayerId 异常: " + e.Message); }
            int curFightType = -1;
            try { curFightType = _probe.FightType(); } catch { }

            // ---- 1) 轮到我的回合 ----
            Handle(TurnTrigger, cur != 0 && cur == self, cur,
                   "轮到我的回合 (playerId=" + cur + ")");

            // ---- 2) 怪物攻击，需要我防御/闪避 ----
            // 每 tick 都判定，不能只在 sn 变化的那一次判定：
            //   * battleFightData.defenderInfo 与 dodgeChoice 之间存在竞态；
            //   * 抢前台可能失败（前台锁），一次性判定会永久错过整个防御窗口。
            // 主判定用权威信号 UIFightWindow.btn_Defend.visible（无竞态），
            // 字段缺失时退回 defenderInfo 比对。
            try
            {
                long sn = _probe.DodgeSn();
                long defender = 0;
                bool btnDefend = false;
                bool authoritative = _probe.DefendSignalAvailable;
                if (sn != 0) defender = _probe.DefenderId();
                if (authoritative) btnDefend = _probe.BtnDefendVisible();

                // 取两个信号的【交集】，而不是任一单独信号：
                //   * btn_Defend.visible：FairyGUI 的 _visible 默认就是 true，
                //     在本进程第一次防御之前会一直为 true（实测 sn=0 时误判为「我」）；
                //   * battleFightData.defenderInfo：随 BattleS2C 更新，与 dodgeChoice
                //     存在竞态，可能短暂停留在上一场战斗的防守方。
                // 两者单独都不可靠，但真正轮到我防御时必然同时成立（实测一致），
                // 取交集既不误报别人的防御，也不漏掉我的。
                bool defenderIsMe = sn != 0 && defender != 0 && defender == self;
                bool isMine = defenderIsMe && (!authoritative || btnDefend);

                long key = sn * 31 + defender;
                if (key != _lastDefendKey)
                {
                    _lastDefendKey = key;
                    if (sn != 0)
                        Diagnostics.Log("[防御] sn=" + sn + " 按钮可见=" + btnDefend +
                                        " defender=" + defender + " self=" + self +
                                        " fightType=" + curFightType +
                                        " 权威=" + authoritative + " 判为我=" + isMine);
                }

                Handle(DefendTrigger, isMine, sn,
                       "怪物攻击，需要我防御/闪避 (sn=" + sn + " 防守方=" + defender + ")");
            }
            catch (Exception e) { Diagnostics.Log("防御判定异常: " + e.Message); }

            // ---- 3) 轮到我选回合奖励卡 ----
            bool card = false;
            try { card = _probe.IsChoosingRoundCardNow(); }
            catch (Exception e) { Diagnostics.Log("IsChoosingRoundCardNow 异常: " + e.Message); }

            Handle(CardTrigger, card, 1, "轮到我选回合奖励卡(遗物三选一)");

            // 窗口变化即记录（不等心跳），避免漏掉瞬时窗口
            try
            {
                var shown = _probe.ShownWindowFields();
                if (shown != _lastShownWindows)
                {
                    _lastShownWindows = shown;
                    Diagnostics.Log("窗口变化: " + shown);
                }
            }
            catch (Exception e) { Diagnostics.Log("窗口扫描异常: " + e.Message); }

            // 心跳：每 15 秒记录一次，用于确认监控存活 + 观察状态
            if (Clock.Elapsed.TotalSeconds - _lastHeartbeat >= 15)
            {
                _lastHeartbeat = Clock.Elapsed.TotalSeconds;
                Diagnostics.Log("心跳 self=" + self + " cur=" + cur + " fightType=" + curFightType +
                    " dodgeSn=" + _probe.DodgeSn() + " defender=" + _probe.DefenderId());
            }
        }

        /// <summary>
        /// 电平触发统一入口，语义：**每个事件只前置一次**。
        ///   * 激活沿：记录事件开始；
        ///   * 激活期间只抢一次前台；抢到（或本来就在前台）即标记 Satisfied，
        ///     此后即使窗口失去前台也不再抢回来 —— 用户主动切走必须被尊重；
        ///   * 只有一次都没抢到才继续重试，最多 MaxFocusAttempts 次；
        ///   * 结束沿：重置状态，等待下一个事件。
        ///
        /// 对「假性结束」做了保护：游戏推进战斗阶段时，窗口级判定可能有一瞬间
        /// 读到 false（例如中场战斗会把同伴的防御按钮置为不可见），因此激活沿后
        /// MinHoldSeconds 秒内忽略伪结束，避免同一个事件被切成两段、重新抢一次前台。
        /// </summary>
        private static void Handle(Trigger t, bool active, long key, string desc)
        {
            if (!active)
            {
                if (!t.WasActive) return;

                // 抑制「假性结束」：游戏推进战斗阶段时窗口判定会瞬时为假。
                // 只在时间窗内且事件身份未变时才忽略 —— 身份变了说明是全新事件，
                // 必须立刻重开，否则会漏掉紧接着的下一个事件。
                if (Clock.Elapsed.TotalSeconds - t.ActivatedAt < Config.MinHoldSeconds.Value
                    && key == t.Key)
                    return;

                Diagnostics.Log("结束: " + t.Name +
                                (t.Satisfied ? "（本次已前置 " + t.Attempts + " 次）"
                                             : "（未抢到前台，放弃）"));
                t.WasActive = false;
                t.Satisfied = false;
                t.Attempts = 0;
                t.SkipLogged = false;
                t.Key = 0;
                return;
            }

            // 事件身份变化 = 新事件（即使上一事件的结束沿被抖动吞掉了）
            bool isNew = !t.WasActive || key != t.Key;
            if (isNew)
            {
                if (t.WasActive) Diagnostics.Log("结束(事件更替): " + t.Name);
                t.WasActive = true;
                t.Satisfied = false;
                t.Attempts = 0;
                t.SkipLogged = false;
                t.ActivatedAt = Clock.Elapsed.TotalSeconds;
                t.Key = key;
                Diagnostics.Log("激活: " + desc);
            }

            // 本次事件已经前置过 → 绝不再抢，从根上杜绝「切走又被拉回来」
            if (t.Satisfied) return;

            if (_hWnd == IntPtr.Zero) return;

            if (Config.LogOnly.Value)
            {
                if (!t.SkipLogged)
                {
                    t.SkipLogged = true;
                    Diagnostics.Log("触发: " + desc + "  (探查模式，未前置)");
                    Plugin.LogSource?.LogInfo("[AstralPartyAutoFocus] 触发: " + desc + "  (探查模式，未前置)");
                }
                t.Satisfied = true;
                return;
            }

            // 本来就在前台：本次事件无需动作，算作已满足
            if (Win32.GetForegroundWindow() == _hWnd)
            {
                if (!t.SkipLogged)
                {
                    t.SkipLogged = true;
                    Diagnostics.Log("触发(" + desc + ") 游戏已在前台，本次不再前置");
                }
                t.Satisfied = true;
                return;
            }

            if (t.Attempts >= Config.MaxFocusAttempts.Value)
            {
                if (!t.SkipLogged)
                {
                    t.SkipLogged = true;
                    Diagnostics.Log("触发(" + desc + ") 已达最大尝试次数，本次放弃");
                }
                t.Satisfied = true;
                return;
            }

            // 重试限流：最快 400ms 一次（各触发点独立，避免互相拖住）
            if (t.AttemptGate.Elapsed.TotalMilliseconds < 400) return;
            t.AttemptGate.Restart();
            t.Attempts++;

            Plugin.LogSource?.LogInfo("[AstralPartyAutoFocus] 触发: " + desc + "  → 前置窗口");
            if (Focus())
            {
                t.Satisfied = true;     // 抢到即满足，后续切走不再抢回
            }
            else
            {
                Diagnostics.Log("触发(" + desc + ") 第 " + t.Attempts + " 次前置失败，将继续重试");
            }

        }
        /// <summary>返回 true 表示窗口现在确实位于前台。</summary>
        private static bool Focus()
        {
            if (_hWnd == IntPtr.Zero)
            {
                Diagnostics.Log("Focus: 窗口句柄为空，跳过");
                return false;
            }

            var fgBefore = Win32.GetForegroundWindow();
            if (fgBefore == _hWnd)
            {
                Diagnostics.Log("Focus: 已是前台，跳过");
                return true;
            }

            bool restored = false;
            bool brought = false;
            if (Config.BringToFront.Value) brought = Win32.ForceForeground(_hWnd, out restored);

            int topMs = Config.TopMostMs.Value;
            if (topMs > 0)
            {
                Win32.SetTopMost(_hWnd, true);
                _unTopAt = (float)Clock.Elapsed.TotalSeconds + topMs / 1000f;
            }

            var fgAfter = Win32.GetForegroundWindow();
            bool ok = fgAfter == _hWnd;
            Diagnostics.Log("Focus: hWnd=" + _hWnd + " 前台前=" + fgBefore + " 前台后=" + fgAfter +
                            " 抢前台试图=" + Config.BringToFront.Value + " 结果=" + brought +
                            " 窗口还原=" + restored + " 置顶=" + (topMs > 0) +
                            " 成功=" + ok);
            return ok;
        }

        private static void LateTick()
        {
            if (_unTopAt > 0f && (float)Clock.Elapsed.TotalSeconds >= _unTopAt)
            {
                _unTopAt = 0f;
                if (_hWnd != IntPtr.Zero) Win32.SetTopMost(_hWnd, false);
            }
        }

        public static void Shutdown()
        {
            _running = false;
            try { _thread?.Join(500); } catch { }

            // 撤销置顶，并确保窗口不是 TOPMOST（游戏崩溃/强杀时这一步尤其重要）
            if (_hWnd != IntPtr.Zero)
            {
                try
                {
                    Win32.SetTopMost(_hWnd, false);
                    Win32.SetWindowPos(_hWnd, Win32.HWND_NOTOPMOST, 0, 0, 0, 0,
                        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
                catch { }
            }
            Diagnostics.Log("Shutdown: 已清理（撤置顶）");
        }

        /// <summary>
        /// 注册进程退出兜底：正常关闭走 Unload；若走不到（异常/强杀），
        /// 由 ProcessExit 尽力撤销置顶，避免游戏窗口残留 TOPMOST。
        /// </summary>
        private static void RegisterExitSafetyNet()
        {
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                {
                    try
                    {
                        if (_hWnd != IntPtr.Zero)
                            Win32.SetWindowPos(_hWnd, Win32.HWND_NOTOPMOST, 0, 0, 0, 0,
                                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                    }
                    catch { }
                };
                AppDomain.CurrentDomain.DomainUnload += (_, __) =>
                {
                    try
                    {
                        if (_hWnd != IntPtr.Zero)
                            Win32.SetWindowPos(_hWnd, Win32.HWND_NOTOPMOST, 0, 0, 0, 0,
                                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                    }
                    catch { }
                };
            }
            catch { }
        }

        private static void ResolveWindow()
        {
            if (_hWnd != IntPtr.Zero)
            {
                Win32.GetWindowThreadProcessId(_hWnd, out uint livePid);
                if (livePid == (uint)_gamePid && _gamePid != 0) return;
                _hWnd = IntPtr.Zero;
            }

            _gamePid = Process.GetCurrentProcess().Id;

            var found = IntPtr.Zero;
            Win32.EnumWindows((h, _) =>
            {
                Win32.GetWindowThreadProcessId(h, out uint pid);
                if (pid != (uint)_gamePid) return true;
                if (!Win32.IsWindowVisible(h)) return true;
                if (Win32.GetWindow(h, Win32.GW_OWNER) != IntPtr.Zero) return true;
                var sb = new StringBuilder(256);
                Win32.GetClassName(h, sb, sb.Capacity);
                if (sb.ToString() != "UnityWndClass") return true;
                found = h;
                return false;
            }, IntPtr.Zero);

            _hWnd = found;
        }
    }
}
