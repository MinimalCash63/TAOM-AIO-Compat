# TAOM AIO Compatibility Patch

Fixes for other mods when they run with TAOM (Tales from the Age of Men) 2.0.39 and 2.0.40 on Mount & Blade II: Bannerlord 1.5.7, including with the Co-op mod.

This module edits no file of any other mod. It changes their behaviour only in memory while the game runs. Install the covered mods from their own pages; this module does not replace them.

Download: see [Releases](../../releases). Current version: v1.5.8.

## How the switches work

Each covered mod has one switch on this module's page in Mod Options (MCM).

- A switch does nothing unless its mod is ticked in the launcher.
- Changing a switch needs a restart of the game. The one exception is the "Limitless battle size" setting, which applies from the next battle.
- Off means off: every part of that switch is removed.
- In co-op, every player should use the same switches.

## Covered mods and switches

- **Retinues**: lets Retinues v2 find the Harmony and UIExtenderEx libraries bundled in TAOM.Dependencies, and shows its Troops button on TAOM's map bar. On a co-op client Retinues builds and changes no troops locally; the host owns them. During new-game set-up it waits with AI clan retinues until it is ready. If you turn it off, untick Retinues too, or Retinues v2 crashes the game at start-up.
- **RBM (Realistic Battle Mod)**: TAOM's RBM balance for TAOM items and arrows (including the Harad quiver and, in TAOM 2.0.40, Legolas's ability arrows), weapon descriptions for TAOM's placeholder weapons, TAOM recruit and grain prices, RBM tournaments with TAOM troops and prizes (the same prize pick for every co-op player), a guard so RBM's mercenary companies cannot stop the campaign tick on a co-op client, crash guards on RBM's battle code (they can hide real RBM errors; each is logged once), and a readable RBM Changelog window. Copied from the TAOM team's RBM patch (RBM-TAOM-2.0.29.5): untick that module, because while it is ticked only the Changelog fix runs here. In co-op, RBM's caravans and mercenary companies are left to the host. If you turn it off, RBM runs without TAOM's balance (TAOM arrows do almost no damage with RBM combat on).
- **Hold the Line**: brings back its Depth morale feature under TAOM, in field battles only. Safe to turn off.
- **Bodyguards**: stops a crash in towns, castles and villages on Bannerlord 1.5.7. Settlement and hideout escorts cannot appear on 1.5.7. If you turn it off, untick Bodyguards too.
- **TAOM Voices**: lets TAOM Voices run on Bannerlord 1.5.7 in single player (its own build check was made for 1.4.8 and switched the whole mod off). In co-op TAOM Voices stays off; restart the game between single player and co-op. It also catches an error at campaign start so loading cannot repeat forever. See Known issues. If you turn it off, untick TAOM_Voices too, to be safe.
- **Xorberax's Legacy**: TAOM's trolls, mumakil and Sauron keep TAOM's own follow-through after a hit. TAOM's trolls, mumakil, war elephants, spiders and Sauron are left out of Deadly Horse Charge and Deadly Combat. Deadly Combat no longer kills a troop that is already down or that the game keeps from dying (conversations, cut scenes). In co-op, Deadly Combat only acts on troops the local machine owns, and the bank is not available to joining players (the host keeps it). Safe to turn off.
- **Improved Garrisons**: TAOM's party size, speed, food and clan finance rules stay in charge, with Improved Garrisons' garrison costs added on top. Improved Garrisons 4.3.1.0 and newer fix their own recruitment, map banners and upgrade-path dialog; with the old 4.2.0.7 this switch repairs recruitment and map banners; its "specify upgrade path" dialog stays unavailable. In co-op its costs are not charged on days Co-op blocks gold changes, and its routines run only on the host. If you turn it off, untick Improved Garrisons too.
- **Dual Wield**: replaces the separate Dual Wield - TAOM module, with a corrected troop file for TAOM 2.0.39 and 2.0.40. Dual Wield's own "NPC Dual-Wield Troops" option still decides whether these troops exist. Keep it on with TAOM, and do not untick Dual Wield in a campaign that already has these troops.
- **RTS Camera Command System**: with RBM ticked and RBM's AI module on, tells the Command System that RBM 4.8.x is installed (it looks for an old module name), so it leaves attacks on a chosen enemy formation to RBM's AI. Does nothing without RBM. Safe to turn off.
- **Dismemberment Plus**: men, female elves and female Nazgul are dismembered the way Dismemberment Plus does it. Male elves, male Nazgul, orcs, uruks, Uruk-hai, berserkers, goblins, Saruman and dwarves can lose their head only; their arms and legs stay on. The cut head shows the race's usual face, not that soldier's face. Trolls, Sauron and creatures are never cut. Its slow motion is off during co-op battles. If you turn it off, untick DismembermentPlus too. Players do not have to match.
- **Party Quality**: while TAOM's Hidden Hosts hides a party or army, Party Quality adds no leader name or quality rating to its tooltip. Safe to turn off. Players do not have to match.
- **Limitless** (single player only): with BattleSizeResized ticked, lets BattleSizeResized's first wave use Limitless' slots, lets Limitless start even if it loads after BattleSizeResized, and makes a crash at the start of a battle less likely. If you turn it off, untick Limitless too, or a crash at the start of a battle is more likely. The "Limitless battle size" setting (2040, 3000, 4000, 6000 or Unlimited) decides how many slots a battle uses. Untick Limitless before hosting or joining co-op. Limitless runs its own unsigned program once per game start; that is Limitless' design, not part of this module.
- **TAOM escape menu**: TAOM's pause menu (Esc) has room for about 8 buttons, so with RBM, Retinues or Co-op "Exit to Main Menu" was hidden. The list now grows to show every button, and scrolls if there are still more. Safe to turn off. Players do not have to match.

