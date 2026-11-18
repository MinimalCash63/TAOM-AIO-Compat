// TAOM AIO Compatibility Patch (module id TAOM_AIO_Compat).
// Fixes for other mods running with TAOM 2.0.39 on Bannerlord 1.5.7 (and with the Co-op mod).
// One FixGroup per target mod, each behind its own MCM switch (Settings\AioSettings). A switch does
// nothing unless its mod is ticked. No file of any other mod is shipped or edited.
//
// Timing (Verified, TaleWorlds.MountAndBlade 1.5.7, Module.LoadSubModules / InitializeSubModuleBases):
// every module DLL is loaded and every SubModule constructor runs first, in load order; then every
// OnSubModuleLoad runs, in load order. An exception out of OnSubModuleLoad aborts the game, and one during
// new-game loading makes TAOM's loading step repeat forever, so nothing here may throw.
using System;
using System.Collections.Generic;
using System.Linq;
using TAOM_AIO_Compat.Core;
using TAOM_AIO_Compat.Fixes.Bodyguards;
using TAOM_AIO_Compat.Fixes.DismembermentPlus;
using TAOM_AIO_Compat.Fixes.DualWield;
using TAOM_AIO_Compat.Fixes.HoldTheLine;
using TAOM_AIO_Compat.Fixes.ImprovedGarrisons;
using TAOM_AIO_Compat.Fixes.Limitless;
using TAOM_AIO_Compat.Fixes.PartyQuality;
using TAOM_AIO_Compat.Fixes.RBM;
using TAOM_AIO_Compat.Fixes.TaomVoices;
using TAOM_AIO_Compat.Fixes.XorberaxLegacy;
using TAOM_AIO_Compat.Fixes.Retinues;
using TAOM_AIO_Compat.Fixes.RtsCommand;
using TAOM_AIO_Compat.Fixes.TaomEscapeMenu;
using TAOM_AIO_Compat.Settings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat
{
    public sealed class SubModule : MBSubModuleBase
    {
        private static readonly List<FixGroup> Groups = new List<FixGroup>();
        private static readonly List<FixGroup> Pending = new List<FixGroup>();
        private static bool _built;
        private static bool _menuShown;

        public SubModule()
        {
            try
            {
                BuildGroups();
                foreach (FixGroup g in Pending.ToList())
                {
                    if (g.EarlyInstallAssembly != null && ModEnv.FindAssembly(g.EarlyInstallAssembly) != null)
                        TryInstall(g, "constructor");
                }
            }
            catch (Exception e) { AioLog.Write("Constructor error (ignored): " + e); }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                BuildGroups();
                foreach (FixGroup g in Pending.ToList())
                    TryInstall(g, "OnSubModuleLoad");
            }
            catch (Exception e) { AioLog.Write("OnSubModuleLoad error (ignored): " + e); }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            ForEachInstalled("main menu", g => g.OnMainMenu());
            if (_menuShown)
                return;
            _menuShown = true;
            try
            {
                string[] names = Groups.Where(g => g.Installed && !g.Broken).Select(g => g.Name).ToArray();
                string text = names.Length == 0 ? "[TAOM AIO] No fixes active (none of the covered mods is ticked, or all switches are off)."
                                                : "[TAOM AIO] Fixes active: " + string.Join(", ", names) + ".";
                AioLog.Write(text);
                InformationManager.DisplayMessage(new InformationMessage(text));
            }
            catch { }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            ForEachInstalled("tick", g => g.OnApplicationTick(dt));
            try { StartupWarnings.Tick(dt); } catch { } // v1.5.8: Tick catches its own errors; this also covers a type-load failure
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            ForEachInstalled("mission start", g => g.OnBeforeMissionBehaviorInitialize(mission));
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            ForEachInstalled("game start", g => g.OnGameStart(game, gameStarterObject));
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            ForEachInstalled("game end", g => g.OnGameEnd(game));
        }

        protected override void OnSubModuleUnloaded()
        {
            ForEachInstalled("unload", g => g.Uninstall());
            base.OnSubModuleUnloaded();
        }

        // ---- groups ----

        private static void BuildGroups()
        {
            if (_built)
                return;
            _built = true;
            AioLog.Write("---- TAOM AIO Compatibility Patch " + typeof(SubModule).Assembly.GetName().Version + " starting ----");
            var all = new FixGroup[]
            {
                new RetinuesFixes(),
                new RbmFixes(),
                new HoldTheLineFixes(),
                new BodyguardsFixes(),
                new TaomVoicesFixes(),
                new XorberaxLegacyFixes(),
                new ImprovedGarrisonsFixes(),
                new DualWieldFixes(),
                new RtsCommandFixes(),
                new DismembermentPlusFixes(),
                new PartyQualityFixes(),
                new LimitlessFixes(),
                new TaomEscapeMenuFixes(),
            };
            foreach (FixGroup g in all)
            {
                g.Log = AioLog.For(g.Name);
                Groups.Add(g);
                bool on = StartupSettings.IsOn(g.SettingKey);
                bool active = ModEnv.IsActive(g.TargetModuleId);
                if (!active)
                    g.Log(g.TargetModuleId + " is not ticked. Nothing applied.");
                else if (g.ReplacesModuleId != null && ModEnv.IsActive(g.ReplacesModuleId))
                    g.Log("Old fix module " + g.ReplacesModuleId + " is still ticked; it does this job. Nothing applied here. Untick it to use this switch.");
                else if (on)
                    Pending.Add(g);
                else
                    g.Log("Switch is off in MCM. Nothing applied (owner rule 13 Nov 2026: off means every part of this switch is off)."
                          + (g.OffWarning == null ? "" : " WARNING: " + g.OffWarning));
            }
        }

        private static void TryInstall(FixGroup g, string when)
        {
            Pending.Remove(g);
            try
            {
                g.Installed = g.Install();
                g.Log(g.Installed ? "Installed (" + when + ")." : "Not installed (" + when + "), see the lines above.");
                if (!g.Installed)
                    SafeUninstall(g); // remove any part that did get applied
            }
            catch (Exception e)
            {
                g.Installed = false;
                g.Log("Install failed (" + when + "); its patches are removed again: " + e);
                SafeUninstall(g);
            }
        }

        private static void SafeUninstall(FixGroup g)
        {
            try { g.Uninstall(); }
            catch (Exception e) { g.Log("Uninstall error (ignored): " + e.Message); }
        }

        private static void ForEachInstalled(string when, Action<FixGroup> action)
        {
            for (int i = 0; i < Groups.Count; i++)
            {
                FixGroup g = Groups[i];
                if (!g.Installed || g.Broken)
                    continue;
                try { action(g); }
                catch (Exception e)
                {
                    g.Broken = true;
                    g.Log("Error at " + when + "; this switch gets no further events this session (Harmony patches already applied stay): " + e);
                }
            }
        }
    }
}
