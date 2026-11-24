# TAOM AIO Compatibility Patch

> [!CAUTION]
> **Read this before you download.**
> Download the mod here: [TAOM-AIO-Compat-v1.5.52.zip](https://github.com/MinimalCash63/TAOM-AIO-Compat/releases/download/v1.5.52/TAOM-AIO-Compat-v1.5.52.zip) (also under [Releases](https://github.com/MinimalCash63/TAOM-AIO-Compat/releases/latest)). Don't use the green Code → Download ZIP button; it has no mod in it. Drag the `TAOM-AIO-Compat` folder from the zip into your Bannerlord `Modules` folder.

Website: [minimalcash63.github.io/TAOM-MODS](https://minimalcash63.github.io/TAOM-MODS/), with both TAOM mods and a link to Coodsz's TAOM co-op tools.

Makes popular Bannerlord mods work with TAOM (Tales from the Age of Men) 2.0.41 or newer on Bannerlord 1.5.7, in single player and co-op. Checked with TAOM 2.0.42. It doesn't edit or replace those mods; install them from their own pages.

Current version: v1.5.52. Played in single player on v1.5.51. Many of the parts that are new since v1.5.24 were checked against the real game and mod files but have not each been seen in a running game yet.

> [!NOTE]
> **Libraries:** TAOM provides Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu itself. Keep its four library entries ticked, right after TAOM Dependencies and above Native. Don't install separate copies or the old Nexus dependency patch.

> [!NOTE]
> **Not showing in the launcher?** The folder is probably one level too deep. `Modules\TAOM-AIO-Compat` must hold `SubModule.xml` and `bin` directly; if it holds another `TAOM-AIO-Compat` folder, move that one into `Modules`.

> [!NOTE]
> **RBM is back, with TAOM's balance.** One switch makes RBM 4.8.2 run on TAOM's items, prices and money (single player only). Tick RBM near the end of the list, after Hold the Line. Start a new campaign the first time you play with it. Untick RBM before co-op.

> [!NOTE]
> **TAOM Voices is on hold.** The mod author has hidden the mod; support returns when it is re-released. Its switch stays in the menu and does nothing while TAOM Voices isn't installed.

> [!NOTE]
> **RTS Camera:** you don't need it with TAOM unless another mod requires it. TAOM has its own commander's camera (Seat of Seeing) and order system (Marshal's Word), and turns them off for a battle when another battle camera such as RTS Camera is present.

> [!NOTE]
> **Co-op:** every player needs the same version of this module, the same mods and the same switches. Limitless and its big-battle options, Diplomacy, AI Influence, Fourberie and RBM are single player only; untick them before co-op.

> [!NOTE]
> **Big battles with Limitless:** the battle-size settings set how many men can be on the field (field battles and sieges separately; sieges default to 6000), and the first-wave sliders set how many spawn at the start.

## What's new since v1.5.24

- RBM: back as one switch with TAOM's balance (see "What each switch does"). Its battle parts start at the first battle, so battles no longer stay on the loading screen with RBM ticked.
- Update notice: the module tells you at the main menu when a newer version is out (see "Update notice").
- Bodyguards: escorts in towns, castles and villages work again, and hideout escorts join before the boss fight (single player). Guard pay is part of the daily wage, your culture's own guard troop comes first, TAOM's creatures are not picked as guards, and no escort walks into an enemy-held settlement.
- Diplomacy: works inside TAOM's rules on its kingdom buttons, fiefs in peace terms follow TAOM's sides, TAOM's scripted wars gain no war-weariness, TAOM's fief vote runs first, and its English texts use Middle-earth words. Each part has its own setting.
- Fourberie: recruits are TAOM troops of the town's people at TAOM's prices, castle stealth works in TAOM's castles, realm rulers are protected from its schemes, and its bandit bands take TAOM's real bandit troops.
- Middle-earth sellswords: four new mercenary companies in new campaigns (Dunland, Rhûn, Far Harad, Khand).
- Your realm: a realm you lead hires no mercenary company of the other side of the War of the Ring.
- Hold the Line: TAOM's battle AI leads formations, and line keeping follows each people's discipline.
- Party Quality: TAOM's tiers up to 10, TAOM's party names kept, rating names for each people.
- Xorberax's Legacy: banks tied to the town treasury; your own kingdom's lords never plunder your vault.
- Dismemberment Plus: heroes and the player can lose their head, an AI cut chance, troll blood, and an optional limit on cut parts.
- Improved Garrisons: a TAOM defaults button, a template reset, texts in 12 languages, and the English tutorial names TAOM troops.
- Tier Visual: TAOM-style tier colours, and a tier line on volunteers you can't recruit yet.
- More Formation Banners: back flags follow TAOM's banner rule (infantry only, one in ten, none for trolls, Nazgul, Sauron or bandits).
- Immersive Battlefields and Realistic Blood: new switches. TAOM's horn blowers only, order sounds where its captains don't run, no second order voice; beasts and trolls bleed like people with their hide as armour.
- RTS Camera: a Middle-earth menu line and fewer markers in very big battles (only while RTS Camera is ticked).
- BattleSize Resized and Limitless: BattleSize Resized's helmet swap is off, and very big battles can use lighter detail.
- Retinues: the two co-op guards on building retinues now install.
- The AI Influence and Fourberie fixes are now this module's own work.
- My Little Warband is not covered; leave it unticked.

## What each switch does

Each mod has one switch in Mod Options (MCM), on the "TAOM AIO Compatibility Patch" page. A switch only does something when its mod is ticked. Most need a restart; the big-battle settings apply from the next battle. Many switches have extra settings under them.

- Retinues: its Troops button on TAOM's map bar, co-op guards, and its editor button on TAOM's encyclopedia pages.
- Hold the Line: its Depth morale works under TAOM; TAOM's battle AI leads, culture discipline, TAOM's brace list, and a faster cavalry check for big battles.
- Bodyguards: no crash entering towns, castles and villages; escorts there and in hideouts (single player); pay in the daily wage; your culture's guard troop; its button on TAOM's hero page.
- TAOM Voices: on hold (see the note above).
- Xorberax's Legacy: TAOM's trolls, mumakil, spiders and Sauron keep TAOM's own hit rules; banks tied to the town treasury.
- Improved Garrisons: TAOM's party, food and money rules stay in charge, with its garrison costs on top; TAOM defaults button.
- Dual Wield: TAOM's dual-wielding troops with a corrected troop list; no separate Dual Wield - TAOM module needed.
- Dismemberment Plus: dismemberment that fits TAOM's races; creatures are never cut.
- Party Quality: doesn't reveal parties that TAOM's Hidden Hosts keeps hidden; TAOM's tiers and names.
- Limitless (single player): fewer crashes at the start of big battles, battle-size and first-wave settings, formation and reinforcement options, big-battle detail, and keeps TAOM's creature mounts on the field.
- TAOM escape menu: every button fits in the pause menu.
- Diplomacy (single player): Diplomacy works inside TAOM's diplomacy rules; its civil wars are off.
- AI Influence (single player): doesn't cancel TAOM's alliances and wars, gives it a Middle-earth world, and stops its fief and clan moves.
- Fourberie (single player): guards against errors with TAOM's races and items; TAOM troops, prices and bandits.
- RBM (single player): RBM's armour, damage, posture and battle AI run on TAOM's items; prices and money stay TAOM's; TAOM's Culture Doctrine picks the tactics.
- Your realm: no mercenaries of the other side (single player).
- Middle-earth sellswords: four mercenary companies in new campaigns.
- More Formation Banners: back flags follow TAOM's banner rule.
- Immersive Battlefields: sounds only, on your PC.
- Realistic Blood: looks only, on your PC.
- RTS Camera: a menu line and one marker display.
- BattleSize Resized: its helmet swap is off.
- TAOM item names: an item's culture tag comes first, e.g. "[Mordor] Fine Uruk Sword VI".
- Tier Visual: weighted troops (×2, ×3) keep their tier colour; TAOM colours; tier line on locked recruit slots.
- Culture tags on troop names: TAOM's caravan troops, militia and spider riders get a culture tag, e.g. "[Erebor] Caravan Guard". TAOM 2.0.42 tags most of them itself; this switch skips names that already have a tag.

Some options of the covered mods that work against TAOM are held off while their switch is on. `README.txt` in the module folder has the details for every switch.

## Update notice

Once per game start, at the main menu, the module asks GitHub for the newest release number of this mod. When a newer version is out, one message says so, with a button that opens the TAOM Mods website. Each new version is announced once. No internet or an error: nothing shows.

The check is one request to GitHub's public API for this repository's latest release. Like any web request, GitHub sees your IP address. Nothing else is sent. It never runs in Co-op's server process.

To turn it off: Mod Options > TAOM AIO Compatibility Patch > Warnings > "Check for updates at the main menu" (applies from the next start).

The notice works from v1.5.49 on, so players on v1.5.24 hear about this version here or on the website.

## Requirements

- Bannerlord 1.5.7
- TAOM 2.0.41 or newer, with TAOM.Dependencies (checked with 2.0.42)

## Install

1. Install TAOM and the mods you want from their own pages.
2. Put the `TAOM-AIO-Compat` folder from the zip into Bannerlord's `Modules` folder. To update, replace the old folder.
3. In the launcher, tick "TAOM AIO Compatibility Patch" right after TAOM, above the mods it covers.

## Tested mods

Played with this module on Bannerlord 1.5.7, in this load order:

1. TAOM Dependencies 2.0.42 and its four library entries
2. Native, SandBoxCore, Sandbox, StoryMode, CustomBattle
3. LOTRLOME Armory 2.0.42
4. Dual Wield 0.10.9
5. TAOM Map 2.0.42
6. TAOM 2.0.42
7. TAOM AIO Compatibility Patch
8. Xorberax's Legacy 1.0.8.8
9. Diplomacy 1.5.7
10. AI Influence 7.0.6
11. Fourberie 1.5.7.0
12. RTS Camera 5.5.4 and RTS Camera Command System 5.5.4 (not needed with TAOM, see the note above)
13. Bodyguards Reloaded 1.2.13
14. Retinues 2.0.0.16
15. Limitless 1.0.2.2
16. BattleSize Resized 2.0.4
17. Dismemberment Plus 2.0.8.8
18. Realistic Blood 1.1.7
19. Perfect Fire Arrows 6.8.3
20. Immersive Battlefields 3.7.0
21. More Formation Banners 1.0.7
22. Party Quality 1.2.3
23. Tier Visual 2.6
24. Improved Garrisons 4.3.1.0
25. Hold the Line b0.2.5
26. RBM 4.8.2
27. Bannerlord Co-op and TAOM.CoopCompat (co-op only)

## Known issues

- Very big sieges can still slow the game down once the walls break. Keep the siege battle size at 4000 to 8000.
- RBM: start a new campaign the first time you play with the RBM switch.
- Switches aren't compared between co-op players, so check that everyone uses the same ones.
- Bodyguards' hideout escorts may stand still instead of fighting. Escorts are not added in battles where another lord commands.
- Middle-earth sellswords: the Spears of Far Harad's leaders look Haradrim. The companies come only with new campaigns.
- More Formation Banners' banner-bearer interval setting has no effect under TAOM.

## Planned mods

- TAOM Voices: support returns when the mod is re-released.
- Diplomacy, AI Influence and RBM in co-op: single player only for now.

## Reporting bugs

Open an issue with your mod list, single player or co-op, and `Documents\Mount and Blade II Bannerlord\Logs\taom_aio_compat.log` renamed to `.txt`. Remove your Steam id and user name first.

## Credits

- Retinues by bolaft ([Nexus 8847](https://www.nexusmods.com/mountandblade2bannerlord/mods/8847))
- Hold the Line - Infantry AI by UirosRios ([Nexus 14409](https://www.nexusmods.com/mountandblade2bannerlord/mods/14409))
- Bodyguards - Companion Escort Overhaul by Exiur, based on Bodyguards by carbon198 ([Nexus 9492](https://www.nexusmods.com/mountandblade2bannerlord/mods/9492))
- Voices of the Age of Men by Theb0yys ([Nexus 13574](https://www.nexusmods.com/mountandblade2bannerlord/mods/13574))
- Xorberax's Legacy and Dismemberment Plus by TheSchwarz1, original mods by Xorberax ([Nexus 3462](https://www.nexusmods.com/mountandblade2bannerlord/mods/3462)), used with his permission. None of his files are included; get his mods from their own pages.
- Realistic Battle Mod (RBM) by Marnah93 and Philozoraptor ([Nexus 791](https://www.nexusmods.com/mountandblade2bannerlord/mods/791)). No RBM file or code is included or changed; parts of the RBM switch come from the TAOM team's RBM patch by Theb0yys, used with permission.
- Improved Garrisons by Sidies ([Nexus 688](https://www.nexusmods.com/mountandblade2bannerlord/mods/688))
- Party Quality by ormuszeus ([Nexus 12979](https://www.nexusmods.com/mountandblade2bannerlord/mods/12979))
- Limitless by BlocTheWorker ([Nexus 14025](https://www.nexusmods.com/mountandblade2bannerlord/mods/14025)), used with his permission. No Limitless file is included; get it from its Nexus page.
- Diplomacy by the Diplomacy Team ([Nexus 832](https://www.nexusmods.com/mountandblade2bannerlord/mods/832))
- AI Influence by MFive ([Nexus 9711](https://www.nexusmods.com/mountandblade2bannerlord/mods/9711))
- Fourberie by Spinozart1 ([Nexus 2969](https://www.nexusmods.com/mountandblade2bannerlord/mods/2969))
- BattleSize Resized by Vombora ([Nexus 8177](https://www.nexusmods.com/mountandblade2bannerlord/mods/8177))
- Tier Visual by Alex822007 ([Nexus 12004](https://www.nexusmods.com/mountandblade2bannerlord/mods/12004)). Files not included; install the original.
- Dual Wield and Dual Wield - TAOM by the TAOM mod developers
- Bannerlord Co-op by the Bannerlord Co-op team

None of these mods' files are included, apart from the Dual Wield - TAOM troop list, used with permission.

## License

Free to use, ask to copy. Use it in your own games, single player and co-op. Don't re-upload it or share changed copies without permission. See [LICENSE](LICENSE).
