// IgCoopGuards: keeps Improved Garrisons' hourly and daily routines from running on a co-op client
// (GarrisonBehavior of the installed Improved Garrisons). ImprovedGarrisons.dll is not referenced; the
// targets are found by name and a target that is not found is left alone (one log line).
//
// Why (audit 14 Nov 2026, Coop_client.log 13 and 14 Nov and the installed DLLs): GarrisonBehavior.HourlyEvent
// runs ImprovedSettlement.HourlyThinkBehavior for every town and castle on every machine. On a client its
// DisableVanillaTrainingIfNeeded sets the garrison wage limit every hour (about 9,000 logged client writes
// in one session; the value is the one the host already syncs). The same routines can also create guard and
// recruiter parties, collect region taxes and, daily, remove troops from garrisons. On a client those are
// local changes the host never sees. The host runs the same routines and Co-op syncs the results.
//
// What, only while CoopEnv.IsCoopClient():
//   ImprovedGarrisons.SaveSystem.GarrisonBehavior.HourlyEvent()   (private instance void)   skipped
//   ImprovedGarrisons.SaveSystem.GarrisonBehavior.DailyEvent()    (private instance void)   skipped
// Improved Garrisons' screens and settings are not touched.
//
// Installed once (TryInstall is called at the main menu, game start and mission start until it returns
// true) and kept until the switch is uninstalled; the condition is evaluated on every call.
// Co-op: host's server process and single player: never skips. Client: Improved Garrisons' routines do not
// run there, so the clan finance tooltip shows no Improved Garrisons cost lines on a client, and settings a
// joining player makes for their own fiefs have no effect; the host's Improved Garrisons decides.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.ImprovedGarrisons
{
    internal static class IgCoopGuards
    {
        private const string HarmonyId = "taom.compat.aio.improvedgarrisons.coopguards";
        private const string IgAssembly = "ImprovedGarrisons";
        private const string GarrisonBehaviorType = "ImprovedGarrisons.SaveSystem.GarrisonBehavior";

        private static Harmony _harmony;
        private static bool _done;
        private static Action<string> _log = _ => { };
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>
        /// False while ImprovedGarrisons.dll is not loaded yet (call again at the next event). True when finished:
        /// installed, or not installable (one log line per missing target). Never throws.
        /// </summary>
        internal static bool TryInstall(Action<string> log)
        {
            if (_done)
                return true;
            _log = log ?? (_ => { });
            try
            {
                Assembly asm = ModEnv.FindAssembly(IgAssembly);
                if (asm == null)
                    return false;
                _done = true;
                Type behavior = asm.GetType(GarrisonBehaviorType, false);
                if (behavior == null)
                {
                    _log("Co-op client guard: " + GarrisonBehaviorType + " not found. Not installed.");
                    return true;
                }
                var h = new Harmony(HarmonyId);
                _harmony = h;
                int installed = 0;
                if (Guard(h, behavior, "HourlyEvent", nameof(ClientSkipHourly))) installed++;
                if (Guard(h, behavior, "DailyEvent", nameof(ClientSkipDaily))) installed++;
                _log("Co-op client guard: " + installed + " of 2 parts installed.");
            }
            catch (Exception e)
            {
                _done = true;
                _log("Co-op client guard: install failed: " + e.Message + ".");
            }
            return true;
        }

        internal static void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _done = false;
        }

        private static bool Guard(Harmony h, Type type, string method, string prefix)
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo target = type.GetMethod(method, flags, null, Type.EmptyTypes, null);
                if (target == null || target.ReturnType != typeof(void))
                {
                    _log("Co-op client guard " + method + "(): not found in " + type.Name + ". Skipped.");
                    return false;
                }
                MethodInfo patch = typeof(IgCoopGuards).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
                h.Patch(target, prefix: new HarmonyMethod(patch));
                return true;
            }
            catch (Exception e)
            {
                _log("Co-op client guard " + method + "(): install failed: " + e.Message + ". Skipped.");
                return false;
            }
        }

        // ---- Prefixes. Returning false skips Improved Garrisons' method.

        private static bool ClientSkipHourly() => !IsClient("hourly", "the hourly garrison routines");
        private static bool ClientSkipDaily() => !IsClient("daily", "the daily garrison routines");

        private static bool IsClient(string key, string what)
        {
            try
            {
                if (!CoopEnv.IsCoopClient())
                    return false;
                LogOnce(key, "Co-op client: skipped " + what + " on this machine (the host's Improved Garrisons decides). Logged once.");
                return true;
            }
            catch (Exception e)
            {
                LogOnce("error|" + key, "Co-op client guard (" + key + ") failed (Improved Garrisons runs as usual): " + e.Message);
                return false;
            }
        }

        private static void LogOnce(string key, string message)
        {
            lock (Logged)
            {
                if (!Logged.Add(key))
                    return;
            }
            try { _log(message); } catch { }
        }
    }
}
