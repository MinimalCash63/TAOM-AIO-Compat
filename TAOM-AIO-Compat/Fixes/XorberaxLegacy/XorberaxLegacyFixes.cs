// Xorberax's Legacy switch: TAOM's cleave creatures keep TAOM's own remaining momentum after a hit.
// Rewrite of the TAOM Xorberax patch (Xorberax-TAOM-2.0.29.5, by Theb0yys; permission logged 13 Nov 2026) for
// Bannerlord 1.5.7 / TAOM 2.0.39 / Xorberax's Legacy 1.0.8.8 (by TheSchwarz1). No file of either mod is changed.
//
// Why: in Mission.MeleeHitCallback the game sets inOutMomentumRemaining from the damage model. TAOM's two models
// (TaomCombatMechanicsModel, TaomCustomBattleDamageModel) return CombatMechanicsHooks.CleaveMomentum(...) unchanged
// when it has a value: 30% of the momentum for the creatures in TAOM's combat_mechanics_config.json (trolls,
// mumakil, Sauron). Xorberax's "Cut Through Everyone" postfix on MeleeHitCallback then overwrites that value
// with its own (damage share x "Damage Retained Per Cut"). This group records TAOM's value during the hit and
// writes it back after Xorberax's postfix. Normal troops are never touched: TAOM returns no value for them.
//
// Patches (Harmony id below, all removed by Uninstall):
//   Mission.MeleeHitCallback: prefix (opens a per-hit frame), postfix ordered after "XorberaxLegacy" (restores),
//                             finalizer (closes the frame; void, so an exception is never swallowed).
//   TAOM CombatMechanicsHooks.CleaveMomentum: postfix (records TAOM's value in the open frame).
// Installed at the first main menu, after Xorberax's own OnSubModuleLoad PatchAll, so its postfix can be checked.
// Co-op: acts only where the original MeleeHitCallback ran (Coop skips it for attackers another player controls).
//
// v1.4.0 (plan item 3, audit 14 Nov 2026): TAOM's creatures are also kept out of Xorberax's charge multiplier and
// Deadly Combat, and Deadly Combat leaves agents alone that this machine does not own in co-op. That code, its
// reasons and its co-op behaviour are in XorberaxCreatureRules.cs. It is added only after the cleave patches
// above went in, each part in its own try/catch on the same Harmony id: a failure there changes nothing here.
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.XorberaxLegacy
{
    internal sealed class XorberaxLegacyFixes : FixGroup
    {
        internal override string SettingKey => "XorberaxLegacyFixes";
        internal override string Name => "Xorberax's Legacy";
        internal override string TargetModuleId => "XorberaxLegacy";

        private const string HarmonyId = "taom.compat.aio.xorberaxlegacy";
        private const string XorberaxHarmonyId = "XorberaxLegacy";
        private const string XorberaxAssembly = "XorberaxLegacy";
        private const string XorberaxPatchType = "XorberaxLegacy.Patches.CutThroughEveryonePatchMeleeHit";
        private const string XorberaxPostfixName = "MeleeHitCallbackPostfix";
        private const string TaomAssembly = "TAOM";
        private const string TaomHooksType = "TAOM.Features.CombatMechanics.Hooks.CombatMechanicsHooks";
        private const string OldModuleId = "TAOM_XorberaxCompatibility";
        private static readonly string[] TaomModelTypes =
        {
            "TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel",
            "TAOM.Features.CombatMechanics.Models.TaomCustomBattleDamageModel",
        };

        private static MethodInfo _meleeHit;   // Mission.MeleeHitCallback
        private static MethodInfo _cleave;     // CombatMechanicsHooks.CleaveMomentum
        private static MethodInfo _xorPostfix; // Xorberax's MeleeHitCallbackPostfix
        private static Action<string> _log;
        private static bool _restoreLogged, _errorLogged;
        // v1.4.0 addendum 9a: co-op client guards on the bank (XorberaxCoopGuards.cs), installed once a game is running.
        private readonly LateInstall _coopGuards = new LateInstall("Co-op bank guards", XorberaxCoopGuards.TryInstall, XorberaxCoopGuards.Uninstall);
        private bool _menuDone;
        private bool _patched;
        private bool _deadlyTried; // v1.4.0: Deadly Combat prefixes, one attempt at the first mission start

        // ---- install ----

        // OnSubModuleLoad: only checks that every member exists with the expected shape. Nothing is patched here.
        internal override bool Install()
        {
            _log = Log;
            Log("Xorberax's Legacy module " + (ModEnv.ModuleVersion(TargetModuleId) ?? "?") + ", TAOM " + (ModEnv.ModuleVersion("TAOM") ?? "?") + ".");

            _meleeHit = FindMeleeHitCallback();
            if (_meleeHit == null)
                return false;

            Type hooks = ModEnv.FindType(TaomAssembly, TaomHooksType);
            MethodInfo cleave = hooks == null ? null : AccessTools.DeclaredMethod(hooks, "CleaveMomentum");
            ParameterInfo[] cp = cleave?.GetParameters();
            if (cleave == null || cleave.IsStatic || cleave.ReturnType != typeof(float?) || cp.Length != 3
                || cp[0].ParameterType != typeof(Agent) || cp[0].Name != "attacker"
                || cp[1].ParameterType != typeof(float)
                || cp[2].ParameterType != typeof(AttackCollisionData).MakeByRefType())
            {
                Log(TaomHooksType + ".CleaveMomentum(Agent attacker, float, in AttackCollisionData) returning float? not found. Nothing changed.");
                return false;
            }
            _cleave = cleave;
            foreach (string t in TaomModelTypes)
            {
                Type mt = ModEnv.FindType(TaomAssembly, t);
                bool has = mt != null && AccessTools.DeclaredMethod(mt, "CalculateRemainingMomentum") != null;
                Log(t + ".CalculateRemainingMomentum " + (has ? "found." : "NOT found (its battles get no cleave value to keep)."));
            }

            Type xp = ModEnv.FindType(XorberaxAssembly, XorberaxPatchType);
            MethodInfo xm = xp?.GetMethod(XorberaxPostfixName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            ParameterInfo[] xpar = xm?.GetParameters();
            if (xm == null || xpar.Length != 11 || xpar[4].ParameterType != typeof(float).MakeByRefType())
            {
                Log(XorberaxPatchType + "." + XorberaxPostfixName + " (11 parameters, ref float momentum) not found. Nothing changed.");
                return false;
            }
            _xorPostfix = xm;
            Log("Shape checks passed. Patches are applied at the main menu.");
            return true;
        }

        private MethodInfo FindMeleeHitCallback()
        {
            MethodInfo m;
            try { m = AccessTools.DeclaredMethod(typeof(Mission), "MeleeHitCallback"); }
            catch (AmbiguousMatchException) { m = null; }
            ParameterInfo[] p = m?.GetParameters();
            if (m == null || m.IsStatic || p.Length != 11
                || p[1].ParameterType != typeof(Agent) || p[1].Name != "attacker"
                || p[4].ParameterType != typeof(float).MakeByRefType() || p[4].Name != "inOutMomentumRemaining")
            {
                Log("Mission.MeleeHitCallback with 11 parameters (Agent attacker, ref float inOutMomentumRemaining) not found. Nothing changed.");
                return null;
            }
            return m;
        }

        // First main menu: Xorberax's PatchAll (its OnSubModuleLoad) has run by now.
        internal override void OnGameStart(TaleWorlds.Core.Game game, TaleWorlds.Core.IGameStarter starter) => _coopGuards.Try(m => Log("[co-op] " + m));

        internal override void OnMainMenu()
        {
            _coopGuards.Try(m => Log("[co-op] " + m));
            if (_menuDone)
                return;
            _menuDone = true;

            if (ModEnv.IsActive(OldModuleId))
                Log("Old module Xorberax-TAOM-2.0.29.5 (" + OldModuleId + ") is still ticked. It patches nothing on this game build; please untick it.");

            bool xorberaxPresent = false;
            try
            {
                Patches info = Harmony.GetPatchInfo(_meleeHit);
                xorberaxPresent = info != null && info.Postfixes.Any(p => p.owner == XorberaxHarmonyId
                    && (p.PatchMethod == _xorPostfix
                        || (p.PatchMethod.Name == XorberaxPostfixName && p.PatchMethod.DeclaringType?.FullName == XorberaxPatchType)));
            }
            catch (Exception e) { Log("Could not read the patches on MeleeHitCallback: " + e.Message); }
            if (!xorberaxPresent)
            {
                Log("Xorberax's cut-through postfix is not on Mission.MeleeHitCallback, so there is nothing to fix. Nothing changed.");
                Broken = true;
                return;
            }

            try
            {
                var h = new Harmony(HarmonyId);
                h.Patch(_cleave, postfix: Hook(nameof(CleavePostfix), Priority.Normal));
                HarmonyMethod post = Hook(nameof(HitPostfix), Priority.Last);
                post.after = new[] { XorberaxHarmonyId };
                h.Patch(_meleeHit, prefix: Hook(nameof(HitPrefix), Priority.First), postfix: post,
                        finalizer: Hook(nameof(HitFinalizer), Priority.Normal));
                _patched = true;
                Log("Patched: postfix on CombatMechanicsHooks.CleaveMomentum; prefix, finalizer and postfix (after "
                    + XorberaxHarmonyId + ") on Mission.MeleeHitCallback. TAOM creatures keep TAOM's momentum after a hit.");
            }
            catch (Exception e)
            {
                Log("Patching failed; this switch's patches are removed again: " + e);
                Uninstall();
                Broken = true;
                return;
            }

            // v1.4.0, 3a: charge multiplier. Never throws; a failure is one log line.
            try
            {
                XorberaxCreatureRules.Init(Log);
                Log(XorberaxCreatureRules.InstallCharge(new Harmony(HarmonyId)));
            }
            catch (Exception e) { Log("Charge part not installed (ignored): " + e.Message); }
        }

        private static HarmonyMethod Hook(string name, int priority) =>
            new HarmonyMethod(typeof(XorberaxLegacyFixes).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)) { priority = priority };

        // Mission start runs on the main thread between hits: a frame left open would mean a missed finalizer.
        internal override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            _coopGuards.Try(m => Log("[co-op] " + m));
            if (_patched && _depth != 0)
            {
                Log("Per-hit frame count was " + _depth + " at mission start; reset (logged once per mission).");
                ResetFrames();
            }
            if (!_patched)
                return;
            // v1.4.0: TAOM's creature list is asked again each mission; 3b and 3c go in at the first mission
            // (Mission.Current is set; DeadlyCombatMissionLogic's static initializer reads MCM and an action index).
            try
            {
                XorberaxCreatureRules.OnMissionStart();
                if (!_deadlyTried && Mission.Current != null)
                {
                    _deadlyTried = true;
                    Log(XorberaxCreatureRules.InstallDeadlyCombat(new Harmony(HarmonyId)));
                }
            }
            catch (Exception e) { Log("Deadly Combat part not installed (ignored): " + e.Message); }
        }

        // Safe at any point (also after a failed or partial install): removes only this switch's own patches.
        internal override void Uninstall()
        {
            _coopGuards.Uninstall();
            try { new Harmony(HarmonyId).UnpatchAll(HarmonyId); }
            catch (Exception e) { _log?.Invoke("Unpatch error (ignored): " + e.Message); }
            _patched = false;
            ResetFrames();
            try { XorberaxCreatureRules.Reset(); } catch { }
        }

        // ---- per-hit frames (one stack per thread; MeleeHitCallback can be re-entered, e.g. DualWield calls it) ----

        private struct Frame
        {
            internal Agent Attacker;
            internal bool Has;
            internal float Value;
        }

        private const int MaxDepth = 8;
        [ThreadStatic] private static Frame[] _frames;
        [ThreadStatic] private static int _depth;

        private static void ResetFrames()
        {
            _depth = 0;
            if (_frames != null)
                Array.Clear(_frames, 0, _frames.Length);
        }

        // Runs first: opens this hit's frame. __state = frame depth (0 = no frame).
        private static void HitPrefix(Agent attacker, out int __state)
        {
            __state = 0;
            try
            {
                if (_frames == null)
                    _frames = new Frame[MaxDepth];
                int d = ++_depth;
                __state = d;
                if (d <= MaxDepth)
                    _frames[d - 1] = new Frame { Attacker = attacker };
            }
            catch (Exception e) { LogErrorOnce("prefix", e); }
        }

        // TAOM's cleave value for this hit's attacker (null = not a cleave creature, or TAOM's cleave is off).
        // The last call in the hit wins, as in the game (UpdateMomentumRemaining may run twice per hit).
        private static void CleavePostfix(Agent attacker, float? __result)
        {
            try
            {
                int d = _depth;
                if (d <= 0 || d > MaxDepth || _frames == null || !ReferenceEquals(_frames[d - 1].Attacker, attacker))
                    return;
                bool ok = __result.HasValue && !float.IsNaN(__result.Value) && !float.IsInfinity(__result.Value);
                _frames[d - 1].Has = ok;
                _frames[d - 1].Value = ok ? __result.Value : 0f;
            }
            catch (Exception e) { LogErrorOnce("cleave postfix", e); }
        }

        // Runs after Xorberax's postfix: puts TAOM's value back. Only when the game's own code ran for this hit.
        private static void HitPostfix(int __state, bool __runOriginal, Agent attacker, ref float inOutMomentumRemaining)
        {
            try
            {
                if (!__runOriginal || __state <= 0 || __state > MaxDepth || __state != _depth || _frames == null)
                    return;
                Frame f = _frames[__state - 1];
                if (!f.Has || inOutMomentumRemaining == f.Value)
                    return;
                if (!_restoreLogged)
                {
                    _restoreLogged = true;
                    _log?.Invoke("First cleave value kept this session: " + (attacker?.Name ?? "?") + ", momentum "
                                 + inOutMomentumRemaining.ToString("0.###") + " (Xorberax) -> " + f.Value.ToString("0.###") + " (TAOM).");
                }
                inOutMomentumRemaining = f.Value;
            }
            catch (Exception e) { LogErrorOnce("postfix", e); }
        }

        // Closes this hit's frame (and any deeper one left open). Void: the original exception, if any, is rethrown.
        private static void HitFinalizer(int __state)
        {
            try
            {
                if (__state <= 0 || _depth < __state)
                    return;
                if (_frames != null)
                    for (int i = __state - 1; i < Math.Min(_depth, MaxDepth); i++)
                        _frames[i] = default;
                _depth = __state - 1;
            }
            catch (Exception e) { LogErrorOnce("finalizer", e); }
        }

        private static void LogErrorOnce(string where, Exception e)
        {
            if (_errorLogged)
                return;
            _errorLogged = true;
            try { _log?.Invoke("Error in " + where + " (ignored, logged once): " + e.Message); } catch { }
        }
    }
}
