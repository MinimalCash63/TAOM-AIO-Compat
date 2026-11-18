// Bodyguards switch: stops Bodyguards 1.2.13 from crashing a town, castle or village visit on 1.5.7.
// Its settlement escort code calls Mission.SpawnAgent(AgentBuildData, bool), which 1.5.7 replaced by a
// four-parameter version. The calling methods fail at their first call, from
// BodyguardsSettlementBehavior.OnMissionTick, which (like the game's mission tick) has no try/catch.
// That tick only runs the settlement escort logic. This group catches the error there, logs it once and
// skips that tick for the rest of the session. Settlement escorts then do not appear (they cannot work on
// 1.5.7 anyway); battle bodyguards are unchanged (Bodyguards catches that error itself).
// v1.4.0 (plan item 5, audit 14 Nov 2026): the battle side of the same missing call (hideouts, and battles
// where the player is not the general) is handled in BattleEscortGuard.cs. It is applied after this guard, in
// its own try/catch and on its own Harmony id; if it cannot be applied, this guard is unchanged.
using System;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.Bodyguards
{
    internal sealed class BodyguardsFixes : FixGroup
    {
        internal override string SettingKey => "BodyguardsFixes";
        internal override string Name => "Bodyguards";
        internal override string TargetModuleId => "Bodyguards";

        internal override string OffWarning =>
            "Bodyguards can then crash the game when you enter a town, castle or village. Untick Bodyguards in the launcher as well.";

        private const string HarmonyId = "taom.compat.aio.bodyguards";
        private const string BehaviorTypeName = "Bodyguards.BodyguardsSettlementBehavior";

        private static Action<string> _log;
        private static volatile bool _settlementTickOff;
        private static bool _otherLogged;
        private Harmony _harmony;
        private MethodInfo _tick, _prefix, _finalizer;
        private bool _patchTried;
        private bool _escortReady, _escortTried; // v1.4.0: BattleEscortGuard

        // v1.2.2 (13 Nov 2026): the patch is applied at the first mission start, not at load. Compiling the
        // patched OnMissionTick runs TaleWorlds.MountAndBlade.MovementOrder's static initializer (beforefieldinit;
        // OnMissionTick reads MovementOrder.MovementOrderCharge), which needs Mission.Current. At load it threw
        // and .NET cached the failure, so every later battle failed (Team.Initialize / Formation.Reset).
        // OnBeforeMissionBehaviorInitialize runs from Mission.AfterStart: Mission.Current is set and no
        // behaviour has ticked yet (decompiled 1.5.7; research\2026-11-13-cctor-risk-scan.md).
        internal override bool Install()
        {
            _log = Log;
            Type t = ModEnv.FindType("Bodyguards", BehaviorTypeName);
            _tick = t == null ? null : AccessTools.DeclaredMethod(t, "OnMissionTick", new[] { typeof(float) });
            _prefix = typeof(BodyguardsFixes).GetMethod(nameof(TickPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            _finalizer = typeof(BodyguardsFixes).GetMethod(nameof(TickFinalizer), BindingFlags.Static | BindingFlags.NonPublic);
            if (_tick == null || _prefix == null || _finalizer == null)
            {
                Log(BehaviorTypeName + ".OnMissionTick(float) not found. Nothing changed.");
                return false;
            }
            Log("Crash guard ready; it is applied at the first mission start (not at load, see v1.2.2 note).");
            try { _escortReady = BattleEscortGuard.Prepare(Log); }
            catch (Exception e) { Log("Battle escort part: left off (" + e.Message + ")."); }
            return true;
        }

        internal override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            if (Mission.Current == null)
                return;
            InstallSettlementGuard();
            // v1.4.0: battle escort guard, one attempt per session, only while the settlement guard is in.
            if (_escortReady && !_escortTried && !Broken)
            {
                _escortTried = true;
                try { BattleEscortGuard.Install(); }
                catch (Exception e) { Log("Battle escort guard not installed (ignored): " + e.Message); }
            }
        }

        private void InstallSettlementGuard()
        {
            if (_patchTried)
                return;
            _patchTried = true; // one attempt per session
            try
            {
                var h = new Harmony(HarmonyId);
                _harmony = h;
                h.Patch(_tick, prefix: new HarmonyMethod(_prefix), finalizer: new HarmonyMethod(_finalizer));
                Log("Crash guard on " + BehaviorTypeName + ".OnMissionTick installed (first mission).");
            }
            catch (Exception e)
            {
                Broken = true;
                Uninstall(); // remove anything half-applied
                Log("Crash guard could not be installed; switched off for this session: " + e.Message);
            }
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            BattleEscortGuard.Remove();
        }

        private static bool TickPrefix() => !_settlementTickOff;

        private static Exception TickFinalizer(Exception __exception)
        {
            if (__exception == null)
                return null;
            // A missing game member (1.5.7 API change) fails again on every call: stop calling that tick.
            // Anything else: swallow and log once, keep the tick.
            bool apiGone = __exception is MissingMemberException || __exception is TypeLoadException;
            if (apiGone && !_settlementTickOff)
            {
                _settlementTickOff = true;
                try
                {
                    _log?.Invoke("Bodyguards' settlement escort code failed (" + __exception.GetType().Name + ": " + __exception.Message
                                 + "). Caught; settlement escorts are off for this session. Battle bodyguards are not affected.");
                }
                catch { }
            }
            else if (!apiGone && !_otherLogged)
            {
                _otherLogged = true;
                try { _log?.Invoke("Bodyguards' settlement tick error (caught, logged once): " + __exception); } catch { }
            }
            return null; // swallow: the game's mission tick has no try/catch
        }
    }
}
