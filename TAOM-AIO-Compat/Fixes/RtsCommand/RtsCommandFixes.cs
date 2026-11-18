// RTS Camera Command System switch (new in v1.3.0, audit 14 Nov 2026).
// RTSCamera.CommandSystem 5.5.4 decides at load whether RBM is there:
//   CommandSystemSubModule.IsRealisticBattleModuleInstalled =
//       IsModuleInstalled("RBM") && IsModuleInstalled("RealisticBattleAiModule");
// RBM 4.8.1 is one module ("RBM"), so the flag is false with RBM ticked and the Command System treats RBM
// as absent. The flag is read in two places while playing (Verified, decompile of the installed DLL):
//   - Patch_MovementOrder: with the flag false and "after charge: Hold", a player formation whose target
//     formation is gone is put on hold and the game's (and RBM's) own handling of that order is skipped;
//   - Utility.ShouldChargeToFormation: with the flag true, infantry and foot archers leave the focus charge
//     to RBM's AI instead of the Command System's own.
// (Its third use, the default of the "after charge" option, is already decided at load and saved in the
// Command System's own config; this switch does not change that option.)
// This switch sets the public static flag to what the author intended: true while RBM is ticked and RBM's AI
// module is on in RBM's options, otherwise the Command System's own value. No Harmony patch, no file changed.
// It is set at the main menu and again at each mission start (RBM's AI option can change in between).
// Switch off, or RBM not ticked: nothing is touched.
using System;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.RtsCommand
{
    internal sealed class RtsCommandFixes : FixGroup
    {
        internal override string SettingKey => "RtsCommandFixes";
        internal override string Name => "RTS Camera Command System";
        internal override string TargetModuleId => "RTSCamera.CommandSystem";

        private const string AssemblyName = "RTSCamera.CommandSystem";
        private const string SubModuleTypeName = "RTSCamera.CommandSystem.CommandSystemSubModule";
        private const string FlagName = "IsRealisticBattleModuleInstalled";
        private const string RbmModuleId = "RBM";
        private const string RbmConfigTypeName = "RBMConfig.RBMConfig";
        private const string RbmAiFieldName = "rbmAiEnabled";

        private FieldInfo _flag;
        private bool _haveOriginal;
        private bool _original;
        private bool _lookupFailed;
        private bool _rbmAiWaitLogged;
        private bool? _lastSet;

        internal override bool Install()
        {
            if (!ModEnv.IsActive(RbmModuleId))
            {
                Log("RBM is not ticked: the Command System's RBM check is right as it is. Nothing applied.");
                return false;
            }
            Log("RBM is ticked. The Command System's RBM flag is set at the main menu and at each mission start.");
            return true;
        }

        internal override void OnMainMenu() => Apply("main menu");

        internal override void OnBeforeMissionBehaviorInitialize(Mission mission) => Apply("mission start");

        internal override void Uninstall()
        {
            try
            {
                if (_flag != null && _haveOriginal)
                    _flag.SetValue(null, _original);
            }
            catch { }
            _lastSet = null;
        }

        private void Apply(string when)
        {
            if (_lookupFailed)
                return;
            if (_flag == null)
            {
                Type type = ModEnv.FindType(AssemblyName, SubModuleTypeName);
                FieldInfo field = type == null ? null : AccessTools.Field(type, FlagName);
                if (field == null || !field.IsStatic || field.FieldType != typeof(bool))
                {
                    _lookupFailed = true;
                    Log(when + ": " + SubModuleTypeName + "." + FlagName + " (static bool) not found. Nothing changed.");
                    return;
                }
                _flag = field;
            }
            if (!_haveOriginal)
            {
                _original = (bool)_flag.GetValue(null); // captured once; this runs again on every return to the menu
                _haveOriginal = true;
            }

            bool? rbmAi = RbmAiEnabled();
            if (rbmAi == null)
            {
                if (!_rbmAiWaitLogged)
                {
                    _rbmAiWaitLogged = true;
                    Log(when + ": RBM's AI option (" + RbmConfigTypeName + "." + RbmAiFieldName + ") cannot be read yet; the flag is left as it is and looked at again at the next mission start.");
                }
                return;
            }
            bool wanted = rbmAi.Value || _original;
            _flag.SetValue(null, wanted);
            if (_lastSet != wanted)
            {
                _lastSet = wanted;
                Log(when + ": " + FlagName + " = " + wanted + " (the Command System's own value: " + _original + "; RBM's AI module is " + (rbmAi.Value ? "on" : "off") + ").");
            }
        }

        /// <summary>RBM's "AI module" option, or null while RBMConfig is not loaded or has no such bool field.</summary>
        private static bool? RbmAiEnabled()
        {
            Type type = ModEnv.FindType("RBMConfig", RbmConfigTypeName); // null while RBMConfig.dll is not loaded
            FieldInfo field = type == null ? null : AccessTools.Field(type, RbmAiFieldName);
            if (field == null || !field.IsStatic || field.FieldType != typeof(bool))
                return null;
            return (bool)field.GetValue(null);
        }
    }
}
