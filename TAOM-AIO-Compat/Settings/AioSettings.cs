// MCM page: one switch per target mod (owner, 13 Nov 2026). Off means every part of that switch is off. Every switch needs a restart: the module
// reads the saved values at start-up (StartupSettings), because fixes are installed before MCM is ready.
// Property names are the keys in the saved json and must match each FixGroup.SettingKey.
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace TAOM_AIO_Compat.Settings
{
    public sealed class AioSettings : AttributeGlobalSettings<AioSettings>
    {
        internal const string SettingsId = "TAOM_AIO_Compat";
        internal const string SettingsFolder = "TAOM_AIO_Compat";
        private const string Group = "Fixes per mod (restart needed; each does nothing unless its mod is ticked)";

        public override string Id => SettingsId;
        public override string DisplayName => "TAOM AIO Compatibility Patch";
        public override string FolderName => SettingsFolder;
        public override string FormatType => "json2";

        [SettingPropertyGroup(Group, GroupOrder = 0)]
        [SettingPropertyBool("Retinues", Order = 0, RequireRestart = true,
            HintText = "Retinues v2 fixes: lets Retinues find the Harmony and UIExtenderEx libraries that TAOM.Dependencies bundles, and shows its Troops button on TAOM's map bar. On a co-op client Retinues builds and changes no troops locally (the host owns them; a joining player's clan should get no retinue of its own, see the README's Known issues), and during new-game set-up it waits with AI clan retinues until it is ready. Off removes all of it. WARNING: if you turn this off, untick Retinues in the launcher too, or Retinues v2 crashes the game at start-up.")]
        public bool RetinuesFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("RBM (Realistic Battle Mod)", Order = 1, RequireRestart = true,
            HintText = "RBM 4.8.2 under TAOM: TAOM's RBM balance for TAOM items and arrows (including the Harad quiver), TAOM's placeholder weapons keep their weapon descriptions, TAOM recruit and grain prices, RBM tournaments with TAOM troops and prizes (the prize pick is the same for every co-op player), crash guards on RBM's battle code (these can hide real RBM errors; each is logged once), a co-op guard so RBM's mercenary companies cannot stop the campaign tick on a co-op client, TAOM's tournament prize choice only offers prizes that are kept, RBM's caravans and mercenary companies are left to the host in co-op, and a readable RBM Changelog window. Copied from the TAOM team's RBM patch (Theb0yys). Untick RBM-TAOM-2.0.29.5: while it is ticked only the Changelog fix runs. Off removes all of this: RBM then runs without TAOM's balance (TAOM arrows do almost no damage with RBM combat on); no crash is known from that. In co-op every player needs the same value.")]
        public bool RbmFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Hold the Line", Order = 2, RequireRestart = true,
            HintText = "Hold the Line fixes: brings back its Depth morale feature under TAOM, in field battles only (formations with ranks behind them lose less morale) and removes its 'could not be set up' message. Hold the Line's own setting for Depth morale still applies. Off removes all of it; safe to turn off (Depth morale is then simply off). In co-op every player needs the same value.")]
        public bool HoldTheLineFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Bodyguards", Order = 3, RequireRestart = true,
            HintText = "Bodyguards fixes: stops Bodyguards from crashing the game in towns, castles and villages on Bannerlord 1.5.7. Its settlement and hideout escorts cannot spawn on 1.5.7, so they stay away; in battles where you are not the general, escort members who are not already on the field are not added either; Bodyguards' repeated error in those cases is gone and escorts already on the field are used. WARNING: if you turn this off, untick Bodyguards in the launcher too, or the game can crash when you enter a settlement.")]
        public bool BodyguardsFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("TAOM Voices", Order = 4, RequireRestart = true,
            HintText = "TAOM Voices fixes: lets TAOM Voices run on Bannerlord 1.5.7 in single player (its own build check was made for 1.4.8 and switched it off). In Co-op it stays off. If TAOM Voices fails while a campaign starts, the error is caught so loading does not repeat forever. Off: voices are off again. WARNING: if you turn this off, untick TAOM_Voices in the launcher too, to be safe from endless loading.")]
        public bool TaomVoicesFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Xorberax's Legacy", Order = 5, RequireRestart = true,
            HintText = "Xorberax's Legacy fixes: TAOM's trolls, mumakil and Sauron keep TAOM's own follow-through after a hit, instead of the value from Xorberax's Cut Through Everyone. TAOM's trolls, mumakil, war elephants, spiders and Sauron are also left out of Xorberax's Deadly Horse Charge and Deadly Combat. Deadly Combat no longer kills a troop that is already down, or one the game keeps from dying (conversations, cut scenes), and a bleeding troop that dies another way is not killed a second time. In co-op, Deadly Combat only acts on troops this machine owns, and the bank is not available to joining players (the host keeps it). Off: Xorberax's value applies to them too; nothing else changes, nothing to untick. In co-op every player needs the same value.")]
        public bool XorberaxLegacyFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Improved Garrisons", Order = 6, RequireRestart = true,
            HintText = "Improved Garrisons fixes for Bannerlord 1.5.7. Improved Garrisons 4.3.1.0 and newer fix their own recruitment, map banners and upgrade-path dialog; with the old 4.2.0.7 this switch repairs garrison recruitment, creates its recruiter, transfer and guard parties properly with their map banners, and keeps its 'specify upgrade path' dialog unavailable (a chat line says so). With every version: TAOM's party size, speed, food and clan finance rules stay in charge (Improved Garrisons' own models are not loaded); Improved Garrisons' garrison costs are added on top of TAOM's clan finances. In co-op its garrison costs are not charged on days Co-op blocks gold changes, and on a joining player's machine its hourly and daily routines do not run: the host's Improved Garrisons decides, so settings a joining player makes for their own fiefs do nothing and no garrison cost lines show there. Off removes all of it. WARNING: if you turn this off, untick Improved Garrisons in the launcher too: with 4.3.1.0 or newer its own finance and (by default) party size models then replace TAOM's, and its speed and food models too if switched on in its options; with the old 4.2.0.7 its recruitment does not work, its error repeats, and two of its clicks can crash the game. In co-op every player needs the same value.")]
        public bool ImprovedGarrisonsFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Dual Wield", Order = 7, RequireRestart = true,
            HintText = "Dual Wield fixes (replaces the separate Dual Wield - TAOM module): Dual Wield's dual-wielding TAOM troops use the corrected troop file, whose Harad Twin-Scimitar and Umbar Corsair Twin-Blade wear armour that exists in TAOM 2.0.39 and 2.0.40. Dual Wield's own 'NPC Dual-Wield Troops' option still decides whether these troops exist. Off removes all of it: Dual Wield then loads its own older file (armour ids TAOM 2.0.39 and 2.0.40 lack); nothing to untick. Do not untick Dual Wield in a campaign that already has these troops. In co-op every player needs the same value.")]
        public bool DualWieldFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("RTS Camera Command System", Order = 8, RequireRestart = true,
            HintText = "RTS Camera Command System fixes: it looks for RBM under an old module name and so treats RBM 4.8.x as not installed. With this on, and RBM ticked with its AI module on, the Command System is told RBM is there: a player formation that was sent to attack an enemy formation is no longer put on hold by the Command System when that enemy is gone, and infantry and foot archers leave the attack on a chosen formation to RBM's AI. The Command System's own 'after charge' option is not changed. Does nothing without RBM. TAOM's logs show its Marshal's Word and Seat of Seeing staying off in every battle while RTS Camera is ticked, so the Command System handles orders; this switch does not change that. Off: the Command System behaves as before; safe to turn off, nothing to untick. In co-op every player should use the same value.")]
        public bool RtsCommandFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Dismemberment Plus", Order = 9, RequireRestart = true,
            HintText = "Dismemberment Plus fixes: men, female elves and female Nazgul are dismembered the way Dismemberment Plus does it. Male elves, male Nazgul, orcs, uruks, Uruk-hai, berserkers, goblins, Saruman and dwarves can lose their head, made from their own head, hair and helmet; their arms and legs stay on (cut limbs would need new models for each race). The cut head shows the race's usual face, not that soldier's face, and there is no wound cap on the neck; a hood worn as a cape stays on the body. Trolls, Sauron and creatures are never cut. Its errors go to the AIO log instead of a Windows message box that stops the game, and its slow motion is off during co-op battles. Visual only: co-op players do not have to use the same value. WARNING: if you turn this off, untick DismembermentPlus in the launcher too, or an error in it stops the game behind a message box.")]
        public bool DismembermentPlusFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Party Quality", Order = 10, RequireRestart = true,
            HintText = "Party Quality fixes: while TAOM's Hidden Hosts hides a party or army (shown as 'Unknown party', or with a masked troop count), Party Quality adds no leader name and no quality rating to its tooltip; they show again once TAOM shows the full numbers. Does nothing unless Party Quality is ticked. Off: Party Quality shows its title for hidden parties too; safe to turn off, nothing to untick. Display only: co-op players do not have to use the same value.")]
        public bool PartyQualityFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("Limitless", Order = 11, RequireRestart = true,
            HintText = "Limitless fixes (single player only): Limitless raises the game's limit of 2040 soldiers and horses on the field. With BattleSizeResized ticked, this switch lets BattleSizeResized's first wave use Limitless' slots, and lets Limitless start even if it loads after BattleSizeResized. It also makes a crash at the start of a battle less likely when a first wave no longer fits Limitless' budget, for example after TAOM's mumakil crews spawn: about 1% of the slots are kept free when the first wave is sized, and if that is not enough the budget is raised for that one spawn, but only where there is room (a battle size below Limitless' own limit, or a siege below 2040); no soldier is held back. While Limitless is active, BattleSizeResized's 'Mission Agent Limit' has no effect; if you set Limitless' Agent Slot Limit to 2040 or less, keep that BattleSizeResized setting at 2040. The battle size below decides how many of Limitless' slots a battle uses. Does nothing during a co-op session; untick Limitless in the launcher before co-op (Co-op would also require every player to have it ticked). Off: Limitless still starts when it loads before BattleSizeResized, but every first wave keeps BattleSizeResized's 2040 limit, the battle size below is gone, and a crash at the start of a battle is more likely; untick Limitless as well. Limitless runs an unsigned program once per game start, at the first main menu, that changes the game in memory; antivirus tools may flag it.")]
        public bool LimitlessFixes { get; set; } = true;

        [SettingPropertyGroup(Group)]
        [SettingPropertyDropdown("Limitless battle size", Order = 12, RequireRestart = false,
            HintText = "How many soldiers and horses can be on the field at once in a battle with Limitless (single player). 2040 is the game's own limit, as with BattleSizeResized alone. Unlimited uses all of Limitless' slots (its 'Agent Slot Limit', or 'Siege Agent Slot Limit' in sieges, in Options > Performance). This never goes above Limitless' own setting. Takes effect from the next battle; no restart. Reinforcements still arrive as troops fall. Bigger battles need a strong PC.")]
        public Dropdown<string> LimitlessBattleSize { get; set; } = new Dropdown<string>(new[]
        {
            "2040 (game limit)", "3000", "4000", "6000", "Unlimited (all Limitless slots)"
        }, Fixes.Limitless.LimitlessFixes.DefaultSizeChoice);

        [SettingPropertyGroup(Group)]
        [SettingPropertyBool("TAOM escape menu", Order = 13, RequireRestart = true,
            HintText = "TAOM escape menu fix: TAOM's pause menu (Esc) has room for about 8 buttons. RBM, Retinues and Co-op add their own, so 'Exit to Main Menu' was hidden below the list (it showed only when you scrolled). With this on, the list grows to show every button when they do not fit, up to the free space in TAOM's frame (10 buttons); if there are even more, the list still scrolls and its scrollbar is made wider so you can see it. Nothing changes when all buttons already fit. TAOM's files are not changed. Off: TAOM's own layout; scroll the mouse wheel over the list to reach the last buttons. Display only: co-op players do not have to use the same value.")]
        public bool TaomEscapeMenuFixes { get; set; } = true;

        [SettingPropertyGroup("Warnings", GroupOrder = 1)]
        [SettingPropertyBool("Show start-up warnings", Order = 0, RequireRestart = true,
            HintText = "Shows a short in-game message when Co-op is ticked while you play single player, and when Limitless is ticked together with Co-op. Off: no such messages. Applies from the next game start.")]
        public bool StartupWarnings { get; set; } = true;
    }
}
