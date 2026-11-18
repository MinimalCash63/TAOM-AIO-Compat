// v1.3.0 (audit 14 Nov 2026). Co-op guard for RBM's mercenary companies (RBMCampaign.dll, RBM 4.8.1).
// Seen in Coop_client.log on 14 Nov 2026 (11:33:36-11:36:19): on a co-op client, RBMMercenaryCompanyBehavior's
// tick calls MercenaryCompanies.EnsureRoster -> CreateCompany -> Clan.CreateClan, which throws a
// NullReferenceException there (Co-op stops a client from giving the new clan its id). The exception left the
// campaign tick 199 times in under three minutes ("Failed to run Game.OnTick") and the client repeated the
// same daily tick 198 times.
// Guard, only when the Coop module is ticked (single player without Coop is never touched):
//  - finalizer on EnsureRoster: an exception is logged once and not passed on, so the campaign tick goes on;
//  - prefix on EnsureRoster: after the first such exception, EnsureRoster is skipped for the rest of that
//    campaign (the host creates the companies), so the failing call is not repeated every tick.
// v1.4.0 (plan 2a, 14 Nov 2026): the prefix also skips EnsureRoster whenever this process is a client of a
// running co-op session (CoopEnv.IsCoopClient), before any failure, so no half-made clan is left on a client
// at all. The finalizer stays as the net for anything the check misses.
// Host: never skipped by the new check. Client: always skipped. Single player with Coop ticked: not a client
// (no session container), so RBM creates its companies as usual.
// Finalizer on EnsureRoster, not on CreateCompany (plan review): swallowing inside CreateCompany would let
// all 14 company slots try and fail on every call; here the loop stops at the first failure, as it does today.
// EnsureRoster reads no mission-only statics (Campaign.Current, MBObjectManager, Clan.All, RBM's own timers).
using System;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.RBM.Campaign
{
    internal static class MercenaryGuard
    {
        internal const string AssemblyName = "RBMCampaign";
        internal const string TypeName = "RBMCampaign.MercenaryCompanies";
        internal const string MethodName = "EnsureRoster";

        private static bool _skipRest;
        private static int _campaign; // counts campaigns, so the log line appears once per campaign

        /// <summary>New campaign or campaign ended: try again (this machine may be the host next time).</summary>
        internal static void Reset()
        {
            _skipRest = false;
            _campaign++;
        }

        /// <summary>
        /// Null while RBMCampaign.dll is not loaded (try again later). Throws if the method is not there.
        /// </summary>
        internal static MethodInfo FindTarget()
        {
            if (ModEnv.FindAssembly(AssemblyName) == null)
                return null;
            Type type = ModEnv.FindType(AssemblyName, TypeName)
                ?? throw new MissingMemberException(TypeName + " not found in " + AssemblyName + ".dll");
            MethodInfo method = AccessTools.DeclaredMethod(type, MethodName, Type.EmptyTypes)
                ?? throw new MissingMethodException(TypeName + "." + MethodName + "()");
            if (!method.IsStatic || method.ReturnType != typeof(void))
                throw new MissingMethodException(TypeName + "." + MethodName + "() is not a static void method any more");
            return method;
        }

        internal static void Install(Harmony harmony, MethodInfo target)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(MercenaryGuard).GetMethod(nameof(Prefix), flags)),
                finalizer: new HarmonyMethod(typeof(MercenaryGuard).GetMethod(nameof(Finalizer), flags)));
        }

        private static bool Prefix()
        {
            if (_skipRest)
                return false;
            try
            {
                if (CoopEnv.IsCoopClient())
                {
                    RbmLog.WriteOnce("mercguard-client|" + _campaign, "Co-op guard: this machine is a co-op client, so RBM's mercenary "
                        + "company set-up is skipped here (the host creates the companies).");
                    return false;
                }
            }
            catch { } // CoopEnv never throws; if it did, RBM's own code runs and the finalizer is the net
            return true;
        }

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception == null)
                return null;
            _skipRest = true;
            RbmLog.WriteOnce("mercguard|" + _campaign, "Co-op guard: RBM could not create its mercenary companies on this machine ("
                + __exception.GetType().Name + ": " + __exception.Message + "). Expected on a co-op client, where the host creates them; "
                + "RBM's company set-up is skipped here for the rest of this campaign so the campaign tick goes on. "
                + "On the host or without a co-op session this would be a real RBM error.");
            return null;
        }
    }
}
