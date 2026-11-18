// Dual Wield switch: replaces the separate module Dual Wield - TAOM (DualWield_TAOM v0.1.3, by Dual Wield's
// author, folded in with permission, owner 13 Nov 2026).
// Dual Wield 0.10.x (checked on 0.10.5 and 0.10.7) loads its own TAOM troop set (DualWield.Core.DwTroopSetLoader.Sync, called from its
// OnGameStart): three entries in XmlResource.XmlInformationList, module "DualWield", names
// troops/taom/troops_dualwield, heroes_dualwield, equipment_dualwield. Dual Wield - TAOM's only difference was a
// corrected troops file: harad03_* armour on the Harad Twin-Scimitar and harad05_v1_* on the Umbar Corsair Twin-Blade (neither in TAOM 2.0.39) replaced by armour that exists.
// This group ships that corrected file (ModuleData\aio_dualwield_troops.xml, byte-identical, plus the troops
// XSLT, identical in both copies) and, after every Sync, points Dual Wield's troops entry at it. Dual Wield's
// own checks and its "NPC Dual-Wield Troops" setting stay in charge: if Sync adds nothing, nothing is swapped.
// The game merges the list only later, when a game loads its objects (MBObjectManager.GetMergedXmlForManaged).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;

namespace TAOM_AIO_Compat.Fixes.DualWield
{
    internal sealed class DualWieldFixes : FixGroup
    {
        internal override string SettingKey => "DualWieldFixes";
        internal override string Name => "Dual Wield";
        internal override string TargetModuleId => "DualWield";
        internal override string ReplacesModuleId => "DualWield_TAOM";

        internal override string OffWarning =>
            "Dual Wield then loads its own TAOM troops file, whose Harad Twin-Scimitar and Umbar Corsair Twin-Blade name armour that TAOM 2.0.39 does not have (not tested: missing armour or item errors). Nothing to untick, but keep this on with TAOM.";

        private const string HarmonyId = "taom.compat.aio.dualwield";
        private const string AioModuleId = "TAOM_AIO_Compat";
        private const string DwModuleId = "DualWield";
        private const string DwTroopsName = "troops/taom/troops_dualwield";
        private const string AioTroopsName = "aio_dualwield_troops";
        private const string NpcCharactersId = "NPCCharacters";

        private static Action<string> _log;
        private static bool _swapLogged, _errorLogged;
        private Harmony _harmony;

        internal override bool Install()
        {
            _log = Log;
            Type loader = ModEnv.FindType("DualWield", "DualWield.Core.DwTroopSetLoader");
            MethodInfo sync = loader == null ? null : AccessTools.DeclaredMethod(loader, "Sync", new[] { typeof(string) });
            if (sync == null)
            {
                Log("Dual Wield's DualWield.Core.DwTroopSetLoader.Sync(string) not found (Dual Wield " + (ModEnv.ModuleVersion(TargetModuleId) ?? "?") + "). Nothing changed.");
                return false;
            }
            string xml = ModuleHelper.GetXmlPath(AioModuleId, AioTroopsName);
            if (!File.Exists(xml) || !File.Exists(Path.ChangeExtension(xml, ".xslt")))
            {
                Log("ModuleData\\" + AioTroopsName + ".xml/.xslt missing from the AIO module. Nothing changed.");
                return false;
            }
            var h = new Harmony(HarmonyId);
            h.Patch(sync, postfix: new HarmonyMethod(typeof(DualWieldFixes).GetMethod(nameof(SyncPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            _harmony = h;
            Log("Postfix on DwTroopSetLoader.Sync installed (Dual Wield " + (ModEnv.ModuleVersion(TargetModuleId) ?? "?") + ").");
            Swap(); // Sync runs from Dual Wield's OnGameStart, so normally there is nothing to swap yet
            return true;
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            try { RemoveOurEntries(); } catch { }
        }

        private static void SyncPostfix()
        {
            try { Swap(); }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    try { _log?.Invoke("Swap error (ignored, logged once; Dual Wield's own file stays): " + e.Message); } catch { }
                }
            }
        }

        // Remove any entry of ours left from an earlier Sync, then point the troops entry Dual Wield just
        // added (if any) at our corrected file, at the same list position.
        private static void Swap()
        {
            List<MbObjectXmlInformation> list = XmlResource.XmlInformationList;
            if (list == null)
                return;
            int removed = RemoveOurEntries();
            for (int i = 0; i < list.Count; i++)
            {
                MbObjectXmlInformation x = list[i];
                if (x.ModuleName == DwModuleId && x.Id == NpcCharactersId && x.Name == DwTroopsName)
                {
                    list[i] = new MbObjectXmlInformation
                    {
                        Id = x.Id,
                        Name = AioTroopsName,
                        ModuleName = AioModuleId,
                        GameTypesIncluded = x.GameTypesIncluded == null ? new List<string>() : new List<string>(x.GameTypesIncluded)
                    };
                    if (!_swapLogged)
                    {
                        _swapLogged = true;
                        _log?.Invoke("Dual Wield's TAOM troops file swapped for the corrected one (Harad and Umbar armour ids that exist in TAOM 2.0.39).");
                    }
                    return;
                }
            }
            if (removed > 0)
                _log?.Invoke("Dual Wield added no TAOM troops this time (its own setting or checks); our earlier entry removed.");
        }

        private static int RemoveOurEntries()
        {
            List<MbObjectXmlInformation> list = XmlResource.XmlInformationList;
            return list == null ? 0 : list.RemoveAll(x => x.ModuleName == AioModuleId && x.Name == AioTroopsName);
        }
    }
}
