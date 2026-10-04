using BepInEx.Configuration;

namespace AstralFocus
{
    internal static class Config
    {
        public static ConfigEntry<int> PollIntervalMs;
        public static ConfigEntry<int> TopMostMs;
        public static ConfigEntry<bool> LogOnly;
        public static ConfigEntry<bool> BringToFront;
        public static ConfigEntry<bool> OnlyWhileGameRunning;
        public static ConfigEntry<bool> ProbeEnabled;
        public static ConfigEntry<bool> SelfTest;
        public static ConfigEntry<int> MaxFocusAttempts;
        public static ConfigEntry<float> MinHoldSeconds;

        public static void Load(ConfigFile cfg)
        {
            LogOnly = cfg.Bind("General", "LogOnly", true,
                "探查模式：只写日志，不前置窗口。首次校准用，确认触发点正确后改为 false。");
            PollIntervalMs = cfg.Bind("General", "PollIntervalMs", 150,
                "状态轮询间隔（毫秒）。150 足够灵敏且开销极低。");
            MaxFocusAttempts = cfg.Bind("General", "MaxFocusAttempts", 6,
                "单个事件最多尝试几次抢前台。抢到一次即停止；一直抢不到（前台锁拒绝）时达到次数后放弃本次事件。");
            MinHoldSeconds = cfg.Bind("General", "MinHoldSeconds", 1.0f,
                "事件开始后多少秒内忽略「判定变为非我」的抖动。游戏推进战斗阶段时窗口判定会瞬时为假，不保护的话同一事件会被切成两段、重新抢一次前台。");
            BringToFront = cfg.Bind("Focus", "BringToFront", true,
                "抢前台焦点。");
            TopMostMs = cfg.Bind("Focus", "TopMostMs", 700,
                "临时置顶持续毫秒数。设 0 表示不置顶。");
            OnlyWhileGameRunning = cfg.Bind("Focus", "OnlyWhileGameRunning", false,
                "仅在游戏窗口可见时动作。");
            SelfTest = cfg.Bind("General", "SelfTest", false,
                "自检模式：逐个测试 il2cpp API 可用性（排查用）。");
            ProbeEnabled = cfg.Bind("General", "ProbeEnabled", true,
                "是否读取游戏状态。用于排查：关掉后插件完全不碰 il2cpp。");
        }
    }
}
