// One log file for the whole module: Documents\Mount and Blade II Bannerlord\Logs\taom_aio_compat.log
using System;
using System.IO;

namespace TAOM_AIO_Compat.Core
{
    internal static class AioLog
    {
        private static string _path;
        private static string _pid; // v1.3.0: a co-op host and client on one PC write to the same file

        internal static void Write(string message)
        {
            try { TaleWorlds.Library.Debug.Print("[TAOM_AIO_Compat] " + message); } catch { }
            try
            {
                if (_path == null)
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord", "Logs");
                    Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, "taom_aio_compat.log");
                }
                if (_pid == null)
                {
                    try { _pid = " p" + System.Diagnostics.Process.GetCurrentProcess().Id; } catch { _pid = ""; }
                }
                File.AppendAllText(_path, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + _pid + "] " + message + Environment.NewLine);
            }
            catch { }
        }

        /// <summary>Logger that prefixes every line with the switch name, e.g. "[Retinues] ...".</summary>
        internal static Action<string> For(string name) => m => Write("[" + name + "] " + m);
    }
}
