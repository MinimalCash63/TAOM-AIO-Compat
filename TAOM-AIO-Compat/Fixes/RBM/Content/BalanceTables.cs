// Copied from TAOM_Standalone_Compat_RBM v1.0.11 (ItemAvailability, EquipmentBalance) by Theb0yys, TAOM team,
// with permission (owner, 13 Nov 2026). Changes: tables are loaded explicitly by RbmFixes.Install (no static
// constructor, so a bad table cannot throw later during item loading), prices are checked when the table is
// loaded, and every edit is skipped if the table was not loaded.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace TAOM_AIO_Compat.Fixes.RBM.Content
{
    /// <summary>Culture / is_merchandise corrections for TAOM Armory items (TAOM.RBM.Availability.xml).</summary>
    internal static class ItemAvailability
    {
        private static Dictionary<string, XmlElement> _corrections;

        internal static int Count => _corrections?.Count ?? 0;

        internal static void Load(XmlDocument document)
        {
            var corrections = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
            foreach (XmlElement item in document.SelectNodes("/Availability/File/Item"))
                corrections.Add(item.GetAttribute("id"), item); // a duplicate id throws here, at Install
            _corrections = corrections;
        }

        internal static void Repair(XmlNode node)
        {
            Dictionary<string, XmlElement> corrections = _corrections;
            if (corrections == null || !(node is XmlElement element) || (element.Name != "Item" && element.Name != "CraftedItem")
                || !corrections.TryGetValue(element.GetAttribute("id"), out XmlElement correction))
                return;
            foreach (XmlAttribute attribute in correction.Attributes)
            {
                if (attribute.Name != "id")
                    element.SetAttribute(attribute.Name, attribute.Value);
            }
        }
    }

    /// <summary>RBM-tuned stats and prices for TAOM items and blade pieces (TAOM.RBM.EquipmentBalance.xml).</summary>
    internal static class EquipmentBalance
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static Dictionary<string, XmlElement> _items;
        private static Dictionary<string, XmlElement> _pieces;

        internal static int ItemCount => _items?.Count ?? 0;
        internal static int PieceCount => _pieces?.Count ?? 0;

        internal static void Load(XmlDocument document)
        {
            var items = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
            var pieces = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
            foreach (XmlElement profile in document.SelectNodes("/EquipmentBalance/Items/Profile"))
            {
                XmlElement price = profile["Price"];
                if (price != null)
                {
                    foreach (string name in new[] { "campaign", "legacy" })
                    {
                        if (price.HasAttribute(name) && !double.TryParse(price.GetAttribute(name), NumberStyles.Float, Invariant, out _))
                            throw new FormatException("Balance table: price '" + name + "' of " + profile.GetAttribute("id") + " is not a number.");
                    }
                }
                items.Add(profile.GetAttribute("id"), profile);
            }
            foreach (XmlElement piece in document.SelectNodes("/EquipmentBalance/Pieces/Piece"))
                pieces.Add(piece.GetAttribute("id"), piece);
            _items = items;
            _pieces = pieces;
        }

        internal static void Repair(XmlNode node, bool combatEnabled, bool campaignEnabled, float weaponPriceModifier = 1f, float armorPriceModifier = 1f)
        {
            Dictionary<string, XmlElement> items = _items;
            if (items == null || !combatEnabled || !(node is XmlElement element) || (element.Name != "Item" && element.Name != "CraftedItem")
                || !items.TryGetValue(element.GetAttribute("id"), out XmlElement profile))
                return;
            XmlElement component = null;
            switch (profile.GetAttribute("kind"))
            {
                case "Crafted":
                    if (element.Name != "CraftedItem" || element.GetAttribute("crafting_template") != profile.GetAttribute("template") || element["Pieces"] == null)
                        return;
                    break;
                case "Armor":
                    component = element["ItemComponent"]?["Armor"];
                    if (element.Name != "Item" || element.GetAttribute("Type") != profile.GetAttribute("type") || component == null
                        || component.GetAttribute("material_type") != profile.GetAttribute("material"))
                        return;
                    break;
                case "Weapon":
                    component = element["ItemComponent"]?["Weapon"];
                    if (element.Name != "Item" || component == null || component.GetAttribute("weapon_class") != profile.GetAttribute("weapon_class"))
                        return;
                    break;
                default:
                    return;
            }
            ApplyAttributes(element, profile["Item"]);
            ApplyAttributes(component, profile["Weapon"] ?? profile["Armor"]);
            if (campaignEnabled && profile.GetAttribute("automatic_campaign_price") == "true")
                element.RemoveAttribute("value");
            XmlElement price = profile["Price"];
            if (price == null)
                return;
            string name = campaignEnabled ? "campaign" : "legacy";
            if (!price.HasAttribute(name))
                return;
            float modifier = price.GetAttribute("modifier") == "Armor" ? armorPriceModifier : weaponPriceModifier;
            if (float.IsNaN(modifier) || float.IsInfinity(modifier) || modifier < 0f)
                return;
            if (!double.TryParse(price.GetAttribute(name), NumberStyles.Float, Invariant, out double basePrice))
                return;
            double value = basePrice * modifier;
            element.SetAttribute("value", Math.Min(int.MaxValue, Math.Round(value, MidpointRounding.AwayFromZero)).ToString("0", Invariant));
        }

        internal static void RepairPieces(XmlDocument document, bool combatEnabled)
        {
            Dictionary<string, XmlElement> pieces = _pieces;
            if (pieces == null || !combatEnabled || document?.DocumentElement?.Name != "CraftingPieces")
                return;
            foreach (XmlNode child in document.DocumentElement.ChildNodes)
            {
                if (!(child is XmlElement element) || element.Name != "CraftingPiece" || element.GetAttribute("piece_type") != "Blade"
                    || !pieces.TryGetValue(element.GetAttribute("id"), out XmlElement piece))
                    continue;
                XmlElement blade = element["BladeData"];
                if (blade?["Swing"] != null && blade["Thrust"] != null)
                {
                    blade["Swing"].SetAttribute("damage_factor", piece.GetAttribute("swing"));
                    blade["Thrust"].SetAttribute("damage_factor", piece.GetAttribute("thrust"));
                }
            }
        }

        private static void ApplyAttributes(XmlElement target, XmlElement attributes)
        {
            if (target == null || attributes == null)
                return;
            foreach (XmlAttribute attribute in attributes.Attributes)
                target.SetAttribute(attribute.Name, attribute.Value);
        }
    }
}
