// Dismemberment Plus switch (new in v1.4.0): keeps Dismemberment (Plus) 2.0.8.8 by TheSchwarz1 to the victims
// it can handle under TAOM, and takes away its Windows message box and, in co-op battles, its slow motion.
// The mod's files are not changed; everything here is found by name at run time and patched in memory.
//
// Evidence (audit and design of 14 Nov 2026, installed DismembermentPlus.dll, decompiled):
//   - The mod has no Harmony patches. Its mission behaviour DismembermentPlusMissionLogic.OnRegisterBlow rebuilds
//     a killed victim on "human_skeleton" from vanilla human skin meshes. Its only race gate is Agent.IsHuman,
//     which every TAOM race passes (LOTRLOME_Armory monsters.xml: IsHumanoid on all 14, the human race and 13 others, elves included: code review 14 Nov 2026). Elves, orcs, uruks, goblins
//     and the others on the human skeleton would get human skin and a human head; dwarves and hill trolls
//     have their own skeletons.
//   - The catch block of DismembermentPlusMissionLogic.Dismember calls System.Windows.Forms.MessageBox.Show(string)
//     on the game's main thread: the game stops until the box is closed, and in co-op the session stalls.
//   - The validator runs outside that try/catch, so an error there leaves Mission.RegisterBlow.
//   - CheckSetSlowMotion adds a local 0.25x time speed request when the player dismembers someone. Co-op does
//     not patch mission time speed, so only that one machine would slow down.
//
// What this group does:
//   P1  OnRegisterBlow: prefix by race (v1.5.7, DpRaceHeads.cs): men, female elves and female Nazgul go through
//       the mod as before; the head-cut-only races only for a neck or head hit on a skeleton with DP's bone
//       numbers; every other humanoid victim (cave and hill trolls, Sauron, unknown races) is skipped.
//       Finalizer logs an error once and swallows it.
//   P4  Dismember (v1.5.7): prefix; for a head-cut-only race the AIO builds the head cut from the victim's own
//       meshes and skeleton and DP's method is skipped (it would rebuild a human body). Patched before P2.
//   P2  Dismember: the MessageBox.Show(string) call is replaced by a method that writes the text to the AIO log
//       once per session. Call not found: nothing is changed, one log line.
//   P3  CheckSetSlowMotion: skipped while a co-op battle runs. Installed only when Coop is ticked.
//
// Installed: Install() only looks the methods up. The patches are applied once, at the first mission start
// (OnBeforeMissionBehaviorInitialize): the logic class's static initializer reads the mod's MCM settings, and
// patching compiles the method, so it is not done at load.
//
// Co-op: the mod only changes what a corpse looks like on the machine where its own OnRegisterBlow saw the
// kill; it never kills, removes or changes a blow, so there is nothing to desync. P1 and P2 behave the same on
// host, client and in single player. P3 acts on host and client during a co-op battle and never in single
// player without Coop ticked (with Coop ticked and no co-op battle running, slow motion stays).
// Players do not need the same value of this switch.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.DismembermentPlus
{
    internal sealed class DismembermentPlusFixes : FixGroup
    {
        internal override string SettingKey => "DismembermentPlusFixes";
        internal override string Name => "Dismemberment Plus";
        internal override string TargetModuleId => "DismembermentPlus";

        internal override string OffWarning =>
            "Dismemberment Plus then also acts on orcs, uruks, trolls and dwarves with human body parts, and any error in it stops the game behind a Windows message box (in co-op the session stalls). Untick DismembermentPlus in the launcher as well, to be safe.";

        private const string HarmonyId = "taom.compat.aio.dismembermentplus";
        private const string AssemblyName = "DismembermentPlus";
        private const string LogicTypeName = "DismembermentPlus.Logic.DismembermentPlusMissionLogic";
        private const string DismembermentInterfaceName = "DismembermentPlus.Dismemberment.IDismemberment";
        private const string MessageBoxTypeName = "System.Windows.Forms.MessageBox";

        private static Action<string> _log;
        private static bool _raceErrorLogged, _blowErrorLogged, _boxLogged, _slowMotionErrorLogged, _slowMotionLogged, _dismemberErrorLogged;
        private static int _replacedCalls;
        private static System.Reflection.PropertyInfo _victimProperty, _collisionProperty;

        private Harmony _harmony;
        private MethodInfo _onRegisterBlow, _dismember, _checkSetSlowMotion;
        private bool _patchTried;

        internal override bool Install()
        {
            _log = Log;
            DpRaceHeads.Init(Log);
            Type logic = ModEnv.FindType(AssemblyName, LogicTypeName);
            if (logic == null)
            {
                Log(LogicTypeName + " not found. Nothing changed.");
                return false;
            }
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            _onRegisterBlow = FindMethod(logic, "OnRegisterBlow", flags, 6, null);
            _dismember = FindMethod(logic, "Dismember", flags, 1, DismembermentInterfaceName);
            _checkSetSlowMotion = FindMethod(logic, "CheckSetSlowMotion", flags, 1, DismembermentInterfaceName);
            if (_onRegisterBlow == null)
                Log(LogicTypeName + ".OnRegisterBlow (6 parameters) not found: the race rule and the error guard stay off.");
            if (_dismember == null)
                Log(LogicTypeName + ".Dismember(IDismemberment) not found: the message box stays, and no race gets head cuts.");
            else
            {
                Type face = _dismember.GetParameters()[0].ParameterType; // IDismemberment
                _victimProperty = face.GetProperty("Victim");
                _collisionProperty = face.GetProperty("CollisionData");
                if (_victimProperty?.PropertyType != typeof(Agent) || _collisionProperty?.PropertyType != typeof(AttackCollisionData))
                {
                    _victimProperty = _collisionProperty = null;
                    Log(DismembermentInterfaceName + ".Victim / CollisionData not found: no race gets head cuts (men, female elves and female Nazgul are dismembered as before).");
                }
            }
            if (_checkSetSlowMotion == null)
                Log(LogicTypeName + ".CheckSetSlowMotion(IDismemberment) not found: slow motion is not changed.");
            if (_onRegisterBlow == null && _dismember == null && _checkSetSlowMotion == null)
                return false;
            Log("Ready; the patches are applied at the first mission start (not at load).");
            return true;
        }

        internal override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            if (_patchTried || Mission.Current == null)
                return;
            _patchTried = true; // one attempt per session
            var h = new Harmony(HarmonyId);
            _harmony = h;
            int applied = 0;

            if (_onRegisterBlow != null)
            {
                try
                {
                    h.Patch(_onRegisterBlow,
                        prefix: new HarmonyMethod(Own(nameof(RegisterBlowPrefix))),
                        finalizer: new HarmonyMethod(Own(nameof(RegisterBlowFinalizer))));
                    applied++;
                    Log("Race rule and error guard installed on " + LogicTypeName + ".OnRegisterBlow (men, female elves and female Nazgul: all cuts; other TAOM races: head cuts only; trolls, Sauron and unknown races: none).");
                }
                catch (Exception e) { Log("Race rule and error guard could not be installed: " + e.Message); }
            }

            // P4 before P2: Harmony runs P2's transpiler again on every later patch of the same method.
            if (_dismember != null && _victimProperty != null && _onRegisterBlow != null)
            {
                try
                {
                    h.Patch(_dismember, prefix: new HarmonyMethod(Own(nameof(DismemberPrefix))));
                    applied++;
                    Log("Head cuts for TAOM races installed on " + LogicTypeName + ".Dismember (built from each race's own head and skeleton).");
                }
                catch (Exception e)
                {
                    _victimProperty = null; // P1 then lets no head-cut-only race through
                    Log("Head cuts for TAOM races could not be installed (those races stay whole): " + e.Message);
                }
            }
            else
            {
                _victimProperty = null;
            }

            if (_dismember != null)
            {
                try
                {
                    _replacedCalls = 0;
                    h.Patch(_dismember, transpiler: new HarmonyMethod(Own(nameof(DismemberTranspiler))));
                    if (_replacedCalls == 1)
                    {
                        applied++;
                        Log("Message box in " + LogicTypeName + ".Dismember replaced by a log line.");
                    }
                    else
                    {
                        Log("Expected one MessageBox.Show(string) call in " + LogicTypeName + ".Dismember, found " + _replacedCalls + ". The method was left as it is.");
                    }
                }
                catch (Exception e) { Log("Message box replacement could not be installed: " + e.Message); }
            }

            if (_checkSetSlowMotion != null)
            {
                if (!CoopEnv.IsCoopTicked)
                {
                    Log("Slow motion guard: Coop is not ticked, not installed.");
                }
                else
                {
                    try
                    {
                        h.Patch(_checkSetSlowMotion, prefix: new HarmonyMethod(Own(nameof(SlowMotionPrefix))));
                        applied++;
                        Log("Slow motion guard installed on " + LogicTypeName + ".CheckSetSlowMotion (Coop is ticked; acts in co-op battles).");
                    }
                    catch (Exception e) { Log("Slow motion guard could not be installed: " + e.Message); }
                }
            }

            if (applied == 0)
                Log("No patch was applied at the first mission start. Dismemberment Plus runs unchanged.");
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _victimProperty = _collisionProperty = null;
            DpRaceHeads.Reset();
        }

        private static MethodInfo Own(string name) =>
            typeof(DismembermentPlusFixes).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>The one declared method with that name and parameter count; the first parameter's type is matched by full name when given.</summary>
        private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, int parameterCount, string firstParameterType)
        {
            try
            {
                MethodInfo found = null;
                foreach (MethodInfo m in type.GetMethods(flags))
                {
                    if (m.Name != name)
                        continue;
                    ParameterInfo[] p = m.GetParameters();
                    if (p.Length != parameterCount)
                        continue;
                    if (firstParameterType != null && p[0].ParameterType.FullName != firstParameterType)
                        continue;
                    if (found != null)
                        return null; // ambiguous: do not guess
                    found = m;
                }
                return found;
            }
            catch { return null; }
        }

        // ---- P1 ----------------------------------------------------------------------------------------------

        // __1 is the victim, __4 the collision data (OnRegisterBlow(Agent attacker, Agent victim, WeakGameEntity,
        // Blow, ref AttackCollisionData, in MissionWeapon)). False skips the mod for this blow; the base
        // MissionBehavior.OnRegisterBlow it would have called is empty.
        private static bool RegisterBlowPrefix(Agent __1, ref AttackCollisionData __4)
        {
            try
            {
                if (__1 == null || !__1.IsHuman)
                    return true; // the mod rejects these itself
                switch (DpRaceHeads.ModeOf(__1))
                {
                    case DpRaceMode.DpPath:
                        return true;
                    case DpRaceMode.HeadOnly:
                        return _victimProperty != null && DpRaceHeads.IsHeadBone(__4.CollisionBoneIndex) && DpRaceHeads.HasHumanHeadBones(__1);
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                if (!_raceErrorLogged)
                {
                    _raceErrorLogged = true;
                    try { _log?.Invoke("Race check failed (logged once); the blow is skipped for Dismemberment Plus: " + e.Message); } catch { }
                }
                return false;
            }
        }

        private static Exception RegisterBlowFinalizer(Exception __exception)
        {
            if (__exception != null && !_blowErrorLogged)
            {
                _blowErrorLogged = true;
                try { _log?.Invoke("Dismemberment Plus error in OnRegisterBlow (caught, logged once): " + __exception); } catch { }
            }
            return null; // swallow: Mission.RegisterBlow has no try/catch
        }

        // ---- P4 ----------------------------------------------------------------------------------------------

        // __0 is the IDismemberment. True runs DP's Dismember (DP path races only); a head-cut-only race gets the
        // AIO head cut and DP's method is skipped, also when the head cut fails (DP would rebuild a human body).
        private static bool DismemberPrefix(object __0)
        {
            try
            {
                Agent victim = _victimProperty?.GetValue(__0, null) as Agent;
                if (victim == null)
                    return true; // as before: DP's own handling
                DpRaceMode mode = DpRaceHeads.ModeOf(victim);
                if (mode == DpRaceMode.DpPath)
                    return true;
                // Code review: also checked here, in case P1 is not on (then DP lets every hit through).
                if (mode == DpRaceMode.HeadOnly && _collisionProperty?.GetValue(__0, null) is AttackCollisionData collision
                    && DpRaceHeads.IsHeadBone(collision.CollisionBoneIndex))
                    DpRaceHeads.HeadCut(victim, collision);
                return false;
            }
            catch (Exception e)
            {
                if (!_dismemberErrorLogged)
                {
                    _dismemberErrorLogged = true;
                    try { _log?.Invoke("Head cut check failed (logged once); that victim is left as it is: " + e.Message); } catch { }
                }
                return false;
            }
        }

        // ---- P2 ----------------------------------------------------------------------------------------------

        // Stands in for System.Windows.Forms.MessageBox.Show(string). That method returns a DialogResult, an
        // int32 enum, which the mod pops; this returns an int so the stack stays the same. 1 = DialogResult.OK.
        private static int LogInsteadOfBox(string text)
        {
            try
            {
                if (!_boxLogged)
                {
                    _boxLogged = true;
                    _log?.Invoke("Dismemberment Plus error (its message box was replaced by this line; logged once per session; that victim was not dismembered): " + text);
                }
            }
            catch { }
            return 1;
        }

        private static IEnumerable<CodeInstruction> DismemberTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            try
            {
                MethodInfo replacement = Own(nameof(LogInsteadOfBox));
                var indexes = new List<int>();
                for (int i = 0; i < original.Count; i++)
                {
                    if (original[i].opcode != OpCodes.Call || !(original[i].operand is MethodInfo called))
                        continue;
                    if (called.Name != "Show" || called.DeclaringType == null || called.DeclaringType.FullName != MessageBoxTypeName)
                        continue;
                    ParameterInfo[] p = called.GetParameters();
                    if (p.Length == 1 && p[0].ParameterType == typeof(string))
                        indexes.Add(i);
                }
                _replacedCalls = indexes.Count;
                if (replacement == null || indexes.Count != 1)
                {
                    if (replacement == null)
                        _replacedCalls = 0;
                    return original; // unchanged
                }
                original[indexes[0]].operand = replacement; // same instruction object: labels and blocks stay
                return original;
            }
            catch
            {
                _replacedCalls = 0;
                return original;
            }
        }

        // ---- P3 ----------------------------------------------------------------------------------------------

        private static bool SlowMotionPrefix()
        {
            try
            {
                if (!CoopEnv.IsCoopBattleActive())
                    return true;
                if (!_slowMotionLogged)
                {
                    _slowMotionLogged = true;
                    try { _log?.Invoke("Co-op battle: Dismemberment Plus slow motion is skipped (logged once)."); } catch { }
                }
                return false;
            }
            catch (Exception e)
            {
                if (!_slowMotionErrorLogged)
                {
                    _slowMotionErrorLogged = true;
                    try { _log?.Invoke("Slow motion check failed (logged once); slow motion skipped: " + e.Message); } catch { }
                }
                return false;
            }
        }
    }
}
