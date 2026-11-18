// RetinuesDependencyBridge: the Retinues switch of the TAOM All-In-One compatibility patch.
//
// Retinues v2 (by bolaft) checks at start-up for active modules with the ids "Bannerlord.Harmony"
// and "Bannerlord.UIExtenderEx". TAOM ships both libraries inside TAOM.Dependencies instead, so the
// check fails, Retinues' Harmony object stays null and its OnSubModuleLoad throws (game crash).
// This class adds one Harmony postfix to Retinues.Framework.Modules.ModuleManager.GetModule(string):
// when Retinues asks for one of those two ids and TAOM.Dependencies provides the library, the answer
// says "loaded" with the bundled library's version. Nothing else changes. No game method is patched,
// and no file of Retinues or TAOM is edited or shipped.
//
// Kept free of SubModule code so it can move into the All-In-One mod unchanged (Install/Uninstall).
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Library;

namespace TAOM_AIO_Compat.Fixes.Retinues
{
    internal static class RetinuesDependencyBridge
    {
        internal const string HarmonyId = "taom.compat.aio.retinues"; // "taom." prefix: TAOM PatchShield treats it as protected
        internal const string RetinuesAssemblyName = "Retinues";
        private const string ModuleManagerTypeName = "Retinues.Framework.Modules.ModuleManager";
        private const string HarmonyModuleId = "Bannerlord.Harmony";
        private const string UIExtenderModuleId = "Bannerlord.UIExtenderEx";
        private const string UIExtenderAssemblyName = "Bannerlord.UIExtenderEx";
        private const string TaomDependenciesModuleId = "TAOM.Dependencies";

        private static Harmony _harmony;
        private static MethodInfo _target;
        private static PropertyInfo _appVersionProp;
        private static PropertyInfo _nameProp;
        private static ApplicationVersion _harmonyVersion = ApplicationVersion.Empty;
        private static ApplicationVersion _uiExtenderVersion = ApplicationVersion.Empty;
        private static Action<string> _log;

        internal static bool IsInstalled => _harmony != null;

        internal static Assembly FindRetinues() =>
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == RetinuesAssemblyName);

        /// <summary>Installs the postfix. Returns false (with the reason logged) if it cannot.</summary>
        internal static bool Install(Action<string> log)
        {
            _log = log ?? (_ => { });
            if (_harmony != null)
                return true;

            Assembly retinues = FindRetinues();
            if (retinues == null)
            {
                _log("Retinues assembly is not loaded. Nothing changed.");
                return false;
            }

            string[] active;
            try { active = TaleWorlds.Engine.Utilities.GetModulesNames() ?? new string[0]; }
            catch (Exception e) { _log("Could not read the active module list: " + e.Message + ". Nothing changed."); return false; }
            if (!active.Any(m => string.Equals(m, TaomDependenciesModuleId, StringComparison.OrdinalIgnoreCase)))
            {
                _log("TAOM.Dependencies is not active. Nothing changed.");
                return false;
            }

            Type mm = retinues.GetType(ModuleManagerTypeName, false);
            _target = mm?.GetMethod("GetModule", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            Type infoType = _target?.ReturnType;
            _appVersionProp = infoType?.GetProperty("AppVersion", BindingFlags.Public | BindingFlags.Instance);
            _nameProp = infoType?.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            if (_target == null || _appVersionProp == null || !_appVersionProp.CanWrite || _appVersionProp.PropertyType != typeof(ApplicationVersion) || _nameProp == null || !_nameProp.CanWrite)
            {
                _log("This Retinues build (" + retinues.GetName().Version + ") does not have the expected "
                     + ModuleManagerTypeName + ".GetModule(string) / ModuleInfo.AppVersion. Nothing changed. "
                     + "Retinues v2 will crash at start-up on TAOM without this fix: untick Retinues.");
                return false;
            }

            Assembly uix;
            try
            {
                _harmonyVersion = ToAppVersion(typeof(Harmony).Assembly);
                uix = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == UIExtenderAssemblyName);
                _uiExtenderVersion = uix == null ? ApplicationVersion.Empty : ToAppVersion(uix);
            }
            catch (Exception e)
            {
                _log("Could not read the bundled Harmony/UIExtenderEx versions: " + e.Message + ". Nothing changed. "
                     + "Retinues v2 will crash at start-up on TAOM without this fix: untick Retinues.");
                return false;
            }

            try
            {
                var h = new Harmony(HarmonyId);
                h.Patch(_target, postfix: new HarmonyMethod(typeof(RetinuesDependencyBridge).GetMethod(nameof(GetModulePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                _harmony = h;
            }
            catch (Exception e)
            {
                _log("Harmony could not patch " + ModuleManagerTypeName + ".GetModule: " + e.Message + ". Nothing changed.");
                return false;
            }

            _log("Installed for Retinues " + retinues.GetName().Version + ". "
                 + HarmonyModuleId + " -> " + _harmonyVersion + " (0Harmony from TAOM.Dependencies); "
                 + UIExtenderModuleId + " -> " + (uix == null ? "not bundled, left as missing" : _uiExtenderVersion + " (from TAOM.Dependencies)") + ".");
            return true;
        }

        internal static void Uninstall()
        {
            if (_harmony == null)
                return;
            try { _harmony.Unpatch(_target, HarmonyPatchType.Postfix, HarmonyId); } catch { }
            _harmony = null;
        }

        // Major.Minor.Build of the assembly (System.Version.Build is the third number), as a Release version,
        // so Retinues' own version text parser ("v2.4.2.0") accepts it.
        private static ApplicationVersion ToAppVersion(Assembly a)
        {
            Version v = a.GetName().Version;
            return new ApplicationVersion(ApplicationVersionType.Release, v.Major, v.Minor, Math.Max(0, v.Build), 0);
        }

        // Postfix on Retinues.Framework.Modules.ModuleManager.GetModule(string id). __result is a Retinues
        // ModuleInfo (reference type); only its properties are set. Uses __args so it does not depend on
        // the parameter's name.
        private static void GetModulePostfix(object[] __args, object __result)
        {
            try
            {
                if (__result == null || __args == null || __args.Length < 1 || !(__args[0] is string id))
                    return;
                if ((ApplicationVersion)_appVersionProp.GetValue(__result) != ApplicationVersion.Empty)
                    return; // a real module with this id is active: leave it alone

                ApplicationVersion v;
                if (string.Equals(id, HarmonyModuleId, StringComparison.OrdinalIgnoreCase)) v = _harmonyVersion;
                else if (string.Equals(id, UIExtenderModuleId, StringComparison.OrdinalIgnoreCase)) v = _uiExtenderVersion;
                else return;
                if (v == ApplicationVersion.Empty)
                    return;

                _appVersionProp.SetValue(__result, v);
                _nameProp.SetValue(__result, id + " (bundled in TAOM.Dependencies)");
            }
            catch (Exception e)
            {
                try { _log?.Invoke("Postfix error (ignored): " + e.Message); } catch { }
            }
        }
    }
}
