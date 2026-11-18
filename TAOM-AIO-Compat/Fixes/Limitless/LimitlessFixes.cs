// Limitless switch (v1.5.0): Limitless 1.0.2.1 (Nexus 14025) together with BattleSizeResized and TAOM, single player only.
// Plan, review and evidence: docs\2026-11-17-plan-v1.5.0-limitless.md. Names checked against the decompiled
// Limitless.dll 1.0.2.1, BattleSizeResized.dll 2.0.4 and the Bannerlord 1.5.7 DLLs (17 Nov 2026).
//   A  BattleSizeResized clamps DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission to at most 2040.
//      Limitless' main-menu start-up check needs that getter to equal its raised engine cap, and BattleSizeResized's
//      first-wave budget reads it too. A last postfix gives back the engine value when it is above 2040, which is
//      only the case while Limitless' engine change is active. Installed only when BattleSizeResized is ticked.
//   B  The AIO's "Limitless battle size" choice lowers Limitless' per-battle slot budget (soldiers and horses on
//      the field at once) right after Limitless registers it. Read from MCM at each battle; never raises it.
//   C  Limitless throws ("Campaign initial spawn no longer fits its slot budget") when a side's first wave no
//      longer fits its budget, e.g. after TAOM's mumakil or howdah crews spawned between the two sides' first
//      waves. C1: Limitless sizes the first wave a small reserve short of its budget, and while it does, part A
//      reports at most the battle's budget, so BattleSizeResized's own first-wave guard keeps working. C2: if a
//      first wave still does not fit, the budget is raised for that one spawn, never above Limitless' engine cap
//      (2040 in sieges and sally-outs, the author's warning, Unverified). No troop is ever held back (the review
//      showed that blocks wave reinforcements).
// Every part does nothing during a co-op session. Nothing of Limitless is shipped or edited; its private members
// are read by name, and B and C change Limitless' in-memory budget number of the running battle only.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TAOM_AIO_Compat.Settings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.Limitless
{
    internal sealed class LimitlessFixes : FixGroup
    {
        internal override string SettingKey => "LimitlessFixes";
        internal override string Name => "Limitless";
        internal override string TargetModuleId => "Limitless";

        internal override string OffWarning =>
            "With BattleSizeResized ticked: if Limitless loads before BattleSizeResized (our load order) it still starts, but every first wave keeps BattleSizeResized's 2040 limit; if it loads after, it stops at start-up. The AIO battle size choice is gone, and a battle is more likely to crash at its start when a first wave no longer fits Limitless' budget (for example after TAOM's mumakil crews spawn). Untick Limitless as well.";

        private const string HarmonyId = "taom.compat.aio.limitless";
        private const string LimitlessAssembly = "Limitless";
        private const int StockAgentCap = 2040;
        private const int SiegeRaiseCeiling = 2040; // Limitless' page: siege slots above 2040 can leave agents stuck
        private const int MinReserve = 8;           // BattleSizeResized's default "Reserved Agent Slots"

        // Battle size choices (MCM dropdown index -> slots; 0 = all of Limitless' slots). Order must match AioSettings.
        internal static readonly int[] SizeChoices = { 2040, 3000, 4000, 6000, 0 };
        internal const int DefaultSizeChoice = 4;

        private static Action<string> _log;
        private static volatile bool _coopSession;
        private static readonly HashSet<string> LoggedOnce = new HashSet<string>();
        private static readonly object LogGate = new object();

        // Limitless internals (by name)
        private static object _budgets;             // ConditionalWeakTable<DefaultBattleMissionAgentSpawnLogic, Budget>
        private static MethodInfo _budgetTryGet;    // its TryGetValue
        private static FieldInfo _budgetSlots;      // Budget.Slots (readonly int; written by reflection, see plan)
        private static Func<IAgentOriginBase, MissionBattleSideSpawnContext, int> _slotCost;
        private static Func<uint> _nativeCapacity;  // NativePatcher.GetCapacity (0 = Limitless not initialized)
        // Vanilla private members
        private static AccessTools.FieldRef<int> _engineCapCache; // DefaultBattleMissionAgentSpawnLogic._maxNumberOfAgentsForMissionCache
        private static FieldInfo _battleSize;       // DefaultBattleMissionAgentSpawnLogic._battleSize
        private static FieldInfo _ctxSpawnLogic;    // MissionBattleSideSpawnContext._spawnLogic
        private static FieldInfo _ctxReserved;      // MissionBattleSideSpawnContext._reservedTroops
        private static FieldInfo _ctxSide;          // MissionBattleSideSpawnContext._side

        // C1: the logic whose budget is lowered between the two InitWithSinglePhase prefixes, and its real slots.
        private static DefaultBattleMissionAgentSpawnLogic _headroomLogic;
        private static int _headroomSlots;
        // A: while a budgeted battle's InitWithSinglePhase runs, the agent cap reported is at most its budget.
        [ThreadStatic] private static int _sizingSlots;
        // C2: battles Limitless registered as a siege or sally-out (BattleSizeType 1 or 2, or a custom siege).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DefaultBattleMissionAgentSpawnLogic, object> SiegeLogics =
            new System.Runtime.CompilerServices.ConditionalWeakTable<DefaultBattleMissionAgentSpawnLogic, object>();
        private static bool _coopWarned;

        private Harmony _harmony;

        internal override bool Install()
        {
            _log = Log;
            Log("Limitless " + (ModEnv.ModuleVersion(TargetModuleId) ?? "version unknown") + " (written for v1.0.2.1). Single player only: nothing here acts during a co-op session.");
            if (!ResolveNames())
                return false;

            MethodInfo agentCap = AccessTools.PropertyGetter(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission));
            MethodInfo factory = AccessTools.DeclaredMethod(AccessTools.TypeByName("SandBox.SandBoxMissions"), "CreateCampaignMissionAgentSpawnLogic");
            MethodInfo customBattle = AccessTools.DeclaredMethod(typeof(BannerlordMissions), "OpenCustomBattleMission");
            MethodInfo customSiege = AccessTools.DeclaredMethod(typeof(BannerlordMissions), "OpenSiegeMissionWithDeployment");
            MethodInfo initSingle = AccessTools.DeclaredMethod(typeof(DefaultBattleMissionAgentSpawnLogic), "InitWithSinglePhase");
            MethodInfo spawnTroops = AccessTools.DeclaredMethod(typeof(MissionBattleSideSpawnContext), "SpawnTroops", new[] { typeof(int), typeof(bool) });
            if (agentCap == null || factory == null || customBattle == null || customSiege == null || initSingle == null || spawnTroops == null)
            {
                Log("A game method was not found (agent cap " + (agentCap != null) + ", campaign spawn logic " + (factory != null) + ", custom battle " + (customBattle != null)
                    + ", custom siege " + (customSiege != null) + ", InitWithSinglePhase " + (initSingle != null) + ", SpawnTroops " + (spawnTroops != null) + "). Nothing changed.");
                return false;
            }

            var h = new Harmony(HarmonyId);
            _harmony = h; // set before patching, so Uninstall removes a half-applied set
            if (ModEnv.IsActive("BattleSizeResized"))
            {
                h.Patch(agentCap, postfix: new HarmonyMethod(Method(nameof(AgentCapPostfix)), Priority.Last));
                Log("A: agent cap postfix installed (after BattleSizeResized'): Limitless' start-up check and BattleSizeResized's first wave see Limitless' engine cap.");
            }
            else
                Log("A: BattleSizeResized is not ticked; the agent cap is left alone (not needed).");
            h.Patch(factory, postfix: new HarmonyMethod(Method(nameof(FactoryPostfix)), Priority.Low));
            h.Patch(customBattle, postfix: new HarmonyMethod(Method(nameof(MissionPostfix)), Priority.Low));
            h.Patch(customSiege, postfix: new HarmonyMethod(Method(nameof(CustomSiegePostfix)), Priority.Low));
            Log("B: battle size choice installed (after Limitless sets up a battle's budget; read from MCM at each battle).");
            h.Patch(initSingle, prefix: new HarmonyMethod(Method(nameof(HeadroomPrefix)), Priority.First),
                                finalizer: new HarmonyMethod(Method(nameof(InitFinalizer)), Priority.First));
            h.Patch(initSingle, prefix: new HarmonyMethod(Method(nameof(HeadroomRestorePrefix)), Priority.Normal - 1));
            h.Patch(spawnTroops, prefix: new HarmonyMethod(Method(nameof(SpawnTroopsPrefix)), Priority.High),
                                 finalizer: new HarmonyMethod(Method(nameof(SpawnTroopsFinalizer)), Priority.High));
            Log("C: first-wave guard installed (reserve when Limitless sizes the first wave; raise, never trim, before Limitless' slot check).");
            return true;
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
        }

        internal override void OnMainMenu()
        {
            _coopSession = CoopEnv.IsSessionActive();
            if (CoopEnv.IsCoopTicked && !_coopWarned)
            {
                _coopWarned = true;
                const string text = "[TAOM AIO] Limitless is single player only: untick Limitless in the launcher before a co-op session (Co-op would also require every player to have it ticked).";
                LogOnce("coop-ticked", text);
                try { InformationManager.DisplayMessage(new InformationMessage(text)); } catch { }
            }
        }

        internal override void OnGameStart(Game game, IGameStarter starter) => _coopSession = CoopEnv.IsSessionActive();

        internal override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            _coopSession = CoopEnv.IsSessionActive();
            _headroomLogic = null;
            _sizingSlots = 0;
            lock (LogGate) LoggedOnce.RemoveWhere(k => k.StartsWith("battle:", StringComparison.Ordinal));
        }

        private static MethodInfo Method(string name) => typeof(LimitlessFixes).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        private bool ResolveNames()
        {
            const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type patches = ModEnv.FindType(LimitlessAssembly, "Limitless.CampaignSpawnPatches");
            _budgets = patches?.GetField("Budgets", S)?.GetValue(null);
            _budgetTryGet = _budgets?.GetType().GetMethod("TryGetValue");
            _budgetSlots = patches?.GetNestedType("Budget", BindingFlags.NonPublic | BindingFlags.Public)?.GetField("Slots", I);
            MethodInfo slotCost = patches?.GetMethod("SlotCost", S, null, new[] { typeof(IAgentOriginBase), typeof(MissionBattleSideSpawnContext) }, null);
            if (slotCost != null && slotCost.ReturnType == typeof(int))
                _slotCost = (Func<IAgentOriginBase, MissionBattleSideSpawnContext, int>)Delegate.CreateDelegate(typeof(Func<IAgentOriginBase, MissionBattleSideSpawnContext, int>), slotCost);
            MethodInfo capacity = ModEnv.FindType(LimitlessAssembly, "Limitless.NativePatcher")?.GetMethod("GetCapacity", S, null, Type.EmptyTypes, null);
            if (capacity != null && capacity.ReturnType == typeof(uint))
                _nativeCapacity = (Func<uint>)Delegate.CreateDelegate(typeof(Func<uint>), capacity);

            FieldInfo cache = AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_maxNumberOfAgentsForMissionCache");
            if (cache != null && cache.IsStatic && cache.FieldType == typeof(int))
                _engineCapCache = AccessTools.StaticFieldRefAccess<int>(cache);
            _battleSize = AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_battleSize");
            _ctxSpawnLogic = AccessTools.Field(typeof(MissionBattleSideSpawnContext), "_spawnLogic");
            _ctxReserved = AccessTools.Field(typeof(MissionBattleSideSpawnContext), "_reservedTroops");
            _ctxSide = AccessTools.Field(typeof(MissionBattleSideSpawnContext), "_side");

            bool ok = _budgetTryGet != null && _budgetSlots != null && _budgetSlots.FieldType == typeof(int) && _slotCost != null && _nativeCapacity != null
                      && _engineCapCache != null && _battleSize != null && _battleSize.FieldType == typeof(int)
                      && _ctxSpawnLogic != null && _ctxReserved != null && _ctxReserved.FieldType == typeof(List<IAgentOriginBase>)
                      && _ctxSide != null && _ctxSide.FieldType == typeof(BattleSideEnum);
            if (!ok)
                Log("A Limitless or game member was not found (budget table " + (_budgetTryGet != null) + ", budget slots " + (_budgetSlots != null)
                    + ", slot cost " + (_slotCost != null) + ", native capacity " + (_nativeCapacity != null) + ", engine cap cache " + (_engineCapCache != null)
                    + ", battle size " + (_battleSize != null) + ", spawn context fields " + (_ctxSpawnLogic != null && _ctxReserved != null && _ctxSide != null)
                    + "). This Limitless version is not supported; nothing changed.");
            return ok;
        }

        private static void LogOnce(string key, string message)
        {
            lock (LogGate)
            {
                if (!LoggedOnce.Add(key))
                    return;
            }
            try { _log?.Invoke(message); } catch { }
        }

        /// <summary>Limitless' budget object for this spawn logic, or null (no budget: not a Limitless battle).</summary>
        private static object BudgetOf(DefaultBattleMissionAgentSpawnLogic logic)
        {
            if (logic == null)
                return null;
            object[] args = { logic, null };
            return (bool)_budgetTryGet.Invoke(_budgets, args) ? args[1] : null;
        }

        private static int Slots(object budget) => (int)_budgetSlots.GetValue(budget);
        private static void SetSlots(object budget, int slots) => _budgetSlots.SetValue(budget, slots);

        // ---- A ----
        private static void AgentCapPostfix(ref int __result)
        {
            try
            {
                if (_coopSession)
                    return;
                int engine = _engineCapCache();
                if (engine > StockAgentCap && __result < engine)
                    __result = engine;
                int sizing = _sizingSlots;
                if (sizing > 0 && __result > sizing)
                    __result = sizing; // a budgeted battle is being sized: BattleSizeResized's first wave stays within its budget
            }
            catch (Exception e) { LogOnce("A-error", "A: agent cap postfix error (value left as it was): " + e.Message); }
        }

        // ---- B ----
        private static void FactoryPostfix(Mission.BattleSizeType battleSizeType, DefaultBattleMissionAgentSpawnLogic __result)
        {
            MarkSiege(__result, battleSizeType != Mission.BattleSizeType.Battle);
            ApplySizeChoice(__result);
        }

        private static void CustomSiegePostfix(Mission __result)
        {
            try
            {
                DefaultBattleMissionAgentSpawnLogic logic = __result?.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
                MarkSiege(logic, true);
                ApplySizeChoice(logic);
            }
            catch (Exception e) { LogOnce("B-error", "B: battle size choice error (Limitless' own size used): " + e.Message); }
        }

        private static void MarkSiege(DefaultBattleMissionAgentSpawnLogic logic, bool siege)
        {
            try
            {
                if (logic != null && siege && !_coopSession && BudgetOf(logic) != null && !SiegeLogics.TryGetValue(logic, out _))
                    SiegeLogics.Add(logic, null);
            }
            catch { }
        }

        private static void MissionPostfix(Mission __result)
        {
            try { ApplySizeChoice(__result?.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>()); }
            catch (Exception e) { LogOnce("B-error", "B: battle size choice error (Limitless' own size used): " + e.Message); }
        }

        internal static int ChosenSlots()
        {
            int index = DefaultSizeChoice;
            try
            {
                AioSettings s = AioSettings.Instance;
                if (s?.LimitlessBattleSize != null)
                    index = s.LimitlessBattleSize.SelectedIndex;
            }
            catch (Exception e) { LogOnce("B-settings", "B: MCM value not readable, using 'Unlimited': " + e.Message); }
            return index >= 0 && index < SizeChoices.Length ? SizeChoices[index] : 0;
        }

        private static void ApplySizeChoice(DefaultBattleMissionAgentSpawnLogic logic)
        {
            try
            {
                if (_coopSession || logic == null)
                    return;
                object budget = BudgetOf(logic);
                if (budget == null)
                    return;
                int slots = Slots(budget);
                int cap = ChosenSlots();
                if (cap <= 0 || cap >= slots)
                {
                    LogOnce("battle:B", "B: battle size " + slots + " slots (Limitless' own setting; AIO choice " + (cap <= 0 ? "Unlimited" : cap.ToString()) + ").");
                    return;
                }
                SetSlots(budget, cap);
                _battleSize.SetValue(logic, cap);
                LogOnce("battle:B", "B: battle size lowered from Limitless' " + slots + " to " + cap + " slots (AIO choice).");
            }
            catch (Exception e) { LogOnce("B-error", "B: battle size choice error (Limitless' own size used): " + e.Message); }
        }

        // ---- C1: reserve while Limitless sizes the first wave ----
        private static void HeadroomPrefix(DefaultBattleMissionAgentSpawnLogic __instance)
        {
            _headroomLogic = null;
            if (_coopSession)
                return;
            try
            {
                object budget = BudgetOf(__instance);
                if (budget == null)
                    return;
                int slots = Slots(budget);
                _sizingSlots = slots;
                int reserve = Math.Max(MinReserve, slots / 100);
                if (slots - reserve < 2)
                    return;
                _headroomLogic = __instance;
                _headroomSlots = slots;
                SetSlots(budget, slots - reserve);
                LogOnce("battle:C1", "C: first wave sized with " + reserve + " of " + slots + " slots kept free for agents that arrive between the two sides' first waves.");
            }
            catch (Exception e) { _headroomLogic = null; LogOnce("C1-error", "C: reserve not applied (Limitless sizes as usual): " + e.Message); }
        }

        private static void HeadroomRestorePrefix(DefaultBattleMissionAgentSpawnLogic __instance) => RestoreHeadroom(__instance);

        private static Exception InitFinalizer(DefaultBattleMissionAgentSpawnLogic __instance, Exception __exception)
        {
            _sizingSlots = 0;
            RestoreHeadroom(__instance); // only does something if a prefix threw before the restore prefix ran
            return __exception;
        }

        private static void RestoreHeadroom(DefaultBattleMissionAgentSpawnLogic __instance)
        {
            if (_headroomLogic == null || !ReferenceEquals(_headroomLogic, __instance))
                return;
            try
            {
                object budget = BudgetOf(__instance);
                if (budget != null)
                    SetSlots(budget, _headroomSlots);
            }
            catch (Exception e) { LogOnce("C1-error", "C: budget not restored after sizing (stays a little lower): " + e.Message); }
            finally { _headroomLogic = null; }
        }

        // ---- C2: raise, never trim, for a first wave that no longer fits ----
        private static void SpawnTroopsPrefix(MissionBattleSideSpawnContext __instance, int number, bool isReinforcement, out int __state)
        {
            __state = -1; // slots to put back in the postfix; -1 = nothing changed
            if (isReinforcement || number <= 0 || _coopSession)
                return;
            try
            {
                var logic = _ctxSpawnLogic.GetValue(__instance) as DefaultBattleMissionAgentSpawnLogic;
                object budget = BudgetOf(logic);
                if (budget == null)
                    return;
                var reserved = (List<IAgentOriginBase>)_ctxReserved.GetValue(__instance);
                var side = (BattleSideEnum)_ctxSide.GetValue(__instance);
                MissionSpawnPhase phase = side == BattleSideEnum.Defender ? logic.DefenderActivePhase : logic.AttackerActivePhase;
                if (reserved == null || number > reserved.Count || phase == null || phase.InitialSpawnNumber != number)
                    return; // not the vanilla first-wave call; left to Limitless
                int need = 0;
                for (int i = 0; i < number; i++)
                    need += _slotCost(reserved[i], __instance);
                int slots = Slots(budget);
                int agents = logic.NumberOfAgents;
                if (need <= slots - agents)
                    return;
                int ceiling = (int)Math.Min(int.MaxValue, _nativeCapacity());
                if (SiegeLogics.TryGetValue(logic, out _))
                    ceiling = Math.Min(ceiling, SiegeRaiseCeiling);
                ceiling = Math.Max(ceiling, slots); // never below the battle's own budget
                int wanted = agents + need;
                if (wanted > ceiling)
                {
                    LogOnce("battle:C2-fail:" + side, "C: " + side + " first wave needs " + need + " slots, " + (slots - agents) + " free and the engine/siege ceiling is " + ceiling
                        + "; not changed, Limitless will stop this battle (its own check). Lower the battle size choice or Limitless' slot limit.");
                    return;
                }
                SetSlots(budget, wanted);
                __state = slots;
                LogOnce("battle:C2:" + side, "C: " + side + " first wave needs " + need + " slots but " + (slots - agents) + " were free; budget raised to " + wanted
                    + " for this spawn only (engine/siege ceiling " + ceiling + "), so Limitless does not stop the battle.");
            }
            catch (Exception e) { __state = -1; LogOnce("C2-error", "C: first-wave guard error (Limitless' own check applies): " + e.Message); }
        }

        private static Exception SpawnTroopsFinalizer(MissionBattleSideSpawnContext __instance, int __state, Exception __exception)
        {
            if (__state <= 0) // Harmony's __state starts at 0 if an earlier prefix threw before ours ran
                return __exception;
            try
            {
                object budget = BudgetOf(_ctxSpawnLogic.GetValue(__instance) as DefaultBattleMissionAgentSpawnLogic);
                if (budget != null)
                    SetSlots(budget, __state);
            }
            catch (Exception e) { LogOnce("C2-error", "C: budget not put back after the first wave (stays raised for this battle): " + e.Message); }
            return __exception;
        }
    }
}
