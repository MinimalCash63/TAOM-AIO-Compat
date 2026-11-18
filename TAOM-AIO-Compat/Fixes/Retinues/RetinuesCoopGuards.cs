// RetinuesCoopGuards: two guards on Retinues v2's own methods (Retinues 2.0.0.15 by bolaft). Retinues.dll is
// not referenced; every target is found by name and a target that is not found is left alone (one log line).
//
// Why (audit 14 Nov 2026, Documents\Retinues\Logs\debug.log and Coop_client.log, 142 handled errors):
//  Guard A, co-op client. When a player joins a co-op session, Retinues builds a default retinue for the
//    joining player's clan on that player's machine (RetinuesBehavior.OnGameLoadFinished). Co-op does not
//    let a client build campaign objects: it skips the MBCharacterSkills constructor and the StringId setter
//    there, so the troop ends up with no skill container (96 errors on two joins, 13 and 14 Nov 2026).
//    The guard makes Retinues build and change nothing on a co-op client, at all times
//    (CoopEnv.IsCoopClient(): this process is a client of a running co-op session):
//      RetinuesBehavior.CreateRetinue(WCulture, string, bool) : WCharacter         returns null
//      AIClanRetinuesBehavior.CreateAIClanRetinue(WCulture, string, int) : WCharacter   returns null
//      AIClanRetinuesBehavior.RefreshKingsGuardForClan(WClan, Hero)                skipped (it removes the
//          old king's guard before it creates the new one)
//      AIClanRetinuesBehavior.TryDailyEquipmentUpgradesForAllAIRetinues()          skipped
//      AIClanRetinuesBehavior.OnDailyTickParty(WParty)                             skipped
//      AIClanRetinuesBehavior.OnMapEventEnded(MMapEvent)                           skipped
//      AIClanRetinuesBehavior.OnMissionEnded(MMission)                             skipped (both put
//          looted items on AI retinue troops, rolled at random on each machine; added 14 Nov 2026)
//    Retinues' callers of the two create methods handle null (only its create_retinue console command
//    does not).
//  Guard B, new game. AIClanRetinuesBehavior.OnClanTierIncreased(Clan) can run inside
//    Campaign.OnNewGameCreated (the tournament leaderboard gives clans renown) before Retinues has
//    registered its doctrines; every doctrine lookup is then null (46 errors in one new game, 14 Nov 2026,
//    single player, TAOM clan reaching tier 3). The guard skips that call while
//    DoctrinesRegistry.RegistrationComplete is false and the campaign map is not the active state yet.
//    Retinues builds those retinues itself when character creation is over. Once the map is active the
//    guard never skips, so it cannot block for good if the doctrines are switched off.
//
// Installed at the main menu, once (Retinues.dll is loaded, no campaign is running). Removed in Uninstall.
// Co-op: on the host's server process and in single player guard A never skips anything; on a client
// nothing is built or changed locally, the host owns the troops. Guard B works the same everywhere and only
// during new-game set-up. Known limit, not fixable here: Retinues has no co-op sync, so a joining player's
// clan gets no retinue and host-side retinue changes reach the clients when they rejoin.
// A failure in here never affects the other Retinues parts (dependency bridge, map bar).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.Retinues
{
    internal static class RetinuesCoopGuards
    {
        private const string HarmonyId = "taom.compat.aio.retinues.coopguards";
        private const string RetinuesAssembly = "Retinues";
        private const string RetinuesBehaviorType = "Retinues.Behaviors.Retinues.RetinuesBehavior";
        private const string AiClanBehaviorType = "Retinues.Behaviors.Retinues.AIClanRetinuesBehavior";
        private const string DoctrinesRegistryType = "Retinues.Behaviors.Doctrines.DoctrinesRegistry";

        private static Harmony _harmony;
        private static Action<string> _log = _ => { };
        private static FieldInfo _registrationComplete;
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>Applies the guards that can be applied. Never throws. Returns the number installed.</summary>
        internal static int Install(Action<string> log)
        {
            _log = log ?? (_ => { });
            if (_harmony != null)
                return 0;
            int installed = 0;
            try
            {
                Assembly asm = ModEnv.FindAssembly(RetinuesAssembly);
                if (asm == null)
                {
                    _log("Retinues.dll is not loaded. Guards not installed.");
                    return 0;
                }
                var h = new Harmony(HarmonyId);
                _harmony = h;
                Type retinues = asm.GetType(RetinuesBehaviorType, false);
                Type aiClan = asm.GetType(AiClanBehaviorType, false);

                // Guard A
                if (Guard(h, retinues, "CreateRetinue", nameof(ClientSkipCreateRetinue), "WCulture", "String", "Boolean")) installed++;
                if (Guard(h, aiClan, "CreateAIClanRetinue", nameof(ClientSkipCreateAIClanRetinue), "WCulture", "String", "Int32")) installed++;
                if (Guard(h, aiClan, "RefreshKingsGuardForClan", nameof(ClientSkipRefreshKingsGuard), "WClan", "Hero")) installed++;
                if (Guard(h, aiClan, "TryDailyEquipmentUpgradesForAllAIRetinues", nameof(ClientSkipDailyUpgrades))) installed++;
                if (Guard(h, aiClan, "OnDailyTickParty", nameof(ClientSkipDailyTickParty), "WParty")) installed++;
                if (Guard(h, aiClan, "OnMapEventEnded", nameof(ClientSkipMapEventEnded), "MMapEvent")) installed++;
                if (Guard(h, aiClan, "OnMissionEnded", nameof(ClientSkipMissionEnded), "MMission")) installed++;

                // Guard B
                try
                {
                    Type registry = asm.GetType(DoctrinesRegistryType, false);
                    FieldInfo f = registry?.GetField("RegistrationComplete", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f == null || f.FieldType != typeof(bool))
                        _log("New-game guard: " + DoctrinesRegistryType + ".RegistrationComplete not found. Skipped.");
                    else
                    {
                        _registrationComplete = f;
                        if (Guard(h, aiClan, "OnClanTierIncreased", nameof(NewGameSkipClanTierIncreased), "Clan")) installed++;
                    }
                }
                catch (Exception e) { _log("New-game guard: install failed: " + e.Message + ". Skipped."); }
            }
            catch (Exception e)
            {
                _log("Guards: install failed: " + e.Message + ".");
            }
            return installed;
        }

        internal static void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _registrationComplete = null;
        }

        /// <summary>Finds type.method declared on the type itself with these parameter type names and adds the prefix.</summary>
        private static bool Guard(Harmony h, Type type, string method, string prefix, params string[] parameterTypeNames)
        {
            string label = method + "(" + string.Join(", ", parameterTypeNames) + ")";
            try
            {
                if (type == null)
                {
                    _log("Guard " + label + ": Retinues type not found. Skipped.");
                    return false;
                }
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo[] hits = type.GetMethods(flags).Where(m => m.Name == method
                    && m.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(parameterTypeNames)).ToArray();
                if (hits.Length != 1)
                {
                    _log("Guard " + label + ": " + hits.Length + " matches in " + type.Name + ". Skipped.");
                    return false;
                }
                MethodInfo patch = typeof(RetinuesCoopGuards).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
                h.Patch(hits[0], prefix: new HarmonyMethod(patch));
                _log("Guard installed: " + type.Name + "." + label + ".");
                return true;
            }
            catch (Exception e)
            {
                _log("Guard " + label + ": install failed: " + e.Message + ". Skipped.");
                return false;
            }
        }

        // ---- Guard A: co-op client. Returning false skips Retinues' method; a skipped method that returns a
        //      troop returns null (Harmony leaves the result at its default).

        private static bool ClientSkipCreateRetinue() => !ClientBlocked("CreateRetinue", "building a player retinue");
        private static bool ClientSkipCreateAIClanRetinue() => !ClientBlocked("CreateAIClanRetinue", "building an AI clan retinue");
        private static bool ClientSkipRefreshKingsGuard() => !ClientBlocked("RefreshKingsGuardForClan", "replacing a king's guard");
        private static bool ClientSkipDailyUpgrades() => !ClientBlocked("TryDailyEquipmentUpgradesForAllAIRetinues", "the daily AI retinue gear upgrades");
        private static bool ClientSkipDailyTickParty() => !ClientBlocked("OnDailyTickParty", "the daily AI retinue promotions");
        private static bool ClientSkipMapEventEnded() => !ClientBlocked("OnMapEventEnded", "the AI retinue loot after a battle result");
        private static bool ClientSkipMissionEnded() => !ClientBlocked("OnMissionEnded", "the AI retinue loot after a battle");

        private static bool ClientBlocked(string key, string what)
        {
            try
            {
                // Code review 14 Nov 2026: IsCoopClient, not IsClientWriteBlocked. Inside one of Co-op's allow scopes
                // the write gate is open, and a Retinues handler running there would build troops on the client after all.
                if (!CoopEnv.IsCoopClient())
                    return false;
                LogOnce("client|" + key, "Co-op client: skipped " + what + " on this machine (the host owns Retinues' troops). Logged once.");
                return true;
            }
            catch (Exception e)
            {
                LogOnce("client-error|" + key, "Client guard " + key + " failed (Retinues runs as usual): " + e.Message);
                return false;
            }
        }

        // ---- Guard B: new game, before Retinues registered its doctrines.

        private static bool NewGameSkipClanTierIncreased()
        {
            try
            {
                FieldInfo f = _registrationComplete;
                if (f == null || (bool)f.GetValue(null))
                    return true;
                if (Game.Current?.GameStateManager?.ActiveState is MapState)
                    return true; // on the map: never hold Retinues back
                LogOnce("newgame", "New game: skipped an AI clan tier change that came before Retinues was ready; Retinues builds those retinues after character creation. Logged once.");
                return false;
            }
            catch (Exception e)
            {
                LogOnce("newgame-error", "New-game guard failed (Retinues runs as usual): " + e.Message);
                return true;
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
