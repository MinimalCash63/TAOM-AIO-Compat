// Copied from TAOM_Standalone_Compat_RBM v1.0.11 (CustomContentRepairs, BalancePolicy.RepairStarterAmmo,
// CustomEquipmentMerge, CustomPrizeSources) by Theb0yys, TAOM team, with permission (owner, 13 Nov 2026).
// Updated for TAOM 2.0.38 + Bannerlord 1.5.7 + RBM 4.8.1:
//  - dropped: bow physics-body swap (bo_composite_shotbow_g), elven arrow mesh swap (wm_elven_arrow_v1_a/v2_a)
//    and the old Uruk crafted-weapon restore: TAOM 2.0.38 / Armory 2.0.37 no longer have those values or ids,
//    so they could never fire (plan, section 1, items 4-6);
//  - dropped: CustomContentArrowDrag (RBM 4.8.1 no longer changes AirFrictionArrow, so it restored the value
//    it had just read);
//  - RBM settings are read by reflection (RbmConfig); every patch body is in try/catch so nothing can escape
//    during item loading (TAOM repeats new-game loading forever on an exception).
// The ammo id list is unchanged apart from the five bows (they only had the body swap).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM_AIO_Compat.Fixes.RBM.Content
{
    [HarmonyPatch(typeof(ItemObject), "Deserialize")]
    internal static class ContentRepairs
    {
        /// <summary>True once a TAOM arrow or bolt has been loaded (the original's "custom content loaded" flag).</summary>
        internal static bool CustomAmmoLoaded { get; private set; }

        // TAOM / Armory arrows and bolts that get RBM's ammo format (+100 thrust damage).
        private static readonly HashSet<string> NamedAmmo = new HashSet<string>(StringComparer.Ordinal)
        {
            "wm_elven_arrow_q1v2", "wm_elven_arrow_q2v2", "wm_elven_arrow_q3v2", "wm_elven_arrow_q4v2",
            "wm_elven_arrow_q1v3", "wm_elven_arrow_q2v3", "wm_elven_arrow_q3v3", "wm_elven_arrow_q4v3",
            "wm_elven_arrow_q1v4", "wm_elven_arrow_q2v4", "wm_elven_arrow_q3v4", "wm_elven_arrow_q4v4",
            "wm_elven_arrow_v1_a", "wm_elven_arrow_v2_a", "wm_elven_arrow_v3_a", "wm_elven_arrow_v4_a",
            "wm_elven_arrow_v1_b", "wm_elven_arrow_v2_b", "wm_elven_arrow_v1_c", "wm_elven_arrow_v2_c",
            "wm_elven_arrow_v1_d", "wm_elven_arrow_v2_d",
            "wm_mirkwood_arrow_a01", "wm_mirkwood_arrow_a02", "wm_mirkwood_arrow_a03", "wm_isengard_arrow_a01",
            "sk_dwarf_erebor_arrow_a", "sk_dwarf_erebor_arrow_b",
            "wm_isengard_bolt_a01", "sm_dwarf_iron_bolt_a", "uruk_bolts",
            // v1.3.0 (audit 14 Nov 2026): the only other Arrow/Bolt items under 100 in TAOM 2.0.39 / Armory.
            // hr_st_arrows_a is the Harad troops' quiver; the other three are TAOM's Custom Game tower/volley arrows.
            "hr_st_arrows_a", "fow_tower_arrow", "ld_elven_arrow", "ld_tower_arrow",
            // v1.4.1 (TAOM 2.0.40 check, 17 Nov 2026): Legolas's ability arrow, new in TAOM 2.0.40 (thrust 90),
            // fired through Mission.AddCustomMissile; its damage goes through RBM's -100 (plan review, Verified).
            "taom_legolas_arrow",
        };

        // Starter quivers (original BalancePolicy.StarterAmmo).
        private static readonly HashSet<string> StarterAmmo = new HashSet<string>(StringComparer.Ordinal)
        {
            "starter_sk_dwarf_erebor_arrow_a", "starter_wm_isengard_arrow_a01", "starter_default_arrows", "starter_bodkin_arrows_b",
            "starter_bodkin_arrows_c", "starter_bodkin_arrows_a", "starter_wm_mirkwood_arrow_a01", "starter_steppe_arrows",
            "starter_wm_elven_arrow_v1_a",
        };

        [HarmonyPrefix]
        [HarmonyPriority(0)]
        private static void Prefix(XmlNode __1)
        {
            try
            {
                ItemAvailability.Repair(__1);
                bool combat = RbmConfig.CombatEnabled;
                bool campaign = RbmConfig.CampaignEnabled;
                RbmConfig.PriceModifiers(out float weaponPrice, out float armorPrice);
                EquipmentBalance.Repair(__1, combat, campaign, weaponPrice, armorPrice);
                Repair(__1, combat);
            }
            catch (Exception e)
            {
                string id = (__1 as XmlElement)?.GetAttribute("id") ?? "?";
                RbmLog.WriteOnce("item|" + id, "Item " + id + ": RBM balance not applied (error caught, the item loads as it is): " + e.Message);
            }
        }

        internal static void Repair(XmlNode node, bool combatEnabled)
        {
            if (!(node is XmlElement item) || item.Name != "Item")
                return;
            if (RepairStarterAmmo(item, combatEnabled))
                CustomAmmoLoaded = true;
            if (!NamedAmmo.Contains(item.GetAttribute("id")))
                return;
            XmlElement weapon = item["ItemComponent"]?["Weapon"];
            string weaponClass = weapon?.GetAttribute("weapon_class");
            if (weaponClass != "Arrow" && weaponClass != "Bolt")
                return;
            CustomAmmoLoaded = true;
            if (combatEnabled && int.TryParse(weapon.GetAttribute("thrust_damage"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int thrust)
                && thrust >= 0 && thrust < 100)
                weapon.SetAttribute("thrust_damage", (thrust + 100).ToString(CultureInfo.InvariantCulture));
        }

        private static bool RepairStarterAmmo(XmlElement item, bool combatEnabled)
        {
            if (!combatEnabled || !StarterAmmo.Contains(item.GetAttribute("id")))
                return false;
            XmlElement weapon = item["ItemComponent"]?["Weapon"];
            string weaponClass = weapon?.GetAttribute("weapon_class");
            if (weaponClass != "Arrow" && weaponClass != "Bolt")
                return false;
            if (!int.TryParse(weapon.GetAttribute("thrust_damage"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int thrust) || thrust < 0)
                return false;
            if (thrust < 100)
                weapon.SetAttribute("thrust_damage", (thrust + 100).ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }

    [HarmonyPatch(typeof(MBObjectManager), "CreateMergedXmlFile")]
    internal static class EquipmentMerge
    {
        [HarmonyPostfix]
        [HarmonyPriority(0)]
        internal static void Postfix(List<string> xsltList, ref XmlDocument __result)
        {
            try
            {
                if (__result?.DocumentElement == null || !RbmConfig.CombatEnabled)
                    return;
                string transform;
                switch (__result.DocumentElement.Name)
                {
                    case "CraftingPieces":
                        EquipmentBalance.RepairPieces(__result, combatEnabled: true);
                        return;
                    case "CraftingTemplates":
                        transform = "crafting_templates.xslt";
                        break;
                    case "WeaponDescriptions":
                        transform = "weapon_descriptions.xslt";
                        break;
                    default:
                        return;
                }
                if (xsltList == null)
                    return;
                for (int i = 0; i < xsltList.Count; i++)
                {
                    string xslt = xsltList[i];
                    if (!IsArmoryTransform(xslt, transform))
                        continue;
                    XmlDocument transformed = MBObjectManager.ApplyXslt(xslt, __result);
                    if (transformed?.DocumentElement == null)
                        return;
                    // v1.3.0 (audit 14 Nov 2026): the Armory transform replaces the piece lists, so TAOM's own
                    // later transforms (ModuleData\placeholder_weapons\ph_*.xslt, which append TAOM's placeholder
                    // pieces) have to be applied again after it, in list order. Without this TAOM's placeholder
                    // weapons end up in no weapon description. A failing TAOM step keeps the Armory-only result.
                    for (int j = i + 1; j < xsltList.Count; j++)
                    {
                        string later = xsltList[j];
                        if (!IsTaomTransform(later))
                            continue;
                        try
                        {
                            XmlDocument next = MBObjectManager.ApplyXslt(later, transformed);
                            if (next?.DocumentElement == null)
                            {
                                RbmLog.WriteOnce("merge-taom|" + later, "Merged XML " + transformed.DocumentElement.Name + ": TAOM transform " + later + " gave no result; left out.");
                                continue;
                            }
                            transformed = next;
                            RbmLog.WriteOnce("merge-taom-ok|" + later, "Merged XML " + transformed.DocumentElement.Name + ": TAOM transform re-applied after the Armory's: " + later);
                        }
                        catch (Exception e)
                        {
                            RbmLog.WriteOnce("merge-taom|" + later, "Merged XML " + transformed.DocumentElement.Name + ": TAOM transform " + later + " not re-applied (error caught): " + e.Message);
                        }
                    }
                    RemoveDuplicatePieces(transformed);
                    __result = transformed; // only after the Armory step and the duplicate removal worked
                    return;
                }
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("merge|" + (__result?.DocumentElement?.Name ?? "?"),
                    "Merged XML " + (__result?.DocumentElement?.Name ?? "?") + ": Armory transform not re-applied (error caught, the game's merge result is used): " + e.Message);
            }
        }

        internal static bool IsArmoryTransform(string path, string fileName)
        {
            if (string.IsNullOrEmpty(path) || !string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                return false;
            string directory = Path.GetDirectoryName(path);
            return string.Equals(Path.GetFileName(directory), "ModuleData", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(Path.GetDirectoryName(directory)), "LOTRLOME_Armory", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True for an XSLT anywhere under Modules\TAOM\ModuleData\ (either slash, any case).</summary>
        internal static bool IsTaomTransform(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            string[] parts = path.Replace('\\', '/').Split('/');
            for (int i = 2; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], "ModuleData", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parts[i - 1], "TAOM", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parts[i - 2], "Modules", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void RemoveDuplicatePieces(XmlDocument document)
        {
            foreach (XmlElement list in document.SelectNodes("//AvailablePieces | //UsablePieces"))
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                XmlNode node = list.FirstChild;
                while (node != null)
                {
                    XmlNode next = node.NextSibling;
                    if (node is XmlElement element)
                    {
                        string id = element.GetAttribute("id");
                        if (id.Length == 0)
                            id = element.GetAttribute("piece_id");
                        if (id.Length != 0 && !seen.Add(id))
                            list.RemoveChild(element);
                    }
                    node = next;
                }
            }
        }
    }

    /// <summary>Remembers which item ids come from TAOM or Armory files (used for tournament prizes).</summary>
    [HarmonyPatch(typeof(MBObjectManager), "CreateDocumentFromXmlFile")]
    internal static class PrizeSources
    {
        private static readonly HashSet<string> ItemIds = new HashSet<string>(StringComparer.Ordinal);

        internal static int Revision { get; private set; }

        internal static bool Contains(string id) => id != null && ItemIds.Contains(id);

        [HarmonyPostfix]
        internal static void Postfix(string xmlPath, XmlDocument __result)
        {
            try
            {
                if (__result?.DocumentElement?.Name != "Items" || !IsContentPath(xmlPath))
                    return;
                foreach (XmlNode child in __result.DocumentElement.ChildNodes)
                {
                    if (child is XmlElement element && (element.Name == "Item" || element.Name == "CraftedItem"))
                    {
                        string id = element.GetAttribute("id");
                        if (id.Length != 0 && ItemIds.Add(id))
                            Revision++;
                    }
                }
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("prizesource|" + xmlPath, "Prize sources: " + xmlPath + " skipped (error caught): " + e.Message);
            }
        }

        // Modules\TAOM\ModuleData\... or Modules\LOTRLOME_Armory\ModuleData\... (the original also accepted a
        // TAOM_Standalone_Compat_RBMCustomContent module, which does not exist).
        private static bool IsContentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            string[] parts = path.Replace('\\', '/').Split('/');
            for (int i = 2; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], "ModuleData", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parts[i - 2], "Modules", StringComparison.OrdinalIgnoreCase))
                {
                    string module = parts[i - 1];
                    return string.Equals(module, "LOTRLOME_Armory", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(module, "TAOM", StringComparison.OrdinalIgnoreCase);
                }
            }
            return false;
        }
    }
}
