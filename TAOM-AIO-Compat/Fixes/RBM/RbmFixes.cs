// RBM switch: everything for Realistic Battle Mod 4.8.1 (Marnah93 and Philozoraptor) under TAOM 2.0.39 (ported on 2.0.38, re-checked on 2.0.39 on 14 Nov 2026).
//  1. The TAOM team's RBM patch (TAOM_Standalone_Compat_RBM v1.0.11, "RBM-TAOM-2.0.29.5", by Theb0yys),
//     copied with permission (owner, 13 Nov 2026) and updated for Bannerlord 1.5.7 / TAOM 2.0.38 / RBM 4.8.1.
//     Three parts, each with its own Harmony id:
//      - content (taom.compat.aio.rbm.content), installed here at OnSubModuleLoad, before any item XML loads:
//        Armory availability fixes, RBM balance for TAOM items and blade pieces, RBM ammo format for TAOM
//        arrows and bolts, Armory XSLT re-apply, prize-source ids (Content\);
//      - campaign (taom.compat.aio.rbm.campaign), at the main menu / game start once RBM's tournament code is
//        loaded: grain price, TAOM recruit price, RBM tournament troops and prizes (Campaign\);
//      - battle (taom.compat.aio.rbm.battle), at the first mission where all 34 RBM patch classes resolve:
//        crash-guard finalizers on 91 RBM patch methods + Blow + tournament tier, archer range flag (Battle\).
//     The old lookup-rename fix (RBMAI.OverrideFormation -> RBMAI.Frontline+OverrideFormation in the old
//     module's memory) is gone: the corrected name is in Battle\BattlePatches.cs.
//  2. Changelog frame: TAOM replaces Native's Frame1Brush with a bright parchment under RBM's light text.
//     When RBM's Changelog window is open, its frame gets this module's copy of the native brush (unchanged
//     from RBM-TAOM-157Fix v1.1.0).
// Switch off: nothing of this runs (owner rule, 13 Nov 2026). Old module TAOM_Standalone_Compat_RBM still
// ticked: only part 2 runs, so nothing is patched twice; the log and a main-menu message say to untick it.
// Plan and review: AIO-Compat-Patch\docs\2026-11-13-plan-rbm-patch-port.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TAOM_AIO_Compat.Fixes.RBM.Battle;
using TAOM_AIO_Compat.Fixes.RBM.Campaign;
using TAOM_AIO_Compat.Fixes.RBM.Content;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace TAOM_AIO_Compat.Fixes.RBM
{
    internal sealed class RbmFixes : FixGroup
    {
        internal override string SettingKey => "RbmFixes";
        internal override string Name => "RBM";
        internal override string TargetModuleId => "RBM";
        internal override string ReplacesModuleId => "TAOM_RBM_157Fix";

        internal override string OffWarning =>
            "RBM then runs without TAOM's RBM balance: TAOM items keep their own stats under RBM's damage model (TAOM arrows and bolts do almost no damage with RBM combat on), "
            + "recruit and grain prices come from RBM, RBM tournaments use only the town culture's troops and RBM's prizes, RBM's battle code runs without the extra crash guards, "
            + "and RBM's Changelog window is hard to read. No crash or loading loop is known from this. Also untick RBM-TAOM-2.0.29.5 (TAOM_Standalone_Compat_RBM), or it runs its own older copy. "
            + "For balanced play turn this switch on, or turn RBM combat off in RBM's options, or untick RBM.";

        private const string OldPatchModuleId = "TAOM_Standalone_Compat_RBM";
        private const string OldPatchAssemblyName = "RBM_CompatPatch";
        private const string ContentHarmonyId = "taom.compat.aio.rbm.content";
        private const string CampaignHarmonyId = "taom.compat.aio.rbm.campaign";
        private const string BattleHarmonyId = "taom.compat.aio.rbm.battle";

        private bool _legacy;          // old module ticked: Changelog frame only
        private bool _legacyNoteShown;
        private bool _portActive;      // content part installed; campaign and battle parts may follow
        private bool _campaignFinished;
        private bool _campaignWaitLogged;
        private bool _battleFinished;
        private bool _mercGuardFinished;
        // v1.4.0 addendum 9d: on a co-op client RBM's caravan dispatch does not run (Campaign\CaravanCoopGuard.cs).
        private readonly LateInstall _caravanGuard = new LateInstall("Co-op caravan guard", CaravanCoopGuard.TryInstall, CaravanCoopGuard.Uninstall);
        private bool _prizePoolFinished;
        private bool _battleWaitLogged;
        private Harmony _contentHarmony;
        private Harmony _campaignHarmony;
        private Harmony _battleHarmony;

        internal override bool Install()
        {
            RbmLog.Write = Log;
            Log("RBM module " + (ModEnv.ModuleVersion(TargetModuleId) ?? "?") + ".");
            _legacy = ModEnv.IsActive(OldPatchModuleId) || ModEnv.FindAssembly(OldPatchAssemblyName) != null;
            if (_legacy)
            {
                Log("Old TAOM RBM patch (" + OldPatchModuleId + ", RBM-TAOM-2.0.29.5) is ticked: the AIO copy of it is not applied, only the Changelog frame fix. "
                    + "The old module's battle part stays off on RBM 4.8.1 and 4.8.2 (one RBM class moved). Untick it to use the AIO copy.");
                return true;
            }
            try
            {
                InstallContent();
                _portActive = true;
            }
            catch (Exception e)
            {
                Log("TAOM RBM balance (content part) not installed; campaign and battle parts stay off too, only the Changelog frame fix runs: " + e);
                Unpatch(ref _contentHarmony, ContentHarmonyId);
            }
            return true; // the Changelog frame fix works either way
        }

        internal override void OnMainMenu()
        {
            if (_legacy)
            {
                if (!_legacyNoteShown)
                {
                    _legacyNoteShown = true;
                    try
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "[TAOM AIO] RBM: the old RBM-TAOM-2.0.29.5 module is still ticked, so only the Changelog fix is active. Untick it to use the AIO's copy."));
                    }
                    catch { }
                }
                return;
            }
            if (!_portActive)
                return;
            if (!RbmConfig.Resolve())
                Log("Main menu: RBMConfig.RBMConfig is not loaded yet; it is looked up again when items load.");
            TryInstallCampaign("main menu");
            TryInstallMercenaryGuard("main menu");
            TryInstallPrizePool("main menu");
            _caravanGuard.Try(m => Log("[co-op] " + m));
        }

        internal override void OnGameStart(Game game, IGameStarter starter)
        {
            if (!_portActive)
                return;
            PrizeSelection.Clear();
            MercenaryGuard.Reset();
            TryInstallCampaign("game start");
            TryInstallMercenaryGuard("game start");
            TryInstallPrizePool("game start");
            _caravanGuard.Try(m => Log("[co-op] " + m));
        }

        internal override void OnGameEnd(Game game)
        {
            if (_portActive)
            {
                PrizeSelection.Clear();
                MercenaryGuard.Reset();
            }
        }

        internal override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            if (!_portActive)
                return;
            TryInstallCampaign("mission start");
            TryInstallMercenaryGuard("mission start");
            TryInstallPrizePool("mission start");
            _caravanGuard.Try(m => Log("[co-op] " + m));
            TryInstallBattle();
        }

        internal override void OnApplicationTick(float dt)
        {
            if (!_changelogFixOff)
                FixChangelogFrame();
        }

        internal override void Uninstall()
        {
            _caravanGuard.Uninstall();
            Unpatch(ref _battleHarmony, BattleHarmonyId);
            Unpatch(ref _campaignHarmony, CampaignHarmonyId);
            try { new Harmony(CampaignHarmonyId).UnpatchAll(CampaignHarmonyId); } catch { } // the co-op guard, if the campaign part never installed
            Unpatch(ref _contentHarmony, ContentHarmonyId);
            PrizeSelection.Clear();
            MercenaryGuard.Reset();
        }

        // ---- 1. Ported TAOM RBM patch ----

        private void InstallContent()
        {
            ItemAvailability.Load(RbmResources.Load(RbmResources.AvailabilityFile));
            EquipmentBalance.Load(RbmResources.Load(RbmResources.EquipmentBalanceFile));
            Log("Balance tables loaded: " + ItemAvailability.Count + " availability fixes, " + EquipmentBalance.ItemCount + " item profiles, "
                + EquipmentBalance.PieceCount + " blade pieces.");
            _contentHarmony = new Harmony(ContentHarmonyId);
            Apply(_contentHarmony, typeof(ContentRepairs));
            Apply(_contentHarmony, typeof(EquipmentMerge));
            Apply(_contentHarmony, typeof(PrizeSources));
            Log("Content part installed (item balance, ammo format, Armory transforms, prize sources).");
        }

        private void TryInstallCampaign(string when)
        {
            if (_campaignFinished)
                return;
            try
            {
                if (!CampaignTargets.Prepare())
                {
                    if (!_campaignWaitLogged)
                    {
                        _campaignWaitLogged = true;
                        Log(when + ": RBM's tournament code (" + CampaignTargets.TournamentPatchType + ") is not loaded yet; campaign part retried later.");
                    }
                    return;
                }
                _campaignHarmony = new Harmony(CampaignHarmonyId);
                Apply(_campaignHarmony, typeof(GrainPrice));
                Apply(_campaignHarmony, typeof(RecruitmentPrice));
                Apply(_campaignHarmony, typeof(TournamentParticipants));
                Apply(_campaignHarmony, typeof(TournamentPrizes));
                Apply(_campaignHarmony, typeof(StoredTournamentPrize));
                _campaignFinished = true;
                Log(when + ": campaign part installed (grain price, TAOM recruit price, tournament troops and prizes).");
            }
            catch (Exception e)
            {
                _campaignFinished = true;
                Unpatch(ref _campaignHarmony, CampaignHarmonyId);
                _mercGuardFinished = false; // same Harmony id: a co-op guard installed earlier was removed too; it is put back at the next event
                _prizePoolFinished = true;  // the TAOM prize pool filter belongs to the campaign part: off with it
                Log(when + ": campaign part not installed, stays off this session (its patches are removed): " + e);
            }
        }

        // v1.3.0: co-op guard on RBM's mercenary companies (Campaign\MercenaryGuard.cs). Only with Coop ticked.
        // Own try/catch and its own Harmony instance on the campaign id, so a failure here never takes the
        // campaign part down and Unpatch(campaign id) removes it with the rest.
        private void TryInstallMercenaryGuard(string when)
        {
            if (_mercGuardFinished)
                return;
            try
            {
                if (!ModEnv.IsActive("Coop"))
                {
                    _mercGuardFinished = true;
                    Log("Co-op guard for RBM's mercenary companies: Coop is not ticked, not installed.");
                    return;
                }
                MethodInfo target = MercenaryGuard.FindTarget();
                if (target == null)
                    return; // RBMCampaign.dll not loaded yet; retried at the next event
                MercenaryGuard.Install(new Harmony(CampaignHarmonyId), target);
                _mercGuardFinished = true;
                Log(when + ": co-op guard installed on " + MercenaryGuard.TypeName + "." + MercenaryGuard.MethodName + " (Coop is ticked).");
            }
            catch (Exception e)
            {
                _mercGuardFinished = true;
                Log(when + ": co-op guard for RBM's mercenary companies not installed (ignored): " + e.Message);
            }
        }

        // v1.4.0 (plan 2b): filter on TAOM's tournament prize pool (Campaign\CampaignPatches.cs, TaomPrizePool).
        // Optional: own try/catch and its own Harmony instance on the campaign id, like the co-op guard above,
        // so a failure here never takes the campaign part down and Unpatch(campaign id) removes it with the rest.
        // Only once the campaign part is in: it holds the prize postfixes this filter belongs to.
        private void TryInstallPrizePool(string when)
        {
            if (_prizePoolFinished || !_campaignFinished || _campaignHarmony == null)
                return;
            try
            {
                MethodInfo target = TaomPrizePool.FindTarget();
                if (target == null)
                {
                    _prizePoolFinished = true;
                    Log(when + ": TAOM.dll is not loaded; TAOM prize pool filter not installed.");
                    return;
                }
                TaomPrizePool.Install(new Harmony(CampaignHarmonyId), target);
                _prizePoolFinished = true;
                Log(when + ": TAOM prize pool filter installed on " + TaomPrizePool.TypeName + "." + TaomPrizePool.MethodName
                    + " (a prize picked through TAOM's choice is no longer replaced).");
            }
            catch (Exception e)
            {
                _prizePoolFinished = true;
                Log(when + ": TAOM prize pool filter not installed (ignored; the rest of the campaign part is unchanged): " + e.Message);
            }
        }

        private static readonly string[] BattleAssemblies = { "RBM", "RBMAI", "RBMCombat", "RBMTournament" };

        private void TryInstallBattle()
        {
            if (_battleFinished || Mission.Current == null)
                return;
            try
            {
                if (!TargetResolver.TryInitialize(out string problem))
                {
                    bool allLoaded = true;
                    foreach (string name in BattleAssemblies)
                        allLoaded &= ModEnv.FindAssembly(name) != null;
                    if (allLoaded)
                    {
                        _battleFinished = true;
                        Log(problem + " All RBM battle assemblies are loaded, so this will not change: battle part stays off this session.");
                    }
                    else if (!_battleWaitLogged)
                    {
                        _battleWaitLogged = true;
                        Log(problem + " Not every RBM battle assembly is loaded yet; retried at each mission start.");
                    }
                    return;
                }
                _battleHarmony = new Harmony(BattleHarmonyId);
                Apply(_battleHarmony, typeof(PatchBoolFinalizer));
                Apply(_battleHarmony, typeof(PatchVoidFinalizer));
                Apply(_battleHarmony, typeof(BlowFinalizer));
                Apply(_battleHarmony, typeof(TournamentTierFinalizer));
                Apply(_battleHarmony, typeof(ArcherRange));
                _battleFinished = true;
                Log("Battle part installed: crash guards on " + TargetResolver.BoolCount + " bool and " + TargetResolver.VoidCount
                    + " void RBM patch methods, RBM Blow and tournament tier; archer range flag.");
            }
            catch (Exception e)
            {
                _battleFinished = true;
                Unpatch(ref _battleHarmony, BattleHarmonyId);
                Log("Battle part not installed, stays off this session (its patches are removed): " + e);
            }
        }

        private static void Apply(Harmony harmony, Type patchType)
        {
            List<MethodInfo> patched = harmony.CreateClassProcessor(patchType).Patch();
            if (patched == null || patched.Count == 0)
                throw new InvalidOperationException("No hooks installed for " + patchType.FullName);
        }

        private void Unpatch(ref Harmony harmony, string id)
        {
            if (harmony == null)
                return;
            try { harmony.UnpatchAll(id); } // only this id's patches
            catch (Exception e) { Log("Removing " + id + " failed (ignored): " + e.Message); }
            harmony = null;
        }

        // ---- 2. Changelog window frame ----

        // Layer and movie names from RBM 4.8.1 (RBMConfig\RBMConfigUI\Changelog\RBMChangelog.cs, OpenViewer).
        private const string ChangelogLayerName = "RBMChangelog";
        private const string ChangelogMovieName = "RBMChangelog";
        private const string TaomFrameBrushName = "Frame1Brush";
        private const string ReadableFrameBrushName = "TAOM_AIO.RBM.Changelog.Frame"; // GUI\Brushes\TAOM_AIO_Compat.xml

        // Root widget of the viewer already handled; cleared as soon as it is gone so it is not kept alive.
        private Widget _handledChangelogRoot;
        private bool _changelogFixOff;
        private bool _changelogLogged;

        private void FixChangelogFrame()
        {
            try
            {
                ScreenBase top = ScreenManager.TopScreen;
                GauntletLayer layer = top is MBInitialScreenBase ? top.FindLayer<GauntletLayer>(ChangelogLayerName) : null;
                GauntletMovieIdentifier id = layer == null ? null : layer.GetMovieIdentifier(ChangelogMovieName);
                Widget root = id == null || id.Movie == null ? null : id.Movie.RootWidget;
                if (root == null)
                {
                    _handledChangelogRoot = null;
                    return;
                }
                if (root == _handledChangelogRoot)
                    return;
                _handledChangelogRoot = root;

                Brush readable = UIResourceManager.BrushFactory.GetBrush(ReadableFrameBrushName);
                if (readable == null)
                {
                    _changelogFixOff = true;
                    Log("Changelog frame: brush " + ReadableFrameBrushName + " not found. Nothing changed.");
                    return;
                }
                int count = SwapBrush(root, readable);
                if (!_changelogLogged)
                {
                    _changelogLogged = true;
                    Log(count > 0
                        ? "Changelog frame: " + count + " widget(s) set to " + ReadableFrameBrushName + "."
                        : "Changelog frame: WARNING, no widget with " + TaomFrameBrushName + " found in RBM's Changelog window. Nothing changed.");
                }
            }
            catch (Exception ex)
            {
                _changelogFixOff = true;
                _handledChangelogRoot = null;
                Log("Changelog frame fix failed, switched off for this session: " + ex);
            }
        }

        private static int SwapBrush(Widget widget, Brush readable)
        {
            int count = 0;
            BrushWidget brushWidget = widget as BrushWidget;
            if (brushWidget != null && IsFrom(brushWidget.ReadOnlyBrush, TaomFrameBrushName))
            {
                brushWidget.Brush = readable;
                count++;
            }
            for (int i = 0; i < widget.ChildCount; i++)
                count += SwapBrush(widget.GetChild(i), readable);
            return count;
        }

        // A widget's brush is often a clone named "Frame1Brush(Clone)"; follow ClonedFrom back to the original.
        private static bool IsFrom(Brush brush, string name)
        {
            for (int guard = 0; brush != null && guard < 8; guard++, brush = brush.ClonedFrom)
            {
                if (brush.Name == name)
                    return true;
            }
            return false;
        }
    }
}