The full text for each switch, with what to do if you turn it off, is in `README.txt` inside the module folder.

## Requirements

- Mount & Blade II: Bannerlord 1.5.7
- TAOM 2.0.39 or 2.0.40, with TAOM.Dependencies
- TAOM Dependency Patch (Nexus Mods, mod 13196)

This module uses the Harmony and MCM libraries bundled in TAOM.Dependencies.

## Install

1. Install TAOM as its page describes, and the mods you want to use from their own pages.
2. Install TAOM Dependency Patch. In the launcher, tick its four entries, Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu, each named "(TAOM Dependencies Proxy)", right after TAOM Dependencies and above Native. Do not also turn on separate Harmony, ButterLib, UIExtenderEx or MCM modules.
3. Download the zip from Releases, copy the `TAOM-AIO-Compat` folder into Bannerlord's `Modules` folder, and tick "TAOM AIO Compatibility Patch" right after TAOM, above the mods it covers. In TAOM 2.0.40's own order, LOTRLOME_Armory and Dual Wield load before TAOM; that is fine. Limitless can go anywhere below this module, for example right before BattleSizeResized.
4. If you use the TAOM team's RBM-TAOM patch module, untick it (see the RBM switch). The separate Dual Wield - TAOM module is not needed.

To remove: untick it in the launcher. Check the switch list above for mods that then need unticking too.

## Co-op

- Every player needs this module at the same version, the same four proxies and the same ticked mods. Co-op checks this when a player joins.
- Use the same switches on every machine. This module does not compare them.
- Untick Limitless before hosting or joining co-op.
- TAOM Voices stays off in co-op.

## Single player with Co-op ticked

While the Coop module is ticked, TAOM uses the game's standard map bar instead of its own and some TAOM features stay off, even in single player. Co-op also adds its own characters to the save (its team lords and wanderers). A save made with Coop ticked and later loaded without it can get TAOM's "repaired troops" message.

For single player, untick Coop and TAOM.CoopCompat, and keep separate saves for single player and co-op.

## Start-up warnings

The module shows a short in-game message:

- when Coop is ticked while you play single player (once per game start, on the campaign map);
- when Limitless is ticked together with Coop (at the main menu, and again if a co-op session starts).

They never show in Co-op's server process. Turn them off in Mod Options with "Show start-up warnings"; this applies from the next start.

## Known issues

- Retinues: at every game start two of this module's eight Retinues guards do not install. On a joining co-op player's machine Retinues may therefore still create a default retinue when the player joins; the errors are written to `Coop_client.log`.
- This module does not compare its switches between co-op players. Use the same switches on every machine.
- Bodyguards' settlement and hideout escorts do not appear on Bannerlord 1.5.7.
- Under TAOM, More Formation Banners' BannerBearerInterval setting has no effect (TAOM's own banner-bearer rule decides). Its back flags work.
- TAOM Voices: in single player the voices do not play in conversations. The game does not crash. The cause is not known yet.

