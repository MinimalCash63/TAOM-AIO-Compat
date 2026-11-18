// Helpers that Improved Garrisons' patched callers call instead of IG's broken methods, plus the model hooks.
// Every helper keeps the stack shape of the call it replaces (first parameter = the IG instance, typed object
// so this module needs no reference to ImprovedGarrisons.dll) and never throws.
using System;
using System.Collections.Generic;
using System.Reflection;
using TAOM_AIO_Compat.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TAOM_AIO_Compat.Fixes.ImprovedGarrisons
{
    internal static class IgHelpers
    {
        private delegate ExplainedNumber IgCostsDelegate(Clan clan, bool includeDescriptions, bool applyWithdrawals, ExplainedNumber currentValue);

        private static Action<string> _log;
        private static readonly HashSet<string> Logged = new HashSet<string>();

        // IG members read by reflection (resolved in Init; null = that part does nothing).
        private static FieldInfo _villageRecruitManagerField;   // PartyManager.villageRecruitPartyManagement
        private static MethodInfo _isVillageRecruitParty;       // VillageRecruitPartyManager.IsImprovedGarrisonVillageRecruitParty(MobileParty)
        private static PropertyInfo _mainCostModel;             // Main.GarrisonCostModel (static; new instance on every InitializeGame)
        private static MethodInfo _igCostsMethod;               // GarrisonCostModel.CalculateImprovedGarrisonCosts (private)
        private static PropertyInfo _configManagerInstance;     // ConfigManager.Instance (static)
        private static PropertyInfo _configManagerConfig;       // ConfigManager.Config

        // Campaign for which IG's InitializeGame has run (IG's cost lines are only added for that campaign).
        private static Campaign _igCampaign;
        private static object _costOwner;
        private static IgCostsDelegate _costs;
        private static volatile bool _enabled;

        internal static void Init(Assembly asm, Action<string> log)
        {
            _log = log;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            try
            {
                Type pm = asm.GetType("ImprovedGarrisons.AI.AIManagers.PartyManager", false);
                _villageRecruitManagerField = pm?.GetField("villageRecruitPartyManagement", all);
                _isVillageRecruitParty = _villageRecruitManagerField?.FieldType.GetMethod("IsImprovedGarrisonVillageRecruitParty", all, null, new[] { typeof(MobileParty) }, null);
                Type main = asm.GetType("ImprovedGarrisons.Main", false);
                _mainCostModel = main?.GetProperty("GarrisonCostModel", all);
                Type costModel = asm.GetType("ImprovedGarrisons.Models.GarrisonCostModel", false);
                _igCostsMethod = costModel?.GetMethod("CalculateImprovedGarrisonCosts", all, null,
                    new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(ExplainedNumber) }, null);
                Type cfg = asm.GetType("ImprovedGarrisons.SaveSystem.Configuration.ConfigManager", false);
                _configManagerInstance = cfg?.GetProperty("Instance", all);
                _configManagerConfig = cfg?.GetProperty("Config", all);
            }
            catch (Exception e) { Log("Init: IG member lookup error (affected parts do nothing): " + e.Message); }
            if (_isVillageRecruitParty == null)
                Log("VillageRecruitPartyManager.IsImprovedGarrisonVillageRecruitParty not found: banner tracking treats no party as a village recruit party.");
            if (_mainCostModel == null || _igCostsMethod == null)
                Log("Main.GarrisonCostModel / GarrisonCostModel.CalculateImprovedGarrisonCosts not found: IG's garrison cost lines will not be added.");
            _enabled = true;
        }

        internal static void Reset()
        {
            _enabled = false;
            _igCampaign = null;
            _costOwner = null;
            _costs = null;
        }

        internal static void OnGameEnd()
        {
            _igCampaign = null;
            _costOwner = null;
            _costs = null;
        }

        private static void Log(string message)
        {
            try { _log?.Invoke(message); } catch { }
        }

        private static void LogOnce(string key, string message)
        {
            lock (Logged)
            {
                if (!Logged.Add(key))
                    return;
            }
            Log(message);
        }

        // ---- replaces PartyManager.GetPartySizeLimit(PartyBase) ----
        // Same value the game's PartyBase.PartySizeLimit computes (1.5.7), without its cache; IG's original was
        // uncached too and returned 0 on error.
        internal static int GetPartySizeLimit(object self, PartyBase party)
        {
            try
            {
                if (party == null)
                    return 0;
                PartySizeLimitModel model = Campaign.Current?.Models?.PartySizeLimitModel;
                if (model == null)
                    return 0;
                bool atSea = party.IsMobile && party.MobileParty != null && party.MobileParty.IsCurrentlyAtSea;
                return (int)model.GetPartyMemberSizeLimit(party, atSea, false).ResultNumber;
            }
            catch (Exception e)
            {
                LogOnce("size", "Party size helper error (returns 0 like IG's original; logged once): " + e);
                return 0;
            }
        }

        // ---- replaces PartyManager.TrackPartyWithBanner(MobileParty) ----
        // IG's own conditions (player clan, not a village recruit party), then 1.5.7's MapTrackerManager.
        internal static bool TrackPartyWithBanner(object self, MobileParty party)
        {
            try
            {
                MapTrackerManager trackers = Campaign.Current?.MapTrackerManager;
                if (party == null || trackers == null || party.ActualClan != Clan.PlayerClan || IsVillageRecruitParty(self, party))
                    return false;
                trackers.AddMapTracker(party);
                return true;
            }
            catch (Exception e)
            {
                LogOnce("track", "Banner tracker add error (caught, logged once): " + e.Message);
                return false;
            }
        }

        // ---- replaces PartyManager.UntrackPartyWithBanner(MobileParty) ----
        internal static bool UntrackPartyWithBanner(object self, MobileParty party)
        {
            try
            {
                MapTrackerManager trackers = Campaign.Current?.MapTrackerManager;
                if (party == null || trackers == null)
                    return false;
                trackers.RemoveMapTracker(party); // does nothing when the party is not tracked
                return true;
            }
            catch (Exception e)
            {
                LogOnce("untrack", "Banner tracker remove error (caught, logged once): " + e.Message);
                return false;
            }
        }

        private static bool IsVillageRecruitParty(object partyManager, MobileParty party)
        {
            try
            {
                object mgr = partyManager == null ? null : _villageRecruitManagerField?.GetValue(partyManager);
                return mgr != null && _isVillageRecruitParty != null && (bool)_isVillageRecruitParty.Invoke(mgr, new object[] { party });
            }
            catch { return false; }
        }

        // ---- replaces TrainingSettings.SpecifyUpgradePath(bool) in its two dialog callers ----
        internal static void SpecifyUpgradePathUnavailable(object self, bool addNewUnits)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage("Improved Garrisons: specifying upgrade paths is not available on Bannerlord 1.5.7."));
                LogOnce("dialog", "'Specify upgrade path' used: chat line shown instead (IG's dialog cannot run on 1.5.7).");
            }
            catch { }
        }

        // ---- models ----

        // Prefix on IG's Main.AddModels: IG adds none of its models (size, speed, food, cost). Always skips.
        internal static bool AddModelsPrefix()
        {
            try
            {
                object cfg = _configManagerConfig?.GetValue(_configManagerInstance?.GetValue(null));
                if (cfg != null)
                {
                    bool speed = ReadBool(cfg, "LoadCustomPartySpeedModel");
                    bool food = ReadBool(cfg, "LoadFoodGatheringModule");
                    if (speed || food)
                        Log("IG's config has " + (speed ? "its party speed model" : "") + (speed && food ? " and " : "") + (food ? "its food gathering model" : "")
                            + " switched on; not added: TAOM's speed and food models stay in charge.");
                }
                LogOnce("addmodels", "IG's AddModels skipped (first time this session): TAOM's party size, speed, food and clan finance models stay.");
            }
            catch { }
            return false;
        }

        private static bool ReadBool(object obj, string property)
        {
            try { return obj.GetType().GetProperty(property)?.GetValue(obj) is bool b && b; }
            catch { return false; }
        }

        // Postfix on IG's Main.InitializeGame: IG has (re)built its managers and a new GarrisonCostModel.
        internal static void InitializeGamePostfix()
        {
            try
            {
                _igCampaign = Campaign.Current;
                _costOwner = null; // read Main.GarrisonCostModel again on the next call
                _costs = null;
            }
            catch { }
        }

        // Called from OnGameStart only (Campaign game, CampaignGameStarter): DefaultClanFinanceModel's static
        // constructor needs Game.Current.GameTextManager, so no finance model is built anywhere else.
        internal static void RegisterFinanceWrapper(CampaignGameStarter starter)
        {
            try
            {
                ClanFinanceModel previous = starter.GetModel<ClanFinanceModel>();
                ClanFinanceModel fallback = previous == null ? new DefaultClanFinanceModel() : null;
                starter.AddModel<ClanFinanceModel>(new AioIgClanFinanceModel(fallback));
                Log("Clan finance wrapper added on top of " + (previous?.GetType().FullName ?? "a default model") + " (IG's garrison cost lines are added after it).");
            }
            catch (Exception e)
            {
                Log("Clan finance wrapper not added (TAOM's model stays; IG's garrison costs are not charged this game): " + e.Message);
            }
        }

        // IG's own cost lines on top of the base model's gold change. Main.GarrisonCostModel is read on every
        // call (IG makes a new one on every InitializeGame); the delegate is rebuilt when it changes.
        internal static ExplainedNumber AddIgCosts(Clan clan, bool includeDescriptions, bool applyWithdrawals, ExplainedNumber current)
        {
            if (!_enabled || _igCostsMethod == null || _mainCostModel == null)
                return current;
            try
            {
                Campaign campaign = Campaign.Current;
                if (clan == null || campaign == null || !ReferenceEquals(campaign, _igCampaign) || Hero.MainHero == null)
                    return current;
                object owner = _mainCostModel.GetValue(null);
                if (owner == null)
                    return current;
                if (!ReferenceEquals(owner, _costOwner) || _costs == null)
                {
                    _costs = (IgCostsDelegate)Delegate.CreateDelegate(typeof(IgCostsDelegate), owner, _igCostsMethod);
                    _costOwner = owner;
                }
                return _costs(clan, includeDescriptions, applyWithdrawals, current);
            }
            catch (Exception e)
            {
                LogOnce("costs", "IG garrison cost lines error (TAOM's value kept; logged once): " + e);
                return current;
            }
        }
    }
}
