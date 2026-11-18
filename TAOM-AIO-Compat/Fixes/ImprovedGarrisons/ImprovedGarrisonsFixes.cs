// Improved Garrisons switch (IG 4.2.0.7, a 1.4.8 build) on Bannerlord 1.5.7 with TAOM 2.0.39.
// Plan: docs\2026-11-13-plan-improved-garrisons.md (accepted 13 Nov 2026 with changes).
//
// Rule behind every part (Verified): an IG method whose IL names a member 1.5.7 no longer has fails when it is
// compiled, and Harmony cannot patch it (reading its body fails). So the CALLERS are transpiled: each call to a
// broken IG method is replaced by a call to our own helper (IgHelpers). The callers' own IL is clean.
//
// Parts:
// 1. Banner trackers: PartyManager.TrackPartyWithBanner / UntrackPartyWithBanner name the gone type
//    MapTrackerProvider. Their 4 callers are redirected to helpers that use 1.5.7's MapTrackerManager.
//    Without this, InitializeNewParty creates a party and then returns null (orphan party).
// 2. Party size: PartyManager.GetPartySizeLimit calls the gone GetPartyMemberSizeLimit(PartyBase,bool).
//    Its 11 callers (12 call sites) are redirected. Installed ONLY if part 1 is confirmed in InitializeNewParty,
//    so recruitment never runs without working party creation.
// 3. The two "specify upgrade path" dialogs call TrainingSettings.SpecifyUpgradePath (broken); their two
//    callers get a chat line instead of IG's error message.
// 4. Models: IG's Main.AddModels is skipped entirely (size, speed, food and cost models), so TAOM's models stay.
//    A clan finance wrapper (TAOM's model first, then IG's own garrison cost lines) is registered at
//    OnGameStart; see AioIgClanFinanceModel and IgHelpers.
//    v1.4.0 (plan item 4, audit 14 Nov 2026): IG's cost lines follow Co-op's gold gate; see CoopGoldGate.cs
//    (postfix on DefaultClanFinanceModel.CalculateClanGoldChange, applied in OnGameStart after the wrapper,
//    removed at game end; own try/catch, a failure leaves the wrapper as it was in v1.3.0).
//
// Call-site counts per method come from a Mono.Cecil scan of the installed ImprovedGarrisons.dll
// (13 Nov 2026): only direct call/callvirt; no ldftn/ldvirtftn delegate references and no reflection strings
// name GetPartySizeLimit, TrackPartyWithBanner, UntrackPartyWithBanner or SpecifyUpgradePath. The transpiler
// also counts delegate references at run time and logs expected vs found per method.
//
// v1.5.2 (18 Nov 2026): IG 4.3.1.0 is a 1.5.7 build with its own fixes for parts 1-3 (TrackPartyWithBanner returns
// void there, bool in 4.2.0.7). Parts 1-3 are then skipped (part 1 would unbalance the stack); part 4 and the co-op
// guards stay. PatchOne refuses any replacement whose helper and target return types differ.
//
// Off = Install is never called: nothing of this file is applied.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM_AIO_Compat.Fixes.ImprovedGarrisons
{
    internal sealed class ImprovedGarrisonsFixes : FixGroup
    {
        internal override string SettingKey => "ImprovedGarrisonsFixes";
        internal override string Name => "Improved Garrisons";
        internal override string TargetModuleId => "ImprovedGarrisons";

        internal override string OffWarning =>
            "With Improved Garrisons 4.3.1.0 or newer, its own clan finance model and (with its default options) its party size model then replace TAOM's, plus its speed and food models if those are switched on in its options, and its hourly and daily routines also run on a co-op client. " +
            "With the old 4.2.0.7 it runs unpatched on Bannerlord 1.5.7: garrison recruitment does not work, its error message repeats, " +
            "and two of its clicks can crash the game (Reset settings -> Yes in its garrison window, and closing the party screen of one of its recruiter parties). " +
            "To be safe, untick ImprovedGarrisons in the launcher as well.";

        private const string HarmonyId = "taom.compat.aio.improvedgarrisons";
        private const string AssemblyName = "ImprovedGarrisons";
        private const string PartyManagerType = "ImprovedGarrisons.AI.AIManagers.PartyManager";
        private const string TrainingSettingsType = "ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager.TrainingSettings";
        private const string MainType = "ImprovedGarrisons.Main";
        private const string GarrisonPartyBehaviorType = "ImprovedGarrisons.Behaviours.GarrisonPartyBehavior";

        private Harmony _harmony;
        private bool _modelsSkipped;
        // v1.4.0 addendum 9c: on a co-op client IG's hourly and daily routines do not run (IgCoopGuards.cs).
        private readonly LateInstall _coopGuards = new LateInstall("Co-op client guards", IgCoopGuards.TryInstall, IgCoopGuards.Uninstall);

        // ---- transpiler bookkeeping (keyed by the caller's metadata token; all callers are in IG's module) ----

        private sealed class Replacement
        {
            internal MethodInfo Target;
            internal MethodInfo Helper;
            internal int Expected;
            internal int Found;
            internal int DelegateRefs;
        }

        private static readonly Dictionary<int, List<Replacement>> Specs = new Dictionary<int, List<Replacement>>();
        private static Module _igModule;

        internal override bool Install()
        {
            Assembly asm = ModEnv.FindAssembly(AssemblyName);
            if (asm == null)
            {
                Log("ImprovedGarrisons.dll is not loaded. Nothing changed.");
                return false;
            }
            _igModule = asm.ManifestModule;
            Log("Improved Garrisons " + (ModEnv.ModuleVersion(TargetModuleId) ?? "(version unknown)") + " found; this switch was checked against v4.2.0.7 and v4.3.1.0.");

            Type partyManager = asm.GetType(PartyManagerType, false);
            Type training = asm.GetType(TrainingSettingsType, false);
            Type main = asm.GetType(MainType, false);
            if (partyManager == null || main == null)
            {
                Log("IG types " + PartyManagerType + " / " + MainType + " not found. Nothing changed.");
                return false;
            }

            MethodInfo getSize = AccessTools.DeclaredMethod(partyManager, "GetPartySizeLimit", new[] { typeof(PartyBase) });
            MethodInfo track = AccessTools.DeclaredMethod(partyManager, "TrackPartyWithBanner", new[] { typeof(MobileParty) });
            MethodInfo untrack = AccessTools.DeclaredMethod(partyManager, "UntrackPartyWithBanner", new[] { typeof(MobileParty) });
            MethodInfo specify = training == null ? null : AccessTools.DeclaredMethod(training, "SpecifyUpgradePath", new[] { typeof(bool) });

            IgHelpers.Init(asm, Log);
            _harmony = new Harmony(HarmonyId);
            int patched = 0;

            // v1.5.2: IG 4.3.1.0 and newer fix parts 1-3 themselves (TrackPartyWithBanner returns void there).
            bool ownFixes = track != null && track.ReturnType == typeof(void);
            if (ownFixes)
                Log("This Improved Garrisons has its own Bannerlord 1.5.7 fixes for banner trackers, party size and the upgrade-path dialog: "
                    + "parts 1-3 are not needed and not installed. TAOM's models, IG's costs on top and the co-op guards still apply "
                    + "(if the model part below fails, the co-op guards are not installed either).");

            // 1. Banner trackers. InitializeNewParty decides whether the size part may go in.
            bool bannerConfirmed = false;
            if (ownFixes) { }
            else if (track == null || untrack == null)
                Log("PartyManager.TrackPartyWithBanner / UntrackPartyWithBanner not found: banner part skipped.");
            else
            {
                Replacement[] Both() => new[] { R(track, nameof(IgHelpers.TrackPartyWithBanner), 1), R(untrack, nameof(IgHelpers.UntrackPartyWithBanner), 1) };
                bannerConfirmed = PatchCallers(partyManager, "InitializeNewParty", Both, ref patched);
                PatchCallers(partyManager, "TrackAllImprovedGarrisonparties", () => new[] { R(track, nameof(IgHelpers.TrackPartyWithBanner), 1) }, ref patched);
                PatchCallers(partyManager, "UntrackAllImprovedGarrisonparties", () => new[] { R(untrack, nameof(IgHelpers.UntrackPartyWithBanner), 1) }, ref patched);
                PatchCallers(asm.GetType(GarrisonPartyBehaviorType, false), "OnPartyRemoved", () => new[] { R(untrack, nameof(IgHelpers.UntrackPartyWithBanner), 3) }, ref patched);
            }

            // 2. Party size, only with confirmed banner part.
            if (ownFixes) { }
            else if (!bannerConfirmed)
                Log("Banner part not confirmed in PartyManager.InitializeNewParty: the party size part is NOT installed. Garrison recruitment stays off (as without this switch), so no orphan parties.");
            else if (getSize == null)
                Log("PartyManager.GetPartySizeLimit(PartyBase) not found: party size part skipped.");
            else
            {
                Func<int, Func<Replacement[]>> size = n => () => new[] { R(getSize, nameof(IgHelpers.GetPartySizeLimit), n) };
                const string logic = "ImprovedGarrisons.Recruitment.GarrisonRecruitmentLogic";
                const string recruiter = "ImprovedGarrisons.AI.AITypes.GarrisonRecruiter";
                PatchCallers(asm.GetType(logic, false), "CheatSpawnUnitInGarrison", size(1), ref patched);
                PatchCallers(asm.GetType(logic, false), "RecruitPrisonerToGarrison", size(1), ref patched);
                PatchCallers(asm.GetType(logic, false), "RecruitFromSurroundingVillages", size(1), ref patched);
                PatchCallers(asm.GetType(logic, false), "RecruitPrisonersInSettlement", size(1), ref patched);
                PatchCallers(asm.GetType(logic, false), "IsGarrisonRecruitmentAllowed", size(1), ref patched);
                PatchCallers(asm.GetType(logic, false), "DoesAmountExeedGarrisonLimit", size(2), ref patched);
                PatchCallers(asm.GetType(recruiter, false), "SetReturnModeIfNeeded", size(1), ref patched);
                PatchCallers(asm.GetType(recruiter, false), "GetAmountLeftToRecruit", size(1), ref patched);
                PatchCallers(asm.GetType("ImprovedGarrisons.AI.AITypes.ImprovedSettlement", false), "AutoSpawnRecruiterIfNeeded", size(1), ref patched);
                PatchCallers(asm.GetType("ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.RecruitmentUIVM", false), "InitializeRecruitmentSettingsWithoutTemplate", size(1), ref patched);
                // Compiler-generated local function in GarrisonPartyBehavior.GetBestGarrisonToReturnTo.
                MethodInfo candidate = FindNestedMethod(asm.GetType(GarrisonPartyBehaviorType, false), "considerCandidate");
                if (candidate == null)
                    Log("GarrisonPartyBehavior ...considerCandidate (nested) not found: skipped.");
                else
                    PatchOne(candidate, size(1)(), ref patched);
            }

            // 3. The two "specify upgrade path" dialogs.
            if (ownFixes) { }
            else if (specify == null)
                Log("TrainingSettings.SpecifyUpgradePath(bool) not found: dialog part skipped.");
            else
            {
                Func<Replacement[]> dialog = () => new[] { R(specify, nameof(IgHelpers.SpecifyUpgradePathUnavailable), 1) };
                PatchCallers(training, "Inquirydata_SetSpecificUpgradePath", dialog, ref patched);
                PatchCallers(training, "Inquirydata_SelectPathList", dialog, ref patched);
            }

            // 4. Models: skip IG's AddModels; note when IG's InitializeGame ran.
            MethodInfo addModels = AccessTools.DeclaredMethod(main, "AddModels", new[] { typeof(CampaignGameStarter) });
            MethodInfo initGame = AccessTools.DeclaredMethod(main, "InitializeGame", new[] { typeof(Game), typeof(IGameStarter) });
            if (addModels == null || initGame == null)
                Log("Main.AddModels / Main.InitializeGame not found: IG's models are not skipped (TAOM's size and finance models are replaced after loading a save, as without this switch).");
            else
            {
                try
                {
                    _harmony.Patch(addModels, prefix: new HarmonyMethod(typeof(IgHelpers).GetMethod(nameof(IgHelpers.AddModelsPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                    _harmony.Patch(initGame, postfix: new HarmonyMethod(typeof(IgHelpers).GetMethod(nameof(IgHelpers.InitializeGamePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                    _modelsSkipped = true;
                    patched += 2;
                    Log("Main.AddModels skipped (IG's size, speed, food and cost models are not added; TAOM's stay). IG's garrison costs are added through the AIO clan finance wrapper at game start.");
                }
                catch (Exception e)
                {
                    Log("Could not patch Main.AddModels / InitializeGame (IG's models stay as without this switch): " + e.Message);
                    try { _harmony.Unpatch(addModels, HarmonyPatchType.All, HarmonyId); } catch { }
                    try { _harmony.Unpatch(initGame, HarmonyPatchType.All, HarmonyId); } catch { }
                }
            }

            if (patched == 0)
            {
                Log("Nothing could be patched.");
                return false;
            }
            Log(patched + " IG method(s) patched.");
            return true;
        }

        internal override void OnMainMenu() => _coopGuards.Try(m => Log("[co-op] " + m));

        internal override void OnBeforeMissionBehaviorInitialize(TaleWorlds.MountAndBlade.Mission mission) => _coopGuards.Try(m => Log("[co-op] " + m));

        internal override void OnGameStart(Game game, IGameStarter starter)
        {
            _coopGuards.Try(m => Log("[co-op] " + m));
            if (!_modelsSkipped)
                return;
            // Only here: DefaultClanFinanceModel's static constructor needs Game.Current.GameTextManager.
            if (game?.GameType is Campaign && starter is CampaignGameStarter cgs)
            {
                IgHelpers.RegisterFinanceWrapper(cgs);
                TryInstallGoldGate(cgs);
            }
        }

        /// <summary>Log of this switch for the finance wrapper (set in Install; never null).</summary>
        internal static Action<string> GateLog = _ => { };

        // v1.4.0: only when our wrapper is the registered clan finance model (then DefaultClanFinanceModel's
        // static constructor has run). Nothing here can stop the rest of the switch.
        private void TryInstallGoldGate(CampaignGameStarter starter)
        {
            try
            {
                GateLog = Log;
                if (starter.GetModel<TaleWorlds.CampaignSystem.ComponentInterfaces.ClanFinanceModel>() is AioIgClanFinanceModel)
                    CoopGoldGate.Install(Log);
                else
                    Log("Clan finance wrapper is not the active model: gold gate check not installed.");
            }
            catch (Exception e)
            {
                Log("Gold gate check not installed (ignored): " + e.Message);
            }
        }

        internal override void OnGameEnd(Game game)
        {
            CoopGoldGate.Remove();
            IgHelpers.OnGameEnd();
        }

        internal override void Uninstall()
        {
            _coopGuards.Uninstall();
            // Safe at any point, also after a partial Install.
            try { (_harmony ?? new Harmony(HarmonyId)).UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            CoopGoldGate.Remove();
            _modelsSkipped = false;
            lock (Specs) Specs.Clear();
            IgHelpers.Reset();
        }

        // ---- patching helpers ----

        private static Replacement R(MethodInfo target, string helperName, int expected) => new Replacement
        {
            Target = target,
            Helper = typeof(IgHelpers).GetMethod(helperName, BindingFlags.Static | BindingFlags.NonPublic),
            Expected = expected,
        };

        // Patches every declared method of that name (normally one). True if one was patched and every count matched.
        private bool PatchCallers(Type type, string methodName, Func<Replacement[]> make, ref int patched)
        {
            if (type == null)
            {
                Log("Caller type for " + methodName + " not found: skipped.");
                return false;
            }
            List<MethodInfo> methods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == methodName).ToList();
            if (methods.Count == 0)
            {
                Log(type.Name + "." + methodName + " not found: skipped.");
                return false;
            }
            bool all = true;
            foreach (MethodInfo m in methods)
                all &= PatchOne(m, make(), ref patched);
            return all;
        }

        private bool PatchOne(MethodInfo caller, Replacement[] reps, ref int patched)
        {
            string name = caller.DeclaringType?.Name + "." + caller.Name;
            if (reps.Any(r => r.Helper == null))
            {
                Log(name + ": internal error, helper missing. Skipped.");
                return false;
            }
            // v1.5.2 safety net: never swap a call for a helper that leaves a different value on the stack.
            Replacement bad = reps.FirstOrDefault(r => r.Helper.ReturnType != r.Target.ReturnType);
            if (bad != null)
            {
                Log(name + ": " + bad.Target.Name + " return type changed (" + bad.Target.ReturnType.Name + ", helper " + bad.Helper.ReturnType.Name + "); not replaced.");
                return false;
            }
            lock (Specs) Specs[caller.MetadataToken] = reps.ToList();
            try
            {
                _harmony.Patch(caller, transpiler: new HarmonyMethod(typeof(ImprovedGarrisonsFixes).GetMethod(nameof(Transpiler), BindingFlags.Static | BindingFlags.NonPublic)));
                patched++;
            }
            catch (Exception e)
            {
                Log(name + ": patch failed (" + e.GetType().Name + ": " + e.Message + "). Skipped.");
                lock (Specs) Specs.Remove(caller.MetadataToken);
                return false;
            }
            bool ok = true;
            foreach (Replacement r in reps)
            {
                bool match = r.Found == r.Expected && r.DelegateRefs == 0;
                ok &= match;
                Log(name + ": " + r.Target.Name + " call sites expected " + r.Expected + ", found and replaced " + r.Found
                    + ", delegate references " + r.DelegateRefs + (match ? "." : " (MISMATCH)."));
            }
            return ok;
        }

        // Searches the nested (compiler-generated) types of a type, without Assembly.GetTypes().
        private static MethodInfo FindNestedMethod(Type outer, string nameContains)
        {
            if (outer == null)
                return null;
            try
            {
                foreach (Type t in outer.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    foreach (MethodInfo m in AccessTools.GetDeclaredMethods(t))
                        if (m.Name.IndexOf(nameContains, StringComparison.Ordinal) >= 0)
                            return m;
                }
            }
            catch { }
            return null;
        }

        private static bool Same(MethodBase a, MethodBase b) =>
            a != null && b != null && a.MetadataToken == b.MetadataToken && a.Module == b.Module;

        // Replaces call/callvirt <target> by call <helper> (same stack: instance, arguments). Counts delegate
        // references (ldftn/ldvirtftn) to the target, which it cannot replace. Never throws.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> list = instructions.ToList();
            try
            {
                List<Replacement> reps;
                lock (Specs)
                {
                    if (original == null || original.Module != _igModule || !Specs.TryGetValue(original.MetadataToken, out reps))
                        return list;
                }
                // Harmony runs every transpiler again whenever anyone re-patches this method: count per run.
                foreach (Replacement r in reps)
                {
                    r.Found = 0;
                    r.DelegateRefs = 0;
                }
                var changes = new List<KeyValuePair<int, Replacement>>();
                for (int i = 0; i < list.Count; i++)
                {
                    CodeInstruction ins = list[i];
                    if (!(ins.operand is MethodBase op))
                        continue;
                    foreach (Replacement r in reps)
                    {
                        if (!Same(op, r.Target))
                            continue;
                        if (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
                            changes.Add(new KeyValuePair<int, Replacement>(i, r));
                        else if (ins.opcode == OpCodes.Ldftn || ins.opcode == OpCodes.Ldvirtftn)
                            r.DelegateRefs++;
                    }
                }
                foreach (KeyValuePair<int, Replacement> c in changes)
                {
                    list[c.Key].opcode = OpCodes.Call;
                    list[c.Key].operand = c.Value.Helper;
                    c.Value.Found++;
                }
                return list;
            }
            catch (Exception e)
            {
                AioLog.Write("[Improved Garrisons] Transpiler error (method left unchanged): " + e.Message);
                return instructions;
            }
        }
    }
}
