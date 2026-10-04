using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace AstralParty.AutoFocus
{
    [BepInPlugin(Guid, "Astral Party AutoFocus", "0.4.0")]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "astralparty.autofocus";

        internal static ManualLogSource LogSource;

        public override void Load()
        {
            LogSource = base.Log;
            AstralParty.AutoFocus.Config.Load(base.Config);
            Diagnostics.Log("Load 进入");
            LogBuildStamp();

            if (AstralParty.AutoFocus.Config.SelfTest.Value)
            {
                Diagnostics.Log("进入自检模式（不启动常规监控）");
                SelfTest.Run();
                return;
            }

            try { Poller.Start(); LogSource.LogInfo("[AstralPartyAutoFocus] 监控已启动。"); }
            catch (Exception e) { Diagnostics.Log("启动失败: " + e); }
        }

        /// <summary>
        /// 记录本次实际加载的程序集指纹。
        /// 曾出现过「改了源码、构建成功，但部署的是旧 DLL」导致日志与源码对不上、
        /// 排查方向被带偏的情况，这里把构建指纹写进诊断日志，一眼可核对。
        /// </summary>
        private static void LogBuildStamp()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var loc = asm.Location;
                string written = "?";
                try { written = System.IO.File.GetLastWriteTime(loc).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                Diagnostics.Log("构建指纹 MVID=" + asm.ManifestModule.ModuleVersionId +
                                " DLL写入时间=" + written +
                                " 路径=" + loc);
            }
            catch (Exception e) { Diagnostics.Log("记录构建指纹失败: " + e.Message); }
        }

        public override bool Unload()
        {
            try { Poller.Shutdown(); } catch { }
            return true;
        }
    }
}