What has and has not been tested for this version is in the release notes.

## Reporting bugs

Open an issue on this repository. Please include:

- the module version, your Bannerlord version, and your mod list in load order;
- whether it happened in single player or co-op;
- the log `Documents\Mount and Blade II Bannerlord\Logs\taom_aio_compat.log`, attached as a file.

Remove your Steam id, user name and other personal details from anything you attach. GitHub does not accept `.log` files, so rename the copy to `.txt`.

## Building from source

The source is in `TAOM-AIO-Compat/`. It needs the .NET SDK and an installed Bannerlord 1.5.7 with TAOM.Dependencies (it references the game's DLLs, Harmony and MCM from there; none of them is included here).

```
dotnet build TAOM-AIO-Compat/TAOM-AIO-Compat.csproj -c Release -p:GameFolder="<your Bannerlord folder>"
```

The finished module folder is written to `build/TAOM-AIO-Compat/` (set by `Directory.Build.props`; pass `-p:ModuleOut=<folder>\` to change it).

## Credits

Covered mods and their authors:

- Retinues by bolaft ([Nexus 8847](https://www.nexusmods.com/mountandblade2bannerlord/mods/8847))
- Realistic Battle Mod (RBM) by Marnah93 and Philozoraptor ([Nexus 791](https://www.nexusmods.com/mountandblade2bannerlord/mods/791))
- TAOM compatibility patches by Theb0yys (TAOM team) ([Nexus 13207](https://www.nexusmods.com/mountandblade2bannerlord/mods/13207)): the RBM switch is based on his RBM patch, the Xorberax's Legacy switch on his Xorberax patch, used with his permission.
- Hold the Line - Infantry AI by UirosRios ([Nexus 14409](https://www.nexusmods.com/mountandblade2bannerlord/mods/14409))
- Bodyguards - Companion Escort Overhaul by Exiur, based on Bodyguards by carbon198 (MIT) ([Nexus 9492](https://www.nexusmods.com/mountandblade2bannerlord/mods/9492))
- Voices of the Age of Men (TAOM_Voices) by Theb0yys ([Nexus 13574](https://www.nexusmods.com/mountandblade2bannerlord/mods/13574))
- Xorberax's Legacy by TheSchwarz1, original mods by Xorberax ([Nexus 3462](https://www.nexusmods.com/mountandblade2bannerlord/mods/3462))
- Improved Garrisons by Sidies ([Nexus 688](https://www.nexusmods.com/mountandblade2bannerlord/mods/688))
- RTS Camera and RTS Camera Command System by LiZhenhuan1019, Lkoinw and Norbivar ([Nexus 355](https://www.nexusmods.com/mountandblade2bannerlord/mods/355)): no file or code of theirs is included.
- Dismemberment Plus by TheSchwarz1: no file or code of his is included.
- Party Quality by ormuszeus ([Nexus 12979](https://www.nexusmods.com/mountandblade2bannerlord/mods/12979)): no file or code of theirs is included.
- Limitless by BlocTheWorker ([Nexus 14025](https://www.nexusmods.com/mountandblade2bannerlord/mods/14025)): the Limitless switch is made with his permission, on the condition that no Limitless file is included; none is. Get Limitless from its Nexus page.
- BattleSize Resized by Vombora ([Nexus 8177](https://www.nexusmods.com/mountandblade2bannerlord/mods/8177)): no file or code of theirs is included.
- Dual Wield and Dual Wield - TAOM by the TAOM mod developers: the Dual Wield switch carries Dual Wield - TAOM's corrected troop file, used with permission.
- TAOM Dependency Patch by Theb0yys (TAOM team) ([Nexus 13196](https://www.nexusmods.com/mountandblade2bannerlord/mods/13196)): not included here.
- Bannerlord Co-op by the Bannerlord Co-op team: this module reads Co-op's state while the game runs; no file or code of theirs is included.

## License

Free to use, ask to copy. You may use this module in your own games, single player and co-op. Do not re-upload it or distribute changed copies without written permission. Parts based on other authors' work stay under their terms. See [LICENSE](LICENSE).
