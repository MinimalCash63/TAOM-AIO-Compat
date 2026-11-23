# TAOM AIO Compatibility Patch

> [!CAUTION]
> **Read this before you download.**
> Download the mod here: [TAOM-AIO-Compat-v1.5.24.zip](https://github.com/MinimalCash63/TAOM-AIO-Compat/releases/download/v1.5.24/TAOM-AIO-Compat-v1.5.24.zip) (also under [Releases](https://github.com/MinimalCash63/TAOM-AIO-Compat/releases/latest)). Don't use the green Code → Download ZIP button; it has no mod in it. Drag the `TAOM-AIO-Compat` folder from the zip into your Bannerlord `Modules` folder.

Makes popular Bannerlord mods work with TAOM (Tales from the Age of Men) 2.0.41 or newer on Bannerlord 1.5.7, in single player and co-op. It doesn't edit or replace those mods; install them from their own pages.

Current version: v1.5.24. v1.5.23 and v1.5.24 have not been played in game yet.

> [!NOTE]
> **Libraries:** TAOM 2.0.41 provides Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu itself. Keep its four library entries ticked, right after TAOM Dependencies and above Native. Don't install separate copies or the old Nexus dependency patch.

> [!NOTE]
> **Not showing in the launcher?** The folder is probably one level too deep. `Modules\TAOM-AIO-Compat` must hold `SubModule.xml` and `bin` directly; if it holds another `TAOM-AIO-Compat` folder, move that one into `Modules`.

> [!NOTE]
> **RBM is not covered.** RBM's prices and gold amounts don't fit TAOM's, so this version has no RBM switch. Untick RBM before co-op.

> [!NOTE]
> **RTS Camera:** you don't need it with TAOM unless another mod requires it. TAOM has its own commander's camera (Seat of Seeing) and order system (Marshal's Word), and turns them off for a battle when another battle camera such as RTS Camera is present.

> [!NOTE]
> **Co-op:** every player needs the same version of this module, the same mods and the same switches. Limitless, its big-battle options, Diplomacy, AI Influence and Fourberie are single player only; untick them before co-op.

> [!NOTE]
> **Big battles with Limitless:** the battle-size settings set how many men can be on the field (field battles and sieges separately; sieges default to 6000), and the first-wave sliders set how many spawn at the start.

## What each switch does

Each mod has one switch in Mod Options (MCM), on the "TAOM AIO Compatibility Patch" page. A switch only does something when its mod is ticked. Most need a restart; the big-battle settings apply from the next battle.

- Retinues: its Troops button on TAOM's map bar, co-op guards, and its editor button on TAOM's encyclopedia pages.
- Hold the Line: its Depth morale works under TAOM, with a faster cavalry check for big battles.
- Bodyguards: no crash entering towns, castles and villages; its button on TAOM's hero page.
- TAOM Voices: lets it start on Bannerlord 1.5.7 in single player.
- Xorberax's Legacy: TAOM's trolls, mumakil, spiders and Sauron keep TAOM's own hit rules.
- Improved Garrisons: TAOM's party, food and money rules stay in charge, with its garrison costs on top.
- Dual Wield: TAOM's dual-wielding troops with a corrected troop list; no separate Dual Wield - TAOM module needed.
- Dismemberment Plus: dismemberment that fits TAOM's races; creatures are never cut.
- Party Quality: doesn't reveal parties that TAOM's Hidden Hosts keeps hidden.
- Limitless (single player): fewer crashes at the start of big battles, battle-size and first-wave settings, formation and reinforcement options, and keeps TAOM's creature mounts on the field.
- TAOM escape menu: every button fits in the pause menu.
- Diplomacy (single player): Diplomacy works inside TAOM's diplomacy rules; its civil wars are off.
- AI Influence (single player): doesn't cancel TAOM's alliances and wars, gives it a Middle-earth world, and stops its fief and clan moves.
- Fourberie (single player): guards against errors with TAOM's races and items; its recruit button offers TAOM troops.
- TAOM item names: an item's culture tag comes first, e.g. "[Mordor] Fine Uruk Sword VI".
- Tier Visual: weighted troops (×2, ×3) keep their tier colour in the party screen.
- Culture tags on troop names: TAOM's caravan troops, militia and spider riders get a culture tag, e.g. "[Erebor] Caravan Guard".

Some options of the covered mods that work against TAOM are held off while their switch is on. `README.txt` in the module folder has the details for every switch.

## Requirements

- Bannerlord 1.5.7
- TAOM 2.0.41 or newer, with TAOM.Dependencies

## Install

1. Install TAOM and the mods you want from their own pages.
2. Put the `TAOM-AIO-Compat` folder from the zip into Bannerlord's `Modules` folder.
3. In the launcher, tick "TAOM AIO Compatibility Patch" right after TAOM, above the mods it covers.

## Tested mods

Played with this module on Bannerlord 1.5.7, in this load order:

1. TAOM Dependencies 2.0.41 and its four library entries
2. Native, SandBoxCore, Sandbox, StoryMode, CustomBattle
3. LOTRLOME Armory 2.0.41
4. Dual Wield 0.10.8
5. TAOM Map 2.0.41
6. TAOM 2.0.41
7. TAOM AIO Compatibility Patch
8. Xorberax's Legacy 1.0.8.8
9. Diplomacy 1.5.7
10. AI Influence 7.0.6
11. Fourberie 1.5.7.0
12. Voices of the Waning Realm (TAOM Voices) 1.0.0
13. RTS Camera 5.5.4 and RTS Camera Command System 5.5.4 (not needed with TAOM, see the note above)
14. Bodyguards Reloaded 1.2.13
15. Retinues 2.0.0.16
16. Limitless 1.0.2.2
17. BattleSize Resized 2.0.4
18. Dismemberment Plus 2.0.8.8
19. Realistic Blood 1.1.7
20. Perfect Fire Arrows 6.8.2
21. Immersive Battlefields 3.7.0
22. More Formation Banners 1.0.7
23. Party Quality 1.2.3
24. Tier Visual 1.4.6
25. Improved Garrisons 4.3.1.0
26. Hold the Line b0.2.5
27. Bannerlord Co-op and TAOM.CoopCompat (co-op only)

## Known issues

- Very big sieges can still slow the game down once the walls break. Keep the siege battle size at 4000 to 8000.
- TAOM Voices doesn't play in conversations yet.
- Switches aren't compared between co-op players, so check that everyone uses the same ones.
- Bodyguards' settlement and hideout escorts don't appear on 1.5.7.

## Planned mods

- RBM: wanted, once its values fit TAOM's.
- Diplomacy and AI Influence in co-op: single player only for now.
- My Little Warband: doesn't run on Bannerlord 1.5.7 yet; leave it unticked.

## Reporting bugs

Open an issue with your mod list, single player or co-op, and `Documents\Mount and Blade II Bannerlord\Logs\taom_aio_compat.log` renamed to `.txt`. Remove your Steam id and user name first.

## Credits

- Retinues by bolaft ([Nexus 8847](https://www.nexusmods.com/mountandblade2bannerlord/mods/8847))
- TAOM compatibility patches by Theb0yys ([Nexus 13207](https://www.nexusmods.com/mountandblade2bannerlord/mods/13207)): the Xorberax's Legacy switch is based on his Xorberax patch, the AI Influence switch on his AI Influence patch, the Fourberie switch on his Fourberie patch. (Until v1.5.14 the Diplomacy switch was based on his Diplomacy patch; since v1.5.15 it does the opposite and holds none of it.) Parts based on Theb0yys' TAOM compatibility patches, used with his permission. His RBM patch is not used in this version (RBM is not covered, see "Planned mods").
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
- Tier Visual by Alex822007 ([Nexus 12004](https://www.nexusmods.com/mountandblade2bannerlord/mods/12004)). Files not included; install the original.
- Bannerlord Co-op by the Bannerlord Co-op team

None of these mods' files are included, apart from the Dual Wield - TAOM troop list, used with permission.

## License

Free to use, ask to copy. Use it in your own games, single player and co-op. Don't re-upload it or share changed copies without permission. See [LICENSE](LICENSE).
