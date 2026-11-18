// Start-up warnings (v1.5.8, owner request 18 Nov 2026; plan docs\2026-11-18-plan-v1.5.8-startup-warnings.md):
//   A. Coop ticked while a single-player campaign map shows (no co-op session in this process): once per game start.
//   B. Limitless ticked together with Coop: once at the main menu, and once more when a co-op session starts.
// Never in Co-op's host server process (started with /server or /coopsave; Coop\CoopMod.cs NoHarmonyInit, bannerlord-1.5.x).
// A plain one-button inquiry (InquiryData / InformationManager.ShowInquiry, TaleWorlds.Library 1.5.7), shown only when no
// other inquiry is open. Reads only the active game state's type, the module list and CoopEnv: no hero, campaign or object
// access, so it is safe at the main menu. Any error logs once and turns the warnings off for this process.
// MCM "Show start-up warnings" (StartupWarnings, read at start-up) turns them off.
using System;
using TAOM_AIO_Compat.Settings;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM_AIO_Compat.Core
{
    internal static class StartupWarnings
    {
        internal const string SettingKey = "StartupWarnings";
        private const float SettleSeconds = 2f;
        private const float SessionCheckSeconds = 1f;
        private const string OffHint = "You can turn this warning off in Mod Options > TAOM AIO Compatibility Patch > Warnings > Show start-up warnings.";

        internal const string CoopTitle = "TAOM AIO: Co-op is ticked";
        internal const string CoopText =
            "You are playing single player with the Co-op mod ticked in the launcher. While Coop is ticked:\n" +
            "- TAOM uses the game's standard map bar instead of its own, and some TAOM features stay off.\n" +
            "- Co-op adds its own characters to the save (its team lords and wanderers). If you later load this save without Co-op, TAOM can show a \"repaired troops\" message.\n\n" +
            "For single player, untick Coop and TAOM.CoopCompat in the launcher, and keep separate saves for single player and co-op.\n\n" + OffHint;

        internal const string LimitlessTitle = "TAOM AIO: Limitless and Co-op";
        private const string LimitlessBody =
            "Limitless is a single-player mod. The AIO's Limitless switch does nothing in co-op, and Co-op requires every player to have the same modules ticked; the host's co-op server also loads the host's ticked modules.\n\n" +
            "Untick Limitless in the launcher before hosting or joining co-op.\n\n" + OffHint;
        internal const string LimitlessMenuText = "Limitless is ticked together with Co-op.\n\n" + LimitlessBody;
        internal const string LimitlessSessionText = "A co-op session started while Limitless is ticked.\n\n" + LimitlessBody;

        private static bool _decided, _enabled, _coopTicked, _limitlessTicked;
        private static bool _coopShown, _limitlessMenuShown, _limitlessSessionShown;
        private static Type _stateType;
        private static float _stateTime, _sessionCheckTimer;
        private static bool _sessionActive, _sessionSeen; // _sessionSeen: latch after a co-op session until the main menu shows without one
        private static float _noSessionTime;

        // Seams for the offline check.
        internal static Func<string[]> CommandLine = () => Environment.GetCommandLineArgs();
        internal static Func<bool> IsOn = () => StartupSettings.IsOn(SettingKey);
        internal static Func<string, bool> ModuleTicked = ModEnv.IsActive;
        internal static Func<bool> SessionActive = CoopEnv.IsSessionActive;
        internal static Func<Type> ActiveStateType = () => GameStateManager.Current?.ActiveState?.GetType();
        internal static Func<bool> InquiryOpen = InformationManager.IsAnyInquiryActive;
        internal static Func<bool> Loading = () => TaleWorlds.Engine.LoadingWindow.IsLoadingWindowActive;
        internal static Action<string, string> Show = ShowInquiry;

        internal static void Tick(float dt)
        {
            try
            {
                if (!_decided)
                    Decide();
                if (!_enabled)
                    return;

                Type state = ActiveStateType();
                if (state != _stateType || Loading())
                {
                    _stateType = state;
                    _stateTime = 0f; // the state must be shown, without a loading screen, for SettleSeconds
                }
                else
                    _stateTime += dt;

                _sessionCheckTimer -= dt;
                if (_sessionCheckTimer <= 0f)
                {
                    _sessionCheckTimer = SessionCheckSeconds;
                    _sessionActive = SessionActive();
                }
                if (_sessionActive)
                {
                    _sessionSeen = true;
                    _noSessionTime = 0f;
                }
                else
                    _noSessionTime += dt;

                if (state == null || _stateTime < SettleSeconds || InquiryOpen())
                    return;

                bool menu = typeof(TaleWorlds.MountAndBlade.InitialState).IsAssignableFrom(state);
                bool map = typeof(TaleWorlds.CampaignSystem.GameState.MapState).IsAssignableFrom(state);
                if (menu && !_sessionActive)
                    _sessionSeen = false; // back at the main menu after a co-op session (Co-op destroys its container before the menu shows)
                if (_limitlessTicked && !_limitlessSessionShown && _sessionActive)
                {
                    _limitlessSessionShown = true;
                    _limitlessMenuShown = true;
                    Present(LimitlessTitle, LimitlessSessionText);
                }
                else if (_limitlessTicked && !_limitlessMenuShown && !_sessionActive && menu)
                {
                    _limitlessMenuShown = true;
                    Present(LimitlessTitle, LimitlessMenuText);
                }
                else if (!_coopShown && !_sessionActive && !_sessionSeen && _noSessionTime >= SettleSeconds && map)
                {
                    _coopShown = true;
                    Present(CoopTitle, CoopText);
                }

                if (_coopShown && (!_limitlessTicked || _limitlessSessionShown))
                    _enabled = false; // nothing left to show in this process
            }
            catch (Exception e)
            {
                _enabled = false;
                _decided = true;
                AioLog.Write("[Warnings] Error (warnings off for this game start): " + e);
            }
        }

        private static void Decide()
        {
            _decided = true;
            _coopTicked = ModuleTicked("Coop");
            _limitlessTicked = ModuleTicked("Limitless");
            if (!_coopTicked)
                return; // both warnings need Coop ticked
            foreach (string a in CommandLine() ?? new string[0])
            {
                if (string.Equals(a, "/server", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/coopsave", StringComparison.OrdinalIgnoreCase))
                {
                    AioLog.Write("[Warnings] Co-op server process: no start-up warnings.");
                    return;
                }
            }
            if (!IsOn())
            {
                AioLog.Write("[Warnings] Start-up warnings are off in MCM.");
                return;
            }
            _enabled = true;
        }

        private static void Present(string title, string text)
        {
            AioLog.Write("[Warnings] " + title + ": " + text.Replace("\n", " "));
            Show(title, text);
        }

        private static void ShowInquiry(string title, string text) =>
            InformationManager.ShowInquiry(new InquiryData(title, text, true, false, "OK", null, () => { }, null), false, false);

        // Offline check only.
        internal static void ResetForTest()
        {
            _decided = _enabled = _coopTicked = _limitlessTicked = false;
            _coopShown = _limitlessMenuShown = _limitlessSessionShown = false;
            _stateType = null;
            _stateTime = _sessionCheckTimer = 0f;
            _sessionActive = _sessionSeen = false;
            _noSessionTime = 0f;
        }
    }
}
