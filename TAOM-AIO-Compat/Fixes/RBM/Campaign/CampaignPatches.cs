// Copied from TAOM_Standalone_Compat_RBM v1.0.11 (BalancePolicy, TaomGrainPrice, TaomRecruitmentPrice,
// CustomTournamentParticipants, CustomTournamentPrizes, CustomStoredTournamentPrize, CustomPrizeSelection)
// by Theb0yys, TAOM team, with permission (owner, 13 Nov 2026). Updated for Bannerlord 1.5.7 / TAOM 2.0.38 /
// RBM 4.8.1:
//  - prize eligibility written with enum names: Shield/Bow/Crossbow (the original's compiled 8/9/10 on 1.5.7),
//    which mirrors RBM 4.8.1's own prize filter (RBMTournament Participants.cs). Behaviour unchanged;
//  - targets, the AddTroopsFromCulture delegate, the ItemObject.Value setter and the prize backing field are
//    resolved by Prepare() (called by RbmFixes, in try/catch) instead of static initializers;
//  - RBM settings are read by reflection (RbmConfig); every patch body is in try/catch and falls back to
//    RBM's or the game's own result.
// v1.4.0 (plan 2b, audit 14 Nov 2026): TaomPrizePool, at the end of this file. TAOM 2.0.39 lets the player pick
// one of three tournament prizes from its own pool (TournamentService.BuildPrizePool: any merchandise weapon
// or armour of the band). StoredTournamentPrize below replaces a stored prize that fails
// PrizeSelection.IsEligible, so a prize the player picked could be swapped afterwards. TaomPrizePool removes
// the items that would be replaced from TAOM's pool, so every prize TAOM offers is one that is kept.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Fixes.RBM.Content;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace TAOM_AIO_Compat.Fixes.RBM.Campaign
{
    internal static class CampaignTargets
    {
        internal const string TournamentPatchType = "RBMTournament.RBMTournament+FightTournamentGamePatch";

        internal static MethodInfo GrainApply;
        internal static MethodInfo RecruitPrice;
        internal static MethodInfo FillTroopList;
        internal static Type TaomWageModel;
        internal static Action<ItemObject, int> SetItemValue;
        internal static AccessTools.FieldRef<TournamentGame, ItemObject> StoredPrize;
        internal delegate void AddCultureTroops(CultureObject culture, ref List<CharacterObject> troops);
        internal static AddCultureTroops AddTroops;

        /// <summary>False if RBM's tournament code is not loaded yet (try again later). Throws if something is missing.</summary>
        internal static bool Prepare()
        {
            Type tournament = AccessTools.TypeByName(TournamentPatchType);
            if (tournament == null)
                return false;
            GrainApply = AccessTools.Method(AccessTools.TypeByName("RBMCampaign.TradeGoodValues"), "Apply", new[] { typeof(ItemObject) })
                ?? throw new MissingMethodException("RBMCampaign.TradeGoodValues.Apply(ItemObject)");
            RecruitPrice = AccessTools.Method(AccessTools.TypeByName("RBMCampaign.RecruitSupply"), "RecruitPrice",
                new[] { typeof(CharacterObject), typeof(Hero), typeof(Settlement), typeof(bool) })
                ?? throw new MissingMethodException("RBMCampaign.RecruitSupply.RecruitPrice(CharacterObject, Hero, Settlement, bool)");
            FillTroopList = AccessTools.DeclaredMethod(tournament, "FillTroopListFromCulture")
                ?? throw new MissingMethodException(TournamentPatchType + ".FillTroopListFromCulture");
            MethodInfo add = AccessTools.DeclaredMethod(tournament, "AddTroopsFromCulture")
                ?? throw new MissingMethodException(TournamentPatchType + ".AddTroopsFromCulture");
            AddTroops = AccessTools.MethodDelegate<AddCultureTroops>(add);
            TaomWageModel = AccessTools.TypeByName("TAOM.Features.TroopProgression.Models.TaomPartyWageModel")
                ?? throw new InvalidOperationException("TAOM recruitment model TaomPartyWageModel is unavailable.");
            MethodInfo setter = AccessTools.PropertySetter(typeof(ItemObject), "Value")
                ?? throw new MissingMethodException("ItemObject.Value setter");
            SetItemValue = (Action<ItemObject, int>)Delegate.CreateDelegate(typeof(Action<ItemObject, int>), setter);
            StoredPrize = AccessTools.FieldRefAccess<TournamentGame, ItemObject>("<Prize>k__BackingField");
            return true;
        }
    }

    /// <summary>Grain base value back to 10 after RBM's trade-good prices (RBM campaign on).</summary>
    [HarmonyPatch]
    internal static class GrainPrice
    {
        internal const int GrainBaseValue = 10;

        internal static MethodBase TargetMethod() => CampaignTargets.GrainApply;

        [HarmonyPostfix]
        [HarmonyPriority(0)]
        internal static void Postfix(ItemObject __0)
        {
            try
            {
                if (__0 != null && __0.StringId == "grain" && RbmConfig.CampaignEnabled)
                    CampaignTargets.SetItemValue(__0, GrainBaseValue);
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("grain", "Grain price: not changed (error caught): " + e.Message);
            }
        }
    }

    /// <summary>Recruit price from TAOM's own wage model instead of RBM's RecruitSupply.RecruitPrice.</summary>
    [HarmonyPatch]
    internal static class RecruitmentPrice
    {
        internal static MethodBase TargetMethod() => CampaignTargets.RecruitPrice;

        [HarmonyPrefix]
        internal static bool Prefix(CharacterObject __0, Hero __1, ref ExplainedNumber __result)
        {
            try
            {
                PartyWageModel model = TaleWorlds.CampaignSystem.Campaign.Current?.Models?.PartyWageModel;
                if (!RbmConfig.CampaignEnabled || model == null || CampaignTargets.TaomWageModel == null
                    || model.GetType() != CampaignTargets.TaomWageModel || __0 == null || __0.IsHero)
                    return true;
                __result = model.GetTroopRecruitmentCost(__0, __1, false);
                return false;
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("recruit", "Recruit price: RBM's price used (error caught): " + e.Message);
                return true;
            }
        }
    }

    /// <summary>RBM tournaments also draw troops of the culture's basic/elite troop cultures (TAOM sub-cultures).</summary>
    [HarmonyPatch]
    internal static class TournamentParticipants
    {
        internal static MethodBase TargetMethod() => CampaignTargets.FillTroopList;

        [HarmonyPostfix]
        internal static void Postfix(CultureObject culture, List<CharacterObject> __result)
        {
            try
            {
                if (!ContentRepairs.CustomAmmoLoaded || culture == null || __result == null)
                    return;
                CultureObject basic = culture.BasicTroop?.Culture;
                CultureObject elite = culture.EliteBasicTroop?.Culture;
                List<CharacterObject> troops = __result;
                if (basic != null && basic != culture)
                    CampaignTargets.AddTroops(basic, ref troops);
                if (elite != null && elite != culture && elite != basic)
                    CampaignTargets.AddTroops(elite, ref troops);
                var seen = new HashSet<CharacterObject>();
                int kept = 0;
                for (int i = 0; i < __result.Count; i++)
                {
                    CharacterObject troop = __result[i];
                    if (troop != null && (troop.Culture == culture || (basic != null && troop.Culture == basic) || (elite != null && troop.Culture == elite))
                        && seen.Add(troop))
                        __result[kept++] = troop;
                }
                if (kept < __result.Count)
                    __result.RemoveRange(kept, __result.Count - kept);
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("participants", "Tournament troops: RBM's list used (error caught): " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(FightTournamentGame), "GetTournamentPrize")]
    internal static class TournamentPrizes
    {
        [HarmonyPostfix]
        [HarmonyPriority(0)]
        internal static void Postfix(FightTournamentGame __instance, ref ItemObject __result)
        {
            try
            {
                __result = PrizeSelection.Replace(__result, PrizeSelection.TownCulture(__instance), PrizeSelection.Seed(__instance, __result));
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("prize", "Tournament prize: not changed (error caught): " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TournamentGame), "Prize", MethodType.Getter)]
    internal static class StoredTournamentPrize
    {
        [HarmonyPostfix]
        [HarmonyPriority(0)]
        internal static void Postfix(TournamentGame __instance, ref ItemObject __result)
        {
            try
            {
                if (!(__instance is FightTournamentGame) || __result == null)
                    return;
                ItemObject replacement = PrizeSelection.Replace(__result, PrizeSelection.TownCulture(__instance), PrizeSelection.Seed(__instance, __result));
                if (replacement != __result)
                {
                    CampaignTargets.StoredPrize(__instance) = replacement;
                    __result = replacement;
                }
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("storedprize", "Stored tournament prize: not changed (error caught): " + e.Message);
            }
        }
    }

    /// <summary>Picks a TAOM / Armory prize of the town's culture (or its sub-cultures) and the nearest tier.</summary>
    internal static class PrizeSelection
    {
        private static Game _game;
        private static int _revision = -1;
        private static List<ItemObject> _catalog;

        internal static void Clear()
        {
            _catalog = null;
            _game = null;
            _revision = -1;
        }

        internal static CultureObject TownCulture(TournamentGame game) => game?.Town?.Owner?.Settlement?.Culture;

        internal static bool IsEligible(ItemObject item)
        {
            if (item == null || !PrizeSources.Contains(item.StringId) || item.NotMerchandise || item.IsCraftedByPlayer || item.MultiplayerItem
                || item.ItemComponent == null)
                return false;
            ItemObject.ItemTypeEnum type = item.Type;
            if (type == ItemObject.ItemTypeEnum.Shield || type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow
                || item.IsCraftedWeapon || item.IsMountable)
                return true;
            return item.ArmorComponent != null;
        }

        internal static ItemObject Replace(ItemObject current, CultureObject culture, string seed)
        {
            Game game = Game.Current;
            if (game?.ObjectManager == null || culture == null || PrizeSources.Revision == 0 || IsEligible(current))
                return current;
            if (_game != game || _revision != PrizeSources.Revision || _catalog == null)
            {
                var catalog = new List<ItemObject>();
                foreach (ItemObject item in game.ObjectManager.GetObjectTypeList<ItemObject>())
                {
                    if (IsEligible(item))
                        catalog.Add(item);
                }
                _catalog = catalog;
                _game = game;
                _revision = PrizeSources.Revision;
            }
            return Select(current, culture, _catalog, seed);
        }

        internal static ItemObject Select(ItemObject current, CultureObject culture, IEnumerable<ItemObject> candidates, string seed)
        {
            if (IsEligible(current))
                return current;
            int tier = current == null ? 2 : (int)current.Tier;
            int best = int.MaxValue;
            var choices = new List<ItemObject>();
            CultureObject basic = culture?.BasicTroop?.Culture;
            CultureObject elite = culture?.EliteBasicTroop?.Culture;
            foreach (ItemObject candidate in candidates)
            {
                if (!IsEligible(candidate))
                    continue;
                bool cultureMatch = culture != null && (candidate.Culture == culture || (basic != null && candidate.Culture == basic) || (elite != null && candidate.Culture == elite));
                int score = (cultureMatch ? 0 : 100) + Math.Abs((int)candidate.Tier - tier);
                if (score > best)
                    continue;
                if (score < best)
                {
                    choices.Clear();
                    best = score;
                }
                choices.Add(candidate);
            }
            if (choices.Count == 0)
                return current;
            // v1.3.0 (audit 14 Nov 2026): no MBRandom here. The same town and incoming prize give the same pick on
            // every machine (co-op host and client), whatever order the catalog is in.
            choices.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
            return choices[(int)(StableHash(seed) % (uint)choices.Count)];
        }

        /// <summary>Town settlement id + "|" + incoming prize id: the same on host and client.</summary>
        internal static string Seed(TournamentGame game, ItemObject current) =>
            (game?.Town?.Settlement?.StringId ?? "") + "|" + (current?.StringId ?? "");

        /// <summary>FNV-1a, 32 bit, over the UTF-16 code units (low byte, then high byte). Not string.GetHashCode: that differs per process.</summary>
        internal static uint StableHash(string text)
        {
            uint hash = 2166136261u;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    hash = unchecked((hash ^ (byte)(c & 0xFF)) * 16777619u);
                    hash = unchecked((hash ^ (byte)(c >> 8)) * 16777619u);
                }
            }
            return hash;
        }
    }

    /// <summary>
    /// Postfix on TAOM.Features.Arena.TournamentService.BuildPrizePool(string, PrizeBand) : MBList&lt;ItemObject&gt;
    /// (public instance; found by name, TAOM.dll is not referenced). Applied by RbmFixes with the campaign part,
    /// as an optional patch of its own. Stateless: the same module list gives the same pool on host, client and
    /// in single player, so nothing can differ between machines.
    /// </summary>
    internal static class TaomPrizePool
    {
        internal const string AssemblyName = "TAOM";
        internal const string TypeName = "TAOM.Features.Arena.TournamentService";
        internal const string MethodName = "BuildPrizePool";
        internal const string BandTypeName = "TAOM.Features.Arena.PrizeBand";

        /// <summary>Null while TAOM.dll is not loaded. Throws if the method is not there as expected.</summary>
        internal static MethodInfo FindTarget()
        {
            Assembly taom = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(a.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        taom = a;
                        break;
                    }
                }
                catch { }
            }
            if (taom == null)
                return null;
            Type type = taom.GetType(TypeName, false)
                ?? throw new MissingMemberException(TypeName + " not found in " + AssemblyName + ".dll");
            MethodInfo method = AccessTools.DeclaredMethod(type, MethodName)
                ?? throw new MissingMethodException(TypeName + "." + MethodName);
            ParameterInfo[] p = method.GetParameters();
            if (method.IsStatic || method.ReturnType != typeof(MBList<ItemObject>) || p.Length != 2
                || p[0].ParameterType != typeof(string) || p[1].ParameterType.FullName != BandTypeName)
                throw new MissingMethodException(TypeName + "." + MethodName + "(string, PrizeBand) returning MBList<ItemObject> has another shape now");
            return method;
        }

        internal static void Install(Harmony harmony, MethodInfo target)
        {
            harmony.Patch(target, postfix: new HarmonyMethod(
                typeof(TaomPrizePool).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic)) { priority = Priority.Last });
        }

        private static void Postfix(MBList<ItemObject> __result)
        {
            try
            {
                if (__result == null || PrizeSources.Revision == 0)
                    return; // no TAOM / Armory item list was seen: PrizeSelection replaces nothing either
                int removed = KeepEligible(__result, PrizeSelection.IsEligible);
                if (removed > 0)
                    RbmLog.WriteOnce("prizepool", "TAOM prize pool: " + removed + " of " + (removed + __result.Count)
                        + " items left out because the prize rules of this switch would replace them (first pool this session; later pools are filtered the same way).");
                else if (removed < 0)
                    RbmLog.WriteOnce("prizepool-empty", "TAOM prize pool: no item of a pool of " + __result.Count
                        + " passes the prize rules of this switch, so that pool is left as TAOM built it.");
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("prizepool-error", "TAOM prize pool: not changed (error caught): " + e.Message);
            }
        }

        /// <summary>
        /// Removes the items that fail the test, in place and keeping the order. Returns how many were removed;
        /// -1 (and the list untouched) when none would remain; 0 when nothing had to go.
        /// </summary>
        internal static int KeepEligible<T>(List<T> pool, Func<T, bool> eligible)
        {
            if (pool == null || eligible == null || pool.Count == 0)
                return 0;
            int keep = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (eligible(pool[i]))
                    keep++;
            }
            if (keep == pool.Count)
                return 0;
            if (keep == 0)
                return -1;
            int before = pool.Count;
            pool.RemoveAll(item => !eligible(item));
            return before - pool.Count;
        }
    }
}
