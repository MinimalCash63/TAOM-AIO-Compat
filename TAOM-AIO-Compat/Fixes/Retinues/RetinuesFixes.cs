// Retinues switch: everything for Retinues v2 (by bolaft) under TAOM 2.0.39 on Bannerlord 1.5.7.
//  1. RetinuesDependencyBridge: must be in place before Retinues' OnSubModuleLoad (else Retinues crashes).
//     Installed from the AIO's constructor when Retinues.dll is already loaded (AIO below Retinues), else
//     from the AIO's OnSubModuleLoad (AIO above Retinues; all OnSubModuleLoad calls run in load order
//     after every module DLL and constructor, Module.LoadSubModules 1.5.7).
//  2. RetinuesTaomMapBarBridge: Troops button on TAOM's map bar, installed at the main menu.
// Both classes are unchanged from Retinues-TAOM-157Fix v1.1.0 apart from namespace and Harmony ids.
//  3. RetinuesCoopGuards (v1.4.0): stops Retinues building troops on a co-op client and during new-game
//     set-up before it is ready; installed at the main menu. A failure there leaves 1 and 2 untouched.
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.Retinues
{
    internal sealed class RetinuesFixes : FixGroup
    {
        internal override string SettingKey => "RetinuesFixes";
        internal override string Name => "Retinues";
        internal override string TargetModuleId => "Retinues";
        internal override string EarlyInstallAssembly => RetinuesDependencyBridge.RetinuesAssemblyName;
        internal override string ReplacesModuleId => "TAOM_Retinues_157Fix";

        // Owner, 13 Nov 2026: off means off. Without the bridge Retinues v2 throws in its own OnSubModuleLoad,
        // which aborts the game at start-up, so the player must untick Retinues too.
        internal override string OffWarning =>
            "Retinues v2 crashes the game at start-up without this switch. Untick Retinues in the launcher as well.";

        private bool _mapBarTried, _guardsTried;
        // v1.4.0 addendum 9b: on a co-op client, unchanged troop values are not written again (RetinuesCoopWriteFilter.cs).
        private readonly LateInstall _writeFilter = new LateInstall("Co-op write filter", RetinuesCoopWriteFilter.TryInstall, RetinuesCoopWriteFilter.Uninstall);

        internal override bool Install()
        {
            Log("Retinues module " + (ModEnv.ModuleVersion(TargetModuleId) ?? "?") + ".");
            return RetinuesDependencyBridge.Install(Log);
        }

        internal override void OnGameStart(TaleWorlds.Core.Game game, TaleWorlds.Core.IGameStarter starter) => _writeFilter.Try(m => Log("[co-op] " + m));

        internal override void OnBeforeMissionBehaviorInitialize(TaleWorlds.MountAndBlade.Mission mission) => _writeFilter.Try(m => Log("[co-op] " + m));

        internal override void OnMainMenu()
        {
            _writeFilter.Try(m => Log("[co-op] " + m));
            if (!_guardsTried)
            {
                _guardsTried = true;
                try { RetinuesCoopGuards.Install(m => Log("[guards] " + m)); }
                catch (System.Exception e) { Log("[guards] install failed: " + e.Message); }
            }
            if (_mapBarTried)
                return;
            _mapBarTried = true;
            RetinuesTaomMapBarBridge.Install(m => Log("[map bar] " + m));
        }

        internal override void Uninstall()
        {
            _writeFilter.Uninstall();
            try { RetinuesCoopGuards.Uninstall(); } catch { }
            RetinuesTaomMapBarBridge.Uninstall();
            RetinuesDependencyBridge.Uninstall();
        }
    }
}
