using System;
using System.IO;

namespace AstralParty.AutoFocus
{
    /// <summary>立即刷盘的诊断日志（BepInEx 日志在崩溃时可能丢缓冲）。</summary>
    internal static class Diagnostics
    {
        private static readonly object Gate = new object();

        private static string FilePath
        {
            get
            {
                try { return Path.Combine(BepInEx.Paths.BepInExRootPath, "AstralPartyAutoFocus.diag.log"); }
                catch { return "AstralPartyAutoFocus.diag.log"; }
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(FilePath, DateTime.Now.ToString("HH:mm:ss.fff ") + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
