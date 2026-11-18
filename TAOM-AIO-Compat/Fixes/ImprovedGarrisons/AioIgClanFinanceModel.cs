// Clan finance model used instead of Improved Garrisons' GarrisonCostModel (owner, 13 Nov 2026: TAOM's rules apply).
// Every value comes from the model registered before it (BaseModel; under TAOM 2.0.39 that is
// TaomClanFinanceModel). Only the daily gold change gets IG's own garrison lines added on top (training,
// recruitment and recruiter costs, guard wages; IG's private CalculateImprovedGarrisonCosts, called on IG's own
// instance, no IG code copied).
// It derives from DefaultClanFinanceModel because the Co-op mod casts the active model to that type
// (BannerlordCoop bannerlord-1.5.x, GameInterface\Services\Clans\ClanFinance.cs line 215).
// Built only in OnGameStart (DefaultClanFinanceModel's static constructor needs Game.Current.GameTextManager).
// v1.4.0 (plan item 4, audit 14 Nov 2026): IG's lines are left out when the game's own CalculateClanGoldChange
// was skipped for this call (Co-op's gold gate), for the daily charge and for tooltips alike; see CoopGoldGate.cs.
// A tooltip then shows what will be charged that day: nothing from IG. Recruitment and training costs stay in
// IG's counters and are charged on the next day that is not gated.
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace TAOM_AIO_Compat.Fixes.ImprovedGarrisons
{
    internal sealed class AioIgClanFinanceModel : DefaultClanFinanceModel
    {
        private readonly ClanFinanceModel _fallback; // only when no model was registered before this one

        internal AioIgClanFinanceModel(ClanFinanceModel fallback)
        {
            _fallback = fallback;
        }

        private ClanFinanceModel B => BaseModel ?? _fallback;

        public override int PartyGoldLowerThreshold => B != null ? B.PartyGoldLowerThreshold : base.PartyGoldLowerThreshold;

        public override ExplainedNumber CalculateClanGoldChange(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
        {
            CoopGoldGate.Begin();
            ExplainedNumber result = B != null
                ? B.CalculateClanGoldChange(clan, includeDescriptions, applyWithdrawals, includeDetails)
                : base.CalculateClanGoldChange(clan, includeDescriptions, applyWithdrawals, includeDetails);
            if (CoopGoldGate.BaseWasSkipped(ImprovedGarrisonsFixes.GateLog))
                return result;
            return IgHelpers.AddIgCosts(clan, includeDescriptions, applyWithdrawals, result);
        }

        public override ExplainedNumber CalculateClanIncome(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false) =>
            B != null ? B.CalculateClanIncome(clan, includeDescriptions, applyWithdrawals, includeDetails)
                      : base.CalculateClanIncome(clan, includeDescriptions, applyWithdrawals, includeDetails);

        public override ExplainedNumber CalculateClanExpenses(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false) =>
            B != null ? B.CalculateClanExpenses(clan, includeDescriptions, applyWithdrawals, includeDetails)
                      : base.CalculateClanExpenses(clan, includeDescriptions, applyWithdrawals, includeDetails);

        public override ExplainedNumber CalculateTownIncomeFromTariffs(Clan clan, Town town, bool applyWithdrawals = false) =>
            B != null ? B.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals) : base.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals);

        public override int CalculateTownIncomeFromProjects(Town town) =>
            B != null ? B.CalculateTownIncomeFromProjects(town) : base.CalculateTownIncomeFromProjects(town);

        public override int CalculateVillageIncome(Clan clan, Village village, bool applyWithdrawals = false) =>
            B != null ? B.CalculateVillageIncome(clan, village, applyWithdrawals) : base.CalculateVillageIncome(clan, village, applyWithdrawals);

        public override int CalculateOwnerIncomeFromCaravan(MobileParty caravan) =>
            B != null ? B.CalculateOwnerIncomeFromCaravan(caravan) : base.CalculateOwnerIncomeFromCaravan(caravan);

        public override int CalculateOwnerIncomeFromWorkshop(Workshop workshop) =>
            B != null ? B.CalculateOwnerIncomeFromWorkshop(workshop) : base.CalculateOwnerIncomeFromWorkshop(workshop);

        public override float RevenueSmoothenFraction() =>
            B != null ? B.RevenueSmoothenFraction() : base.RevenueSmoothenFraction();

        public override int CalculateNotableDailyGoldChange(Hero hero, bool applyWithdrawals) =>
            B != null ? B.CalculateNotableDailyGoldChange(hero, applyWithdrawals) : base.CalculateNotableDailyGoldChange(hero, applyWithdrawals);
    }
}
