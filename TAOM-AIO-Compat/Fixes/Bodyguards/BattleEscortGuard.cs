// Bodyguards switch, v1.4.0 (plan item 5, audit 14 Nov 2026): stops the once-a-second failed escort spawn in
// hideouts and in battles where the player is not the general.
//
// Why (Verified in the installed Bodyguards 1.2.13): BattleBodyguardsMissionBehavior.f() builds the "escort
// without formation". For every escort member that is not on the field yet it calls z (a companion) or A (a
// troop), and both name Mission.SpawnAgent(AgentBuildData, bool), which Bannerlord 1.5.7 no longer has. The
// first such call throws; f() catches it, writes "[Bodyguards] ERROR in CreaScortaSenzaFormazione" to the
// game log and gives up before it registers the escort members that ARE on the field. OnMissionTick then
// tries again every second for the whole mission.
//
// What: f() itself names no missing member, so it can be patched. A transpiler sends its one call to z and its
// one call to A to the two stubs below, which spawn nothing and return null (f() checks for null). f() then
// runs to its end without an exception and registers the escort members already on the field. A postfix sets
// Bodyguards' own field _didAutoCreate when a stub was reached, which ends the once-a-second retry; the
// bodyguard key still calls f(). The calls are matched by signature; if there is not exactly one of each,
// nothing is patched and one line is logged.
// No spawn is restored: z and A cannot be patched (Harmony must read their bodies), re-building them would tie
// this file to the obfuscated names of one build, and extra agents spawned on one machine do not belong in a
// co-op battle.
//
// When: at the first mission start, with the settlement guard of this switch (Mission.Current is set). Own
// Harmony id; removed by the switch's Uninstall.
// Co-op: host, client and single player behave the same. Nothing is spawned and no agent or campaign state is
// changed by this file; it only stops a failing call and a log line.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.Bodyguards
{
    internal static class BattleEscortGuard
    {
        private const string HarmonyId = "taom.compat.aio.bodyguards.escort";
        private const string AssemblyName = "Bodyguards";
        private const string BehaviorTypeName = "Bodyguards.BattleBodyguardsMissionBehavior";
        private const string OutfitTypeName = "Bodyguards.Core.EscortOutfit";
        private const string EscortMethodName = "f";
        private const string AutoCreateFieldName = "_didAutoCreate";

        private static Action<string> _log = _ => { };
        private static MethodInfo _escort;      // BattleBodyguardsMissionBehavior.f()
        private static MethodInfo _heroSpawn;   // z(Hero, int, Vec2, Vec2, EscortOutfit) : Agent
        private static MethodInfo _troopSpawn;  // A(CharacterObject, int, Vec2, Vec2) : Agent
        private static FieldInfo _didAutoCreate;
        private static bool _stubLogged, _errorLogged;
        [ThreadStatic] private static bool _stubHit;

        /// <summary>Looks the members up (no patching). False with one log line if this part cannot be applied.</summary>
        internal static bool Prepare(Action<string> log)
        {
            _log = log ?? (_ => { });
            try
            {
                Type type = ModEnv.FindType(AssemblyName, BehaviorTypeName);
                if (type == null)
                {
                    _log(BehaviorTypeName + " not found: the battle escort part is left off.");
                    return false;
                }
                const BindingFlags inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo[] methods = type.GetMethods(inst);
                MethodInfo[] escort = methods.Where(m => m.Name == EscortMethodName && m.ReturnType == typeof(void) && m.GetParameters().Length == 0).ToArray();
                MethodInfo[] hero = methods.Where(IsHeroSpawn).ToArray();
                MethodInfo[] troop = methods.Where(IsTroopSpawn).ToArray();
                FieldInfo field = type.GetField(AutoCreateFieldName, inst);
                if (escort.Length != 1 || hero.Length != 1 || troop.Length != 1 || field == null || field.FieldType != typeof(bool))
                {
                    _log("Bodyguards' escort methods were not found as expected (f(): " + escort.Length + ", hero spawn: " + hero.Length + ", troop spawn: "
                        + troop.Length + ", " + AutoCreateFieldName + ": " + (field != null) + "): the battle escort part is left off.");
                    return false;
                }
                _escort = escort[0];
                _heroSpawn = hero[0];
                _troopSpawn = troop[0];
                _didAutoCreate = field;
                return true;
            }
            catch (Exception e)
            {
                _log("Battle escort part: lookup failed, left off: " + e.Message);
                return false;
            }
        }

        private static bool IsHeroSpawn(MethodInfo m)
        {
            ParameterInfo[] p = m.GetParameters();
            return m.ReturnType == typeof(Agent) && p.Length == 5 && p[0].ParameterType == typeof(Hero) && p[1].ParameterType == typeof(int)
                && p[2].ParameterType == typeof(Vec2) && p[3].ParameterType == typeof(Vec2)
                && p[4].ParameterType.IsEnum && p[4].ParameterType.FullName == OutfitTypeName
                && Enum.GetUnderlyingType(p[4].ParameterType) == typeof(int);
        }

        private static bool IsTroopSpawn(MethodInfo m)
        {
            ParameterInfo[] p = m.GetParameters();
            return m.ReturnType == typeof(Agent) && p.Length == 4 && p[0].ParameterType == typeof(CharacterObject) && p[1].ParameterType == typeof(int)
                && p[2].ParameterType == typeof(Vec2) && p[3].ParameterType == typeof(Vec2);
        }

        /// <summary>First mission start, after Prepare() returned true. Never throws; false = not applied (logged).</summary>
        internal static bool Install()
        {
            if (_escort == null)
                return false;
            try
            {
                // Count first, so nothing is patched unless there is exactly one call of each.
                List<CodeInstruction> body = PatchProcessor.GetOriginalInstructions(_escort);
                int hero = body.Count(IsHeroCall), troop = body.Count(IsTroopCall);
                if (hero != 1 || troop != 1)
                {
                    _log("Bodyguards' f() calls its hero spawn " + hero + " time(s) and its troop spawn " + troop + " time(s), expected 1 and 1: nothing patched.");
                    return false;
                }
                const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                new Harmony(HarmonyId).Patch(_escort,
                    prefix: new HarmonyMethod(typeof(BattleEscortGuard).GetMethod(nameof(EscortPrefix), flags)),
                    postfix: new HarmonyMethod(typeof(BattleEscortGuard).GetMethod(nameof(EscortPostfix), flags)),
                    transpiler: new HarmonyMethod(typeof(BattleEscortGuard).GetMethod(nameof(EscortTranspiler), flags)));
                _log("Battle escort guard installed on " + BehaviorTypeName + ".f(): its two spawn calls (" + _heroSpawn.Name + ", " + _troopSpawn.Name
                    + ") no longer run, so the escort set-up finishes without the once-a-second error. Escort members that are not on the field are not spawned.");
                return true;
            }
            catch (Exception e)
            {
                Remove();
                _log("Battle escort guard could not be installed (ignored; the settlement guard is unchanged): " + e.Message);
                return false;
            }
        }

        /// <summary>Safe at any point; removes only this file's patches.</summary>
        internal static void Remove()
        {
            try { new Harmony(HarmonyId).UnpatchAll(HarmonyId); } catch { }
        }

        private static bool IsHeroCall(CodeInstruction i) =>
            (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m == _heroSpawn;

        private static bool IsTroopCall(CodeInstruction i) =>
            (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m == _troopSpawn;

        // Only opcode and operand change, so labels and exception blocks stay where they are. The stubs take
        // the same values from the stack: the instance, then the parameters (the enum as its int).
        private static IEnumerable<CodeInstruction> EscortTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = instructions.ToList();
            if (list.Count(IsHeroCall) != 1 || list.Count(IsTroopCall) != 1)
                return list; // checked before patching; if another transpiler changed the count, change nothing
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo heroStub = typeof(BattleEscortGuard).GetMethod(nameof(NoHeroSpawn), flags);
            MethodInfo troopStub = typeof(BattleEscortGuard).GetMethod(nameof(NoTroopSpawn), flags);
            foreach (CodeInstruction i in list)
            {
                if (IsHeroCall(i))
                {
                    i.opcode = OpCodes.Call;
                    i.operand = heroStub;
                }
                else if (IsTroopCall(i))
                {
                    i.opcode = OpCodes.Call;
                    i.operand = troopStub;
                }
            }
            return list;
        }

        // Stand-in for z(Hero, int, Vec2, Vec2, EscortOutfit): nothing is spawned.
        private static Agent NoHeroSpawn(object behavior, Hero hero, int index, Vec2 forward, Vec2 side, int outfit)
        {
            StubReached("companion " + SafeName(hero?.Name));
            return null;
        }

        // Stand-in for A(CharacterObject, int, Vec2, Vec2): nothing is spawned.
        private static Agent NoTroopSpawn(object behavior, CharacterObject troop, int index, Vec2 forward, Vec2 side)
        {
            StubReached("troop " + SafeName(troop?.Name));
            return null;
        }

        private static string SafeName(object name)
        {
            try { return name?.ToString() ?? "?"; }
            catch { return "?"; }
        }

        private static void StubReached(string who)
        {
            _stubHit = true;
            if (_stubLogged)
                return;
            _stubLogged = true;
            try
            {
                _log("Bodyguards wanted to spawn an escort member that is not on the field (" + who + "). That spawn cannot work on Bannerlord 1.5.7 and was left out "
                     + "(first time this session). Escort members already on the field are used; the automatic retry is stopped for this mission.");
            }
            catch { }
        }

        private static void EscortPrefix()
        {
            _stubHit = false;
        }

        // f() catches its own exceptions, so this always runs after it.
        private static void EscortPostfix(object __instance)
        {
            try
            {
                if (!_stubHit)
                    return;
                _stubHit = false;
                if (__instance != null && _didAutoCreate != null)
                    _didAutoCreate.SetValue(__instance, true);
            }
            catch (Exception e)
            {
                if (_errorLogged)
                    return;
                _errorLogged = true;
                try { _log("Battle escort guard: could not stop the automatic retry (ignored, logged once): " + e.Message); } catch { }
            }
        }
    }
}
