# TAOM AIO Compatibility Patch

Makes popular Bannerlord mods work together with TAOM (Tales from the Age of Men) 2.0.39 and 2.0.40 on Bannerlord 1.5.7, in single player and with the Co-op mod.

It doesn't replace or edit any of those mods. Install them from their own pages, then add this module.

Download: [Releases](../../releases). Current version: v1.5.8.

> [!IMPORTANT]
> **You need the TAOM Dependency Patch** ([Nexus mod 13196](https://www.nexusmods.com/mountandblade2bannerlord/mods/13196)).
> Without it, the launcher won't let you tick Xorberax's Legacy, Voices of the Waning Realm (TAOM Voices) or Retinues: they stay greyed out with a red **!**. Unblocking files doesn't help.
> Install it and tick its four "TAOM Dependencies Proxy" entries right after TAOM Dependencies, above Native. Don't install the separate Harmony, ButterLib, UIExtenderEx or MCM mods as well.

## How the switches work

Each mod has its own switch in Mod Options (MCM), on the "TAOM AIO Compatibility Patch" page.

- A switch only does something when its mod is ticked in the launcher.
- Changing a switch needs a game restart. Only "Limitless battle size" works from the next battle.
- In co-op, everyone should use the same switches.

## What each switch does

- **Retinues**: Retinues starts and works with TAOM, and its Troops button sits on TAOM's map bar. In co-op only the host builds and changes retinues. If you turn it off, untick Retinues too.
- **RBM (Realistic Battle Mod)**: TAOM's RBM balance for TAOM items and arrows (including the Harad quiver and, in TAOM 2.0.40, Legolas's ability arrows), weapon descriptions for TAOM's placeholder weapons, TAOM recruit and grain prices, RBM tournaments with TAOM troops and prizes (the same prize pick for every co-op player), and a readable RBM Changelog window. If you use the TAOM team's RBM-TAOM patch, untick it; this switch replaces it.
- **Hold the Line**: its Depth morale works under TAOM in field battles.
- **Bodyguards**: no more crash when entering towns, castles and villages on 1.5.7. If you turn it off, untick Bodyguards too.
- **TAOM Voices**: lets TAOM Voices start on Bannerlord 1.5.7 in single player (see Known issues). It stays off in co-op.
- **Xorberax's Legacy**: TAOM's trolls, mumakil, war elephants, spiders and Sauron can't be one-shot by Deadly Horse Charge or Deadly Combat, and keep TAOM's own hit rules. Deadly Combat no longer kills a troop that is already down. In co-op the bank stays with the host.
- **Improved Garrisons**: TAOM's party size, speed, food and money rules stay in charge, with Improved Garrisons' garrison costs on top. If you turn it off, untick Improved Garrisons too.
- **Dual Wield**: TAOM's dual-wielding troops, with a corrected troop list for TAOM 2.0.39 and 2.0.40. You don't need the separate Dual Wield - TAOM module.
- **RTS Camera Command System**: works together with RBM's battle AI instead of fighting it.
- **Dismemberment Plus**: men, female elves and female Nazgul are dismembered as normal. Male elves, male Nazgul, orcs, uruks, Uruk-hai, berserkers, goblins, Saruman and dwarves can lose their head, but not arms or legs. Trolls, Sauron and creatures are never cut. Its slow motion is off in co-op battles.
- **Party Quality**: doesn't give away parties and armies that TAOM's Hidden Hosts keeps hidden.
- **Limitless** (single player only): makes a crash at the start of big battles with TAOM less likely, and adds a "Limitless battle size" choice (2040, 3000, 4000, 6000 or Unlimited). Untick Limitless before co-op.
- **TAOM escape menu**: the pause menu (Esc) shows every button, so "Exit to Main Menu" is never hidden.

More detail for each switch is in `README.txt` inside the module folder.

## Tested mod list

These are the mods and versions we play with this module on Bannerlord 1.5.7, in this load order. Other versions may work, but these are the ones we know load and play together.

1. TAOM Dependencies 2.0.40, then the four TAOM Dependency Patch proxies (Harmony, ButterLib, UIExtenderEx, Mod Configuration Menu)
2. Native, SandBoxCore, Sandbox, StoryMode, CustomBattle
3. LOTRLOME Armory 2.0.40
4. Dual Wield 0.10.7
5. TAOM Map 2.0.40
6. TAOM 2.0.40
7. TAOM AIO Compatibility Patch 1.5.8
8. Xorberax's Legacy 1.0.8.8
9. Voices of the Waning Realm (TAOM Voices) 1.0.0 (see Known issues)
10. RTS Camera 5.5.4
11. RTS Camera Command System 5.5.4
12. Bodyguards Reloaded 1.2.13
13. Retinues 2.0.0.16
14. Limitless 1.0.2.1 (single player only)
15. BattleSize Resized 2.0.4
16. Dismemberment Plus 2.0.8.8
17. Realistic Blood 1.1.7
18. Perfect Fire Arrows 6.8.2
19. Immersive Battlefields 3.7.0
20. More Formation Banners 1.0.7
21. Party Quality 1.2.3
22. Tier Visual 1.4.6
23. Improved Garrisons 4.3.1.0
24. Hold the Line 0.2.4
25. Realistic Battle Mod (RBM) 4.8.2
26. Bannerlord Co-op and TAOM.CoopCompat 2.0.40 (co-op only; untick both for single player)

Co-op was tested with a development build of Co-op for Bannerlord 1.5.x. Every player must use the same Co-op build.

## Requirements

- Bannerlord 1.5.7
- TAOM 2.0.39 or 2.0.40, with TAOM.Dependencies
- TAOM Dependency Patch (Nexus Mods, mod 13196)

## Install

1. Install TAOM and the mods you want from their own pages.
2. Install TAOM Dependency Patch. In the launcher, tick its four entries (each named "TAOM Dependencies Proxy") right after TAOM Dependencies, above Native. Don't install the separate Harmony, ButterLib, UIExtenderEx or MCM mods as well.
3. Download the zip from Releases, put the `TAOM-AIO-Compat` folder in Bannerlord's `Modules` folder, and tick "TAOM AIO Compatibility Patch" right after TAOM.

## Co-op

- Every player needs the same version of this module, the same mods and the same switches.
- Untick Limitless before hosting or joining.

## Single player with Co-op ticked

While Coop is ticked, TAOM uses the game's standard UI instead of its own, even in single player. Co-op also adds its own characters to your save. For single player, untick Coop and TAOM.CoopCompat, and keep separate saves for single player and co-op. The module reminds you with a short message in game; you can turn these reminders off in Mod Options ("Show start-up warnings").

## Known issues

- Xorberax's Legacy, TAOM Voices or Retinues can't be ticked in the launcher: install the TAOM Dependency Patch (see the box at the top).
- TAOM Voices doesn't play in conversations yet. The game doesn't crash.
- Retinues may still create a default retinue for a player joining a co-op game.
- Switches aren't compared between co-op players, so make sure everyone uses the same ones.
- Bodyguards' settlement and hideout escorts don't appear on 1.5.7.
- More Formation Banners' banner-bearer interval setting has no effect under TAOM.

## Reporting bugs

Open an issue here with your mod list, whether it was single player or co-op, and the log `Documents\Mount and Blade II Bannerlord\Logs\taom_aio_compat.log` (rename it to `.txt` before attaching). Remove your Steam id and user name first.

## Credits

- Retinues by bolaft ([Nexus 8847](https://www.nexusmods.com/mountandblade2bannerlord/mods/8847))
- Realistic Battle Mod by Marnah93 and Philozoraptor ([Nexus 791](https://www.nexusmods.com/mountandblade2bannerlord/mods/791))
- TAOM compatibility patches by Theb0yys ([Nexus 13207](https://www.nexusmods.com/mountandblade2bannerlord/mods/13207)): the RBM and Xorberax's Legacy switches are based on his patches, used with his permission.
- Hold the Line - Infantry AI by UirosRios ([Nexus 14409](https://www.nexusmods.com/mountandblade2bannerlord/mods/14409))
- Bodyguards - Companion Escort Overhaul by Exiur, based on Bodyguards by carbon198 ([Nexus 9492](https://www.nexusmods.com/mountandblade2bannerlord/mods/9492))
- Voices of the Age of Men by Theb0yys ([Nexus 13574](https://www.nexusmods.com/mountandblade2bannerlord/mods/13574))
- Xorberax's Legacy and Dismemberment Plus by TheSchwarz1, original mods by Xorberax ([Nexus 3462](https://www.nexusmods.com/mountandblade2bannerlord/mods/3462))
- Improved Garrisons by Sidies ([Nexus 688](https://www.nexusmods.com/mountandblade2bannerlord/mods/688))
- RTS Camera and RTS Camera Command System by LiZhenhuan1019, Lkoinw and Norbivar ([Nexus 355](https://www.nexusmods.com/mountandblade2bannerlord/mods/355))
- Party Quality by ormuszeus ([Nexus 12979](https://www.nexusmods.com/mountandblade2bannerlord/mods/12979))
- Limitless by BlocTheWorker ([Nexus 14025](https://www.nexusmods.com/mountandblade2bannerlord/mods/14025)), used with his permission. No Limitless file is included; get it from its Nexus page.
- BattleSize Resized by Vombora ([Nexus 8177](https://www.nexusmods.com/mountandblade2bannerlord/mods/8177))
- Dual Wield and Dual Wield - TAOM by the TAOM mod developers
- TAOM Dependency Patch by Theb0yys ([Nexus 13196](https://www.nexusmods.com/mountandblade2bannerlord/mods/13196))
- Bannerlord Co-op by the Bannerlord Co-op team

None of these mods' files are included, apart from the Dual Wield - TAOM troop list and Theb0yys' RBM balance tables, used with permission.

## License

Free to use, ask to copy. Use it in your own games, single player and co-op. Don't re-upload it or share changed copies without permission. See [LICENSE](LICENSE).
