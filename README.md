# TAOM AIO Compatibility Patch

> [!CAUTION]
> **Important: read this before you download.**
>
> 1. Download the mod here: **[TAOM-AIO-Compat-v1.5.17.zip](https://github.com/MinimalCash63/TAOM-AIO-Compat/releases/download/v1.5.17/TAOM-AIO-Compat-v1.5.17.zip)**. It's also under [Releases](https://github.com/MinimalCash63/TAOM-AIO-Compat/releases/latest), on the right side of this page.
> 2. Do **not** use the green **Code → Download ZIP** button. It only gives you this page's text, not the mod, so the launcher won't show anything.
> 3. Open the zip and drag the `TAOM-AIO-Compat` folder into your Bannerlord `Modules` folder. Done.
>
> Check: inside `Modules\TAOM-AIO-Compat` you should see `SubModule.xml` and a `bin` folder right away. If you don't, see "AIO not showing in the launcher?" below.

Makes popular Bannerlord mods work together with TAOM (Tales from the Age of Men) 2.0.41 or newer on Bannerlord 1.5.7 (checked with 2.0.41), in single player and with the Co-op mod.

It doesn't replace or edit any of those mods. Install them from their own pages, then add this module.

Download: [Releases](../../releases). Current version: v1.5.17.

> [!IMPORTANT]
> **RBM (Realistic Battle Mod) is not covered in this version.** The RBM switch and the RTS Camera Command System switch are gone since v1.5.17. With RBM, the prices and gold amounts of TAOM and RBM do not fit together, and MinimalCash63 plans to wait until those values are fixed or updated. See "Planned and wanted mods" below, also for what happens if you keep RBM ticked. Untick RBM before hosting or joining co-op.

> [!NOTE]
> **Libraries:** TAOM 2.0.41 provides Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu itself. Keep its four library entries ticked, right after TAOM Dependencies and above Native. Do not install separate copies of those four libraries or the old Nexus dependency patch.

> [!WARNING]
> **AIO not showing in the launcher? It's probably one folder too deep.**
> Windows' "Extract All" puts the files in a new folder named after the zip, so you end up with `Modules\TAOM-AIO-Compat-v1.5.17\TAOM-AIO-Compat\`. The launcher only looks one folder deep and won't find it.
> Open `Modules\TAOM-AIO-Compat`: you should see `SubModule.xml` and a `bin` folder right there. If you see another `TAOM-AIO-Compat` folder inside instead, move that inner folder into `Modules`.

## How the switches work

Each mod has its own switch in Mod Options (MCM), on the "TAOM AIO Compatibility Patch" page.

- A switch only does something when its mod is ticked in the launcher.
- Changing a switch needs a game restart. Only "Limitless battle size" works from the next battle.
- In co-op, everyone should use the same switches.

## What each switch does

- **Retinues**: Retinues' Troops button sits on TAOM's map bar. In co-op only the host builds and changes retinues. In single player, Retinues' "AI Clans' Custom Troops" option is turned off, so AI clans' fiefs offer TAOM's recruits (see "Settings this module turns off"). If you turn the switch off, Retinues still starts, without that button and without the co-op guards; do not use Retinues in co-op with the switch off.
  - New in v1.5.16, single player only: Retinues' own troop editor button shows on TAOM's hero and troop encyclopedia pages, under the bookmark. It has its own option, "Retinues' editor button on TAOM's encyclopedia pages".
- **Hold the Line**: its Depth morale works under TAOM in field battles.
- **Bodyguards**: no more crash when entering towns, castles and villages on 1.5.7. If you turn it off, untick Bodyguards too.
  - New in v1.5.16, single player only: Bodyguards' own button shows on TAOM's hero encyclopedia page. It has its own option, "Bodyguards' button on TAOM's hero page".
- **TAOM Voices**: lets TAOM Voices start on Bannerlord 1.5.7 in single player (see Known issues). It stays off in co-op.
- **Xorberax's Legacy**: TAOM's trolls, mumakil, war elephants, spiders and Sauron can't be one-shot by Deadly Horse Charge or Deadly Combat, and keep TAOM's own hit rules. Deadly Combat no longer kills a troop that is already down. In co-op the bank stays with the host.
- **Improved Garrisons**: TAOM's party size, speed, food and money rules stay in charge, with Improved Garrisons' garrison costs on top. If you turn it off, untick Improved Garrisons too.
- **Dual Wield**: TAOM's dual-wielding troops, with a corrected troop list for TAOM 2.0.39 and 2.0.40. You don't need the separate Dual Wield - TAOM module.
- **Dismemberment Plus**: men, female elves and female Nazgul are dismembered as normal. Male elves, male Nazgul, orcs, uruks, Uruk-hai, berserkers, goblins, Saruman and dwarves can lose their head, but not arms or legs. Trolls, Sauron and creatures are never cut. Its slow motion is off in co-op battles.
- **Party Quality**: doesn't give away parties and armies that TAOM's Hidden Hosts keeps hidden.
- **Limitless** (single player only): makes a crash at the start of big battles with TAOM less likely, and adds a "Limitless battle size" choice (2040, 3000, 4000, 6000 or Unlimited). Untick Limitless before co-op.
- **TAOM escape menu**: the pause menu (Esc) shows every button, so "Exit to Main Menu" is never hidden.
- **Diplomacy** (single player only): TAOM's diplomacy rules hold and Diplomacy works inside them. TAOM's alliance score is added to Diplomacy's, so AI kingdoms on opposite sides do not ally. Diplomacy's peace (war exhaustion, a peace vote, its Make Peace button) is skipped, without reparations or returned fiefs, where TAOM does not allow peace. Diplomacy's civil wars are turned off (see "Settings this module turns off"). Until v1.5.14 this switch did the opposite and relaxed TAOM's rules. If Theb0yys' Diplomacy patch module is ticked, this switch applies nothing and his patch's rules apply. Untick Diplomacy before co-op.
  - New in v1.5.16: TAOM's messenger rules also hold when you talk to a lord through a Diplomacy messenger. In normal play this does nothing, because TAOM's hero page has no Diplomacy messenger button; it only matters for a messenger already under way in an older save, or if TAOM's page falls back to the game's own. Not checked in a running game.
- **AI Influence** (single player only): with its diplomacy option on, AI Influence no longer cancels TAOM's own alliances, wars and peace (its permanent alliances, the War of the Ring, war pledges); AI kingdoms' own decisions stay under AI Influence's control. Its setting "Middle-earth world for AI Influence" (on by default) gives AI Influence a Middle-earth world in every campaign: world description, facts, secrets, faiths, customs, character types and speech habits, plus written personalities for 90 characters (31 rulers, 59 famous lords and companions). It only replaces AI Influence files that are still at their default and only fills empty personality fields; turning it off stops further changes but does not undo them. This module holds only its own text: the prompt templates, character types and speech habits are AI Influence 7.0.6's own files on your PC with our changes placed into them, so with another AI Influence version those files are left as they are. Untick AI Influence before co-op.
  - New in v1.5.16: four actions of AI Influence's rulers are stopped: transferring a town or castle to another kingdom, granting a fief, confiscating a fief and expelling a clan. The fief or clan does not move; the announcement and AI Influence's own relation changes for it can still happen (not checked in game). AI Influence's own wars, peace, alliances, trade, tribute and kingdom laws are back with its option on. This has its own option, "AI Influence: stop fief and clan moves"; with it off, or if the guard cannot be set up, AI Influence's whole diplomacy option ("Enable Diplomacy System") is held off as in v1.5.15 (see "Settings this module turns off").
- **Fourberie** (single player only): guards for four places where Fourberie or the game can throw errors with TAOM's races and items: Fourberie's fist-fight bonus, the daily food count, party map icons, and dragging corpses in Fourberie's stealth missions. Its recruit button in towns offers TAOM's hired troops of that town's people (troops above level 21 are left out, so Erebor, Lindon, Rivendell, Lothlorien and Mirkwood towns offer nothing through this button). Untick Fourberie before co-op.
- **TAOM item names: tag first** (new in v1.5.16; single player and co-op): an item with a rarity shows its culture tag first, for example "[Mordor] Fine Uruk Sword VI" in place of "Fine [Mordor] Uruk Sword VI", in the inventory, tooltips, trade, loot and for weapons on the ground. Only the order of the name changes. It is display text on your PC, so co-op players do not need the same setting (not yet run in co-op). Safe to turn off.

This module has no switch for RBM, RTS Camera or RTS Camera's Command System.

More detail for each switch is in `README.txt` inside the module folder.

## Settings this module turns off

Some options of the covered mods work against TAOM. This module disables them by default: it turns each one off by itself, in memory, at each game start (main menu) and at each campaign start or load, while that mod's switch and the setting's own entry under it are on. You should keep them off.

| Mod | Setting (where to find it) | Held at | Why |
|---|---|---|---|
| Diplomacy | Mod Options, Diplomacy, Civil Wars: "Daily Chance To Start Rebel Faction" and "Daily Chance To Start Civil War" | 0% | Rebel kingdoms are unknown to TAOM's sides. |
| Retinues | Retinues' settings, Recruitment: "AI Clans' Custom Troops" | Off | AI clans' fiefs would offer clan troops in place of TAOM's recruits. Your own fiefs are not affected. |
| AI Influence | Mod Options, AI Influence, Diplomacy: "Enable Diplomacy System" | Off, but only when this module's option "AI Influence: stop fief and clan moves" is off or its guard could not be set up (since v1.5.16) | That system can move fiefs and clans between kingdoms without TAOM's checks. With the guard on, the option keeps your own value. |

The Retinues row applies in single player only; Diplomacy and AI Influence are single-player switches anyway.

If AI Influence saved "Enable Diplomacy System" as off while v1.5.15 held it, switch it on again in AI Influence's options (Diplomacy).

If you turn one back on in the other mod's own options:

- Diplomacy: it applies at once and is 0% again at the next campaign start or load.
- Retinues: you can't; Retinues does not show an option that is held this way.

None of this needs a restart. A setting is also held again each time the main menu comes up, so a value you set in the other mod's options at the main menu does not reach a campaign.

To keep one on: in this module's options, turn off its entry under the mod's switch ("Turn off ...") and restart the game. That mod's own stored value then applies.

What the other mod's own menu shows and stores: this module writes none of these settings to any file. While a setting is held, that mod's own options screen shows it as off (0% for Diplomacy; Retinues hides the option). If you change that mod's own options while a setting is held, that mod can save it as off: Diplomacy and AI Influence save all their values when you change any of them in Mod Options, and Retinues saves all its settings when you change any of them. If that happened and you later turn this module's entry off, set the option again in that mod's own options.

The Diplomacy hold was seen working in the log of a v1.5.15 game. The Retinues hold and the AI Influence guard have not been run in game yet.

## Tested mod list

These are the mods and versions we played with this module on Bannerlord 1.5.7, in this load order. Other versions may work, but these are the ones we know load and play together.

The last version of this module played in game is v1.5.15 (two single-player sessions on 21 Nov 2026, with a battle and a tournament). **v1.5.16 and v1.5.17 have not been run in game yet**; they were checked offline against the real game and mod files. Nothing new in v1.5.16 (item names, encyclopedia buttons, messenger rule, AI Influence guard) and nothing of v1.5.17 (RBM removed, the RBM notice) has been seen in a running game. In the v1.5.15 sessions this module's Retinues and TAOM Voices switches were off, and RBM 4.8.2 was ticked with the RBM switch that this version no longer has.

1. TAOM Dependencies 2.0.41 with its four library entries (Harmony, ButterLib, UIExtenderEx, Mod Configuration Menu)
2. Native, SandBoxCore, Sandbox, StoryMode, CustomBattle
3. LOTRLOME Armory 2.0.41
4. Dual Wield 0.10.8
5. TAOM Map 2.0.41
6. TAOM 2.0.41
7. TAOM AIO Compatibility Patch (last played: 1.5.15, see above)
8. Xorberax's Legacy 1.0.8.8
9. Diplomacy 1.5.7 (single player only)
10. AI Influence 7.0.6 (single player only)
11. Fourberie 1.5.7.0 (single player only)
12. Voices of the Waning Realm (TAOM Voices) 1.0.0 (see Known issues)
13. RTS Camera 5.5.4
14. RTS Camera Command System 5.5.4
15. Bodyguards Reloaded 1.2.13
16. Retinues 2.0.0.16
17. Limitless 1.0.2.2 (single player only)
18. BattleSize Resized 2.0.4
19. Dismemberment Plus 2.0.8.8
20. Realistic Blood 1.1.7
21. Perfect Fire Arrows 6.8.2
22. Immersive Battlefields 3.7.0
23. More Formation Banners 1.0.7
24. Party Quality 1.2.3
25. Tier Visual 1.4.6
26. Improved Garrisons 4.3.1.0
27. Hold the Line b0.2.5
28. Bannerlord Co-op and TAOM.CoopCompat 2.0.40 (co-op only; untick both for single player)

Co-op was last played with v1.5.11 and older, with a development build of Co-op for Bannerlord 1.5.x. Every player must use the same Co-op build. No co-op session has been run on v1.5.12 or newer.

## Requirements

- Bannerlord 1.5.7
- TAOM 2.0.41 or newer, with TAOM.Dependencies (checked with 2.0.41). From 2.0.41 on, TAOM provides Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu itself through TAOM.Dependencies; older TAOM versions do not have those four entries of their own and are not covered.

## Install

1. Install TAOM as its page describes, and the mods you want from their own pages.
2. In the launcher, keep TAOM's four library entries ticked: Harmony, ButterLib, UIExtenderEx and MBOptionScreen / MCM, each named "(provided by TAOM.Dependencies)", right after TAOM Dependencies and above Native. TAOM 2.0.41 installs them itself. Do not install separate copies of those four libraries or the old Nexus dependency patch.
3. Download the zip from Releases, put the `TAOM-AIO-Compat` folder in Bannerlord's `Modules` folder, and tick "TAOM AIO Compatibility Patch" right after TAOM, above the mods it covers.

## Co-op

- Every player needs the same version of this module, the same mods and the same switches.
- Untick Limitless, Diplomacy, AI Influence and Fourberie before hosting or joining.
- Untick RBM before hosting or joining (see "Planned and wanted mods").

## Planned and wanted mods

Not in this version:

- **RBM (Realistic Battle Mod)**: wanted, not in this version. With RBM, the prices and gold amounts of TAOM and RBM do not fit together. For example, computed from the installed files with the TAOM team's RBM values (not measured in game): a White Warg costs 150,119 with RBM and 8,830 in TAOM alone, while no TAOM weapon or armour costs more than 50,000 with RBM and TAOM's best gear costs 100,000 to 287,000 in TAOM alone. MinimalCash63 plans to wait until those values are fixed or updated. Until then this module does nothing for RBM; see the start-up notice.
  - If you keep RBM ticked: RBM runs with TAOM as it does without this module, with its own item values, prices, tournaments and troop lists. TAOM arrows and bolts do very little damage with RBM combat on. This comes from reading code and logs; it has not been run in a game. To play RBM with TAOM, use the TAOM team's own RBM patch (module TAOM_Standalone_Compat_RBM, from the [TAOM compatibility patches page](https://www.nexusmods.com/mountandblade2bannerlord/mods/13207)) and check there which TAOM version it is for. It is a separate mod and not part of this one. Its v1.0.13, made for TAOM 2.0.40, holds item values, the arrow format, grain and recruit prices, RBM tournaments with TAOM troops and guards on RBM's battle code. It has no co-op guards and does not keep the vanilla troops hidden that TAOM hides.
  - RBM and co-op: untick RBM before hosting or joining. On a joining player's machine RBM's mercenary companies and kingdom caravans stop the campaign day from running properly (seen in logs with RBM 4.8.1; RBM 4.8.2 has the same code). Up to v1.5.16 this module guarded that; this version does not.
  - Coming from v1.5.16 or older: a campaign that goes on with RBM ticked gets RBM's prices, tournaments and troop lists from the next load. The RBM and RTS Camera Command System settings left in this module's saved options file do nothing. With RTS Camera ticked as well, its Command System treats RBM 4.8.x as not installed, as it did before this module had a switch for it.
  - Start-up notice: when RBM is ticked, a notice at the main menu, once per game start, says that this version has no RBM part and, with Coop ticked, to untick RBM before co-op. It is not shown when the TAOM team's RBM patch is ticked and Coop is not. Turn it off in Mod Options with "Show start-up warnings".
  - Authors: Realistic Battle Mod (RBM) by Marnah93 and Philozoraptor ([Nexus 791](https://www.nexusmods.com/mountandblade2bannerlord/mods/791)); the RBM patch for TAOM by Theb0yys (TAOM team). Neither is covered by or included in this version.
- **Diplomacy in co-op.** For now the Diplomacy switch is single player only: untick Diplomacy before hosting or joining co-op.
- **AI Influence in co-op.** For now the AI Influence switch is single player only: untick AI Influence before hosting or joining co-op.
- **My Little Warband.** Its 1.2.7 version does not run on Bannerlord 1.5.7, and a fix needs its author's OK first. Until then, leave My Little Warband unticked.

## Single player with Co-op ticked

While Coop is ticked, TAOM uses the game's standard UI instead of its own, even in single player. Co-op also adds its own characters to your save. For single player, untick Coop and TAOM.CoopCompat, and keep separate saves for single player and co-op. The module reminds you with a short message in game; you can turn these reminders off in Mod Options ("Show start-up warnings").

## Known issues

- v1.5.16 and v1.5.17 have not been run in game yet (see "Tested mod list").
- Encyclopedia buttons (v1.5.16): the Retinues and Bodyguards buttons are not on TAOM's kingdom and clan pages. If a button cannot be built, TAOM shows the game's own page until you restart; turn that button's option off if this happens. How the buttons look on TAOM's pages has not been checked in game yet.
- AI Influence (v1.5.16): kingdom commands in talks (joining or leaving a kingdom, handing over a kingdom, granting a fief) and its clan rebellion event are not stopped. A territory demand that was stopped can stay pending in AI Influence. Another mod using the same AI Influence hook could allow an action again. Whether a real ruler statement reaches this module's guard has not been checked in a running game yet.
- TAOM Voices doesn't play in conversations yet. The game doesn't crash.
- Retinues may still create a default retinue for a player joining a co-op game.
- Switches aren't compared between co-op players, so make sure everyone uses the same ones.
- Bodyguards' settlement and hideout escorts don't appear on 1.5.7.
- More Formation Banners' banner-bearer interval setting has no effect under TAOM.
- Settings this module turns off show as off in the other mod's own options, and that mod can save them as off (see "Settings this module turns off").
- Diplomacy: a campaign saved with v1.5.14 or older can keep wars between kingdoms of one side and alliances between hostile kingdoms until they end by themselves.
- Campaigns saved with v1.5.15 or v1.5.16 and RBM: mercenary companies keep their Middle-earth names.
- Fourberie's recruit button offers nothing in Erebor, Lindon, Rivendell, Lothlorien and Mirkwood towns.
- Diplomacy's messenger button is not shown on TAOM's own encyclopedia pages.

## Reporting bugs

Open an issue here with your mod list, whether it was single player or co-op, and the log `Documents\Mount and Blade II Bannerlord\Logs\taom_aio_compat.log` (rename it to `.txt` before attaching). Remove your Steam id and user name first.

## Credits

- Retinues by bolaft ([Nexus 8847](https://www.nexusmods.com/mountandblade2bannerlord/mods/8847))
- TAOM compatibility patches by Theb0yys ([Nexus 13207](https://www.nexusmods.com/mountandblade2bannerlord/mods/13207)): the Xorberax's Legacy switch is based on his Xorberax patch, the AI Influence switch on his AI Influence patch, the Fourberie switch on his Fourberie patch. (Until v1.5.14 the Diplomacy switch was based on his Diplomacy patch; since v1.5.15 it does the opposite and holds none of it.) Parts based on Theb0yys' TAOM compatibility patches, used with his permission. His RBM patch is not used in this version (RBM is not covered, see "Planned and wanted mods").
- Hold the Line - Infantry AI by UirosRios ([Nexus 14409](https://www.nexusmods.com/mountandblade2bannerlord/mods/14409))
- Bodyguards - Companion Escort Overhaul by Exiur, based on Bodyguards by carbon198 ([Nexus 9492](https://www.nexusmods.com/mountandblade2bannerlord/mods/9492))
- Voices of the Age of Men by Theb0yys ([Nexus 13574](https://www.nexusmods.com/mountandblade2bannerlord/mods/13574))
- Xorberax's Legacy and Dismemberment Plus by TheSchwarz1, original mods by Xorberax ([Nexus 3462](https://www.nexusmods.com/mountandblade2bannerlord/mods/3462)), used with his permission. None of his files are included; get his mods from their own pages.
- Improved Garrisons by Sidies ([Nexus 688](https://www.nexusmods.com/mountandblade2bannerlord/mods/688))
- Party Quality by ormuszeus ([Nexus 12979](https://www.nexusmods.com/mountandblade2bannerlord/mods/12979))
- Limitless by BlocTheWorker ([Nexus 14025](https://www.nexusmods.com/mountandblade2bannerlord/mods/14025)), used with his permission. No Limitless file is included; get it from its Nexus page.
- Diplomacy by the Diplomacy Team ([Nexus 832](https://www.nexusmods.com/mountandblade2bannerlord/mods/832))
- AI Influence by MFive ([Nexus 9711](https://www.nexusmods.com/mountandblade2bannerlord/mods/9711))
- Fourberie by Spinozart1 ([Nexus 2969](https://www.nexusmods.com/mountandblade2bannerlord/mods/2969))
- BattleSize Resized by Vombora ([Nexus 8177](https://www.nexusmods.com/mountandblade2bannerlord/mods/8177))
- Dual Wield and Dual Wield - TAOM by the TAOM mod developers
- Bannerlord Co-op by the Bannerlord Co-op team

None of these mods' files are included, apart from the Dual Wield - TAOM troop list, used with permission.

## License

Free to use, ask to copy. Use it in your own games, single player and co-op. Don't re-upload it or share changed copies without permission. See [LICENSE](LICENSE).
