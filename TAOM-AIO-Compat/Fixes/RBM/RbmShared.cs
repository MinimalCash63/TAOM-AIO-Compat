// Shared helpers for the RBM switch: logging, the embedded balance tables, and RBM's own settings.
// The ported parts (Content\, Campaign\, Battle\) are copied from TAOM_Standalone_Compat_RBM v1.0.11
// ("RBM-TAOM-2.0.29.5") by Theb0yys, TAOM team, with permission (owner, 13 Nov 2026), and updated for
// Bannerlord 1.5.7, TAOM 2.0.38 and RBM 4.8.1 (Marnah93 and Philozoraptor).
// Plan: AIO-Compat-Patch\docs\2026-11-13-plan-rbm-patch-port.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.RBM
{
    /// <summary>Log for code that runs inside Harmony patches (static). Set by RbmFixes.</summary>
    internal static class RbmLog
    {
        internal static Action<string> Write = _ => { };

        private static readonly HashSet<string> Once = new HashSet<string>(StringComparer.Ordinal);
        private static int _onceCount;

        private const int OnceLimit = 200; // above the 93 guarded RBM methods, so each can be named once

        /// <summary>Writes a line the first time a key is seen; at most OnceLimit such lines per session.</summary>
        internal static void WriteOnce(string key, string message)
        {
            lock (Once)
            {
                if (!Once.Add(key))
                    return;
                if (++_onceCount > OnceLimit)
                {
                    if (_onceCount == OnceLimit + 1)
                        Write("Further error lines left out of the log this session (limit " + OnceLimit + ").");
                    return;
                }
            }
            Write(message);
        }
    }

    /// <summary>The two balance tables, embedded in this DLL (csproj: LogicalName "TAOM_AIO_Compat.RBM.&lt;file&gt;").</summary>
    internal static class RbmResources
    {
        internal const string EquipmentBalanceFile = "TAOM.RBM.EquipmentBalance.xml";
        internal const string AvailabilityFile = "TAOM.RBM.Availability.xml";

        internal static XmlDocument Load(string fileName)
        {
            string name = "TAOM_AIO_Compat.RBM." + fileName;
            var document = new XmlDocument { XmlResolver = null };
            using (Stream stream = typeof(RbmResources).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidOperationException("Embedded table " + name + " is missing from this DLL.");
                document.Load(stream);
            }
            return document;
        }
    }

    /// <summary>
    /// RBM's own settings (static fields on RBMConfig.RBMConfig), read by reflection on every call so this
    /// DLL never references RBM's assemblies and RBM's in-game options still apply. RBMConfig.dll is loaded
    /// by RBM itself; it is looked up at the main menu and, if needed, once at first use.
    /// </summary>
    internal static class RbmConfig
    {
        private static FieldInfo _combat;
        private static FieldInfo _campaign;
        private static FieldInfo _prices;
        private static FieldInfo _weaponPrice;
        private static FieldInfo _armorPrice;
        private static bool _resolved;
        private static bool _failed;

        /// <summary>Looks the fields up; safe to call often. Returns false (logged once) if RBMConfig is not there.</summary>
        internal static bool Resolve()
        {
            if (_resolved)
                return true;
            if (_failed)
                return false;
            try
            {
                Type type = AccessTools.TypeByName("RBMConfig.RBMConfig");
                if (type == null)
                    return false; // not loaded yet; the caller decides whether to give up
                _combat = AccessTools.Field(type, "rbmCombatEnabled");
                _campaign = AccessTools.Field(type, "rbmCampaignEnabled");
                _prices = AccessTools.Field(type, "priceMultipliers");
                Type pricesType = _prices?.FieldType;
                _weaponPrice = pricesType == null ? null : AccessTools.Field(pricesType, "WeaponPriceModifier");
                _armorPrice = pricesType == null ? null : AccessTools.Field(pricesType, "ArmorPriceModifier");
                if (_combat == null || _campaign == null || _prices == null || _weaponPrice == null || _armorPrice == null
                    || _combat.FieldType != typeof(bool) || _campaign.FieldType != typeof(bool)
                    || _weaponPrice.FieldType != typeof(float) || _armorPrice.FieldType != typeof(float))
                {
                    _failed = true;
                    RbmLog.Write("RBMConfig.RBMConfig does not have the expected settings fields (rbmCombatEnabled, rbmCampaignEnabled, priceMultipliers). RBM balance parts stay inactive.");
                    return false;
                }
                _resolved = true;
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                RbmLog.Write("Reading RBM's settings failed; RBM balance parts stay inactive: " + e.Message);
                return false;
            }
        }

        /// <summary>Called at first use: if RBMConfig is still not loaded then, give up for the session.</summary>
        private static bool ResolveOrGiveUp()
        {
            if (_resolved)
                return true;
            if (_failed)
                return false;
            // v1.3.0 (audit 14 Nov 2026): while RBM's config DLL is not loaded yet, do not give up for the
            // session; just report "off" for this call (cheap check first: Resolve scans every assembly).
            if (ModEnv.FindAssembly("RBMConfig") == null)
            {
                RbmLog.WriteOnce("rbmconfig-notloaded", "RBMConfig.dll is not loaded yet at first use; RBM balance is skipped until it is (looked up again on each use).");
                return false;
            }
            if (Resolve())
                return true;
            if (!_failed)
            {
                _failed = true;
                RbmLog.Write("RBMConfig.RBMConfig is not loaded; RBM balance parts stay inactive this session.");
            }
            return false;
        }

        internal static bool CombatEnabled => ResolveOrGiveUp() && (bool)_combat.GetValue(null);

        internal static bool CampaignEnabled => ResolveOrGiveUp() && (bool)_campaign.GetValue(null);

        /// <summary>RBM's weapon and armour price multipliers; 1 when RBM has none.</summary>
        internal static void PriceModifiers(out float weapon, out float armor)
        {
            weapon = 1f;
            armor = 1f;
            if (!ResolveOrGiveUp())
                return;
            object prices = _prices.GetValue(null);
            if (prices == null)
                return;
            weapon = (float)_weaponPrice.GetValue(prices);
            armor = (float)_armorPrice.GetValue(prices);
        }
    }
}
