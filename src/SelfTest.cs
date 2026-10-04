using System;
using System.Threading;

namespace AstralFocus
{
    /// <summary>
    /// 运行中持续探测：跟踪"回合奖励卡"相关对象与窗口状态的真实变化，
    /// 用于确认真实触发条件（而不是靠猜）。
    /// </summary>
    internal static class SelfTest
    {
        private static long _lastSig;

        public static void Run()
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    try { Scan(); } catch (Exception e) { Diagnostics.Log("探测异常: " + e.Message); }
                    Thread.Sleep(300);
                }
            }) { IsBackground = true, Name = "AstralFocusProbe" };
            t.Start();
        }

        private static void Scan()
        {
            var probe = new GameProbe();
            probe.Initialize();
            if (!probe.Ready) { Diagnostics.Log("探测: 未就绪 " + probe.LastError); return; }

            var self = probe.SelfPlayerId();
            var cur = probe.CurrentPlayerId();
            var ft = probe.FightType();
            var d = probe.DebugRoundCard();
            var def = probe.IsDefendingNow();

            // 只在状态发生变化时记录，避免刷屏
            long sig = (long)ft * 1000003 + (long)d.UiInst.GetHashCode() * 7 + (long)d.Win.GetHashCode() * 13 + d.Sn + (self == cur && self != 0 ? 1 : 0) * 1_000_000 + (def ? 1 : 0) * 2_000_000 + (d.Visible ? 1 : 0) * 4_000_000;
            if (sig == _lastSig) return;
            _lastSig = sig;

            Diagnostics.Log("状态 self=" + self + " cur=" + cur + " fightType=" + ft + " defending=" + def +
                " | ui=" + d.UiInst + " win=" + d.Win + " sn=" + d.Sn + " visible=" + d.Visible);
        }
    }
}
