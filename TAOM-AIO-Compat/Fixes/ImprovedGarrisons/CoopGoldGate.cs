// Improved Garrisons switch, v1.4.0 (plan item 4, audit 14 Nov 2026): IG's garrison costs follow Co-op's gold gate.
//
// What and why (Verified in the installed Coop build of 14 Nov 2026): Co-op's
// DefaultClanFinanceModelPatches.CalculateClanGoldChangePrefix skips
// DefaultClanFinanceModel.CalculateClanGoldChange on days it does not want a clan's gold to change (leader in
// a settlement or a battle, or disconnected, depending on Co-op's options). AioIgClanFinanceModel then still
// added IG's cost lines to the empty result, so IG's costs were charged and IG's counters were reset on a day
// Co-op meant to charge nothing.
//
// How: a postfix on DefaultClanFinanceModel.CalculateClanGoldChange(Clan, bool, bool, bool) notes whether the
// game's own method ran for this call (Harmony's __runOriginal: false when any prefix skipped it). The wrapper
// clears the note, calls the model below it (TAOM's, which calls the game's method once) and leaves IG's lines
// out when the game's method was reached but skipped. It follows what Co-op decided for that very call and
// reads no Co-op name.
//
// When: applied in OnGameStart of a campaign, after the wrapper was registered (the wrapper derives from
// DefaultClanFinanceModel, so that class's static constructor, which needs Game.Current.GameTextManager, has
// run by then). Removed at game end and by Uninstall. Own Harmony id, so the switch's other patches stay.
// If the postfix cannot be applied, the wrapper behaves as in v1.3.0.
//
// Co-op: host: IG's lines are added only on days Co-op lets the clan's gold change. Client: the same test
// runs for the client's own tooltip. Single player (with or without Coop ticked): no prefix skips the game's
// method, so nothing changes.
using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM_AIO_Compat.Fixes.ImprovedGarrisons
{
    internal static class CoopGoldGate
    {
        private const string HarmonyId = "taom.compat.aio.improvedgarrisons.goldgate";

        [ThreadStatic] private static bool _seen; // the game's CalculateClanGoldChange was reached in this call
        [ThreadStatic] private static bool _ran;  // ... and its body ran (no prefix skipped it)

        private static bool _installed;
        private static bool _skipLogged;

        internal static bool Installed => _installed;

        /// <summary>Applies the postfix. Returns false (reason logged) if it could not be applied; nothing throws.</summary>
        internal static bool Install(Action<string> log)
        {
            if (_installed)
                return true;
            try
            {
                MethodInfo target = AccessTools.DeclaredMethod(typeof(DefaultClanFinanceModel), "CalculateClanGoldChange",
                    new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) });
                MethodInfo postfix = typeof(CoopGoldGate).GetMethod(nameof(BasePostfix), BindingFlags.Static | BindingFlags.NonPublic);
                if (target == null || target.IsStatic || target.ReturnType != typeof(ExplainedNumber) || postfix == null)
                {
                    log?.Invoke("DefaultClanFinanceModel.CalculateClanGoldChange(Clan, bool, bool, bool) not found: IG's cost lines do not follow Co-op's gold gate (as in v1.3.0).");
                    return false;
                }
                new Harmony(HarmonyId).Patch(target, postfix: new HarmonyMethod(postfix) { priority = Priority.Last });
                _installed = true;
                log?.Invoke("Gold gate check installed on DefaultClanFinanceModel.CalculateClanGoldChange: IG's cost lines are left out when another mod (Co-op) skips the clan's daily gold change.");
                return true;
            }
            catch (Exception e)
            {
                Remove();
                log?.Invoke("Gold gate check not installed (IG's cost lines are added as in v1.3.0): " + e.Message);
                return false;
            }
        }

        /// <summary>Safe at any point; removes only this file's postfix.</summary>
        internal static void Remove()
        {
            try { new Harmony(HarmonyId).UnpatchAll(HarmonyId); } catch { }
            _installed = false;
            _skipLogged = false;
        }

        /// <summary>The wrapper calls this right before it asks the model below it.</summary>
        internal static void Begin()
        {
            _seen = false;
            _ran = false;
        }

        /// <summary>
        /// True when the game's CalculateClanGoldChange was reached since Begin() and a prefix skipped it.
        /// False when the check is not installed or the model below never reached the game's method.
        /// </summary>
        internal static bool BaseWasSkipped(Action<string> log)
        {
            if (!_installed || !_seen || _ran)
                return false;
            if (!_skipLogged)
            {
                _skipLogged = true;
                try { log?.Invoke("First gated gold change this game: the clan's daily gold change was skipped by another mod (Co-op's gold gate), so IG's garrison cost lines were left out for that call."); } catch { }
            }
            return true;
        }

        // Runs after every prefix and the original (or instead of it, when a prefix skipped it).
        private static void BasePostfix(bool __runOriginal)
        {
            _seen = true;
            _ran = __runOriginal;
        }
    }
}
