// What is loaded: active module ids (launcher ticks) and assemblies in the AppDomain.
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.ModuleManager;

namespace TAOM_AIO_Compat.Core
{
    internal static class ModEnv
    {
        private static string[] _active;

        /// <summary>Ids of the modules the game was started with (TaleWorlds.Engine.Utilities.GetModulesNames).</summary>
        internal static string[] ActiveModules
        {
            get
            {
                if (_active == null)
                {
                    try { _active = TaleWorlds.Engine.Utilities.GetModulesNames() ?? new string[0]; }
                    catch (Exception e) { AioLog.Write("Could not read the active module list: " + e.Message); _active = new string[0]; }
                }
                return _active;
            }
        }

        internal static bool IsActive(string moduleId) =>
            ActiveModules.Any(m => string.Equals(m, moduleId, StringComparison.OrdinalIgnoreCase));

        /// <summary>Version text of an installed module ("v4.8.1"), or null.</summary>
        internal static string ModuleVersion(string moduleId)
        {
            try
            {
                ModuleInfo info = ModuleHelper.GetModuleInfo(moduleId);
                return info == null ? null : info.Version.ToString();
            }
            catch { return null; }
        }

        internal static Assembly FindAssembly(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                        return a;
                }
                catch { }
            }
            return null;
        }

        internal static Type FindType(string assemblyName, string typeName) =>
            FindAssembly(assemblyName)?.GetType(typeName, false);
    }
}
