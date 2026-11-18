// CaravanCoopGuard: keeps RBM's kingdom caravan dispatch from running on a co-op client (RBM campaign
// part, RBMCampaign.dll). RBMCampaign.dll is not referenced; the target is found by name and is left alone
// when it is not found (one log line).
//
// Why (audit 14 Nov 2026, Coop_client.log 13 Nov 17:39:50 and 18:45:06, and the installed DLL):
// RBMCaravanBehavior.OnDailyTick calls RBMCaravanDispatch.RunDispatch every second day on every machine.
// On a client SpawnCaravan -> CaravanPartyComponent.CreateCaravanParty throws a NullReferenceException in
// OnMobilePartySetOnCreation; each one aborts that client's whole game tick ("Failed to run Game.OnTick")
// and leaves a party that Co-op does not know. The host dispatches the caravans and Co-op syncs them.
//
// What, only while CoopEnv.IsCoopClient():
//   RBMCampaign.RBMCaravanDispatch.RunDispatch()   (public static void)   skipped
//
// Installed once (TryInstall is called at the main menu, game start and mission start until it returns
// true) and kept until the switch is uninstalled; the condition is evaluated on every call.
// Co-op: host's server process and single player: never skips. Client: no caravan is dispatched locally;
// the host's caravans arrive through Co-op as before.
using System;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.RBM.Campaign
{
    internal static class CaravanCoopGuard
    {
        private const string HarmonyId = "taom.compat.aio.rbm.caravancoopguard";
        private const string RbmCampaignAssembly = "RBMCampaign";
        private const string DispatchType = "RBMCampaign.RBMCaravanDispatch";

        private static Harmony _harmony;
        private static bool _done;
        private static bool _loggedSkip, _loggedError;
        private static Action<string> _log = _ => { };

        /// <summary>
        /// False while RBMCampaign.dll is not loaded yet (call again at the next event). True when finished:
        /// installed, or not installable (one log line). Never throws.
        /// </summary>
        internal static bool TryInstall(Action<string> log)
        {
            if (_done)
                return true;
            _log = log ?? (_ => { });
            try
            {
                Assembly asm = ModEnv.FindAssembly(RbmCampaignAssembly);
                if (asm == null)
                    return false;
                _done = true;
                Type dispatch = asm.GetType(DispatchType, false);
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo target = dispatch?.GetMethod("RunDispatch", flags, null, Type.EmptyTypes, null);
                if (target == null || target.ReturnType != typeof(void))
                {
                    _log("Co-op caravan guard: " + DispatchType + ".RunDispatch() not found. Not installed.");
                    return true;
                }
                var h = new Harmony(HarmonyId);
                _harmony = h;
                h.Patch(target, prefix: new HarmonyMethod(typeof(CaravanCoopGuard).GetMethod(nameof(ClientSkipDispatch), BindingFlags.Static | BindingFlags.NonPublic)));
                _log("Co-op caravan guard installed: RBMCaravanDispatch.RunDispatch().");
            }
            catch (Exception e)
            {
                _done = true;
                _log("Co-op caravan guard: install failed: " + e.Message + ".");
            }
            return true;
        }

        internal static void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _done = false;
        }

        // Returning false skips RBM's method.
        private static bool ClientSkipDispatch()
        {
            try
            {
                if (!CoopEnv.IsCoopClient())
                    return true;
                if (!_loggedSkip)
                {
                    _loggedSkip = true;
                    try { _log("Co-op client: skipped RBM's caravan dispatch on this machine (the host dispatches the caravans). Logged once."); } catch { }
                }
                return false;
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    try { _log("Co-op caravan guard failed (RBM runs as usual): " + e.Message); } catch { }
                }
                return true;
            }
        }
    }
}
