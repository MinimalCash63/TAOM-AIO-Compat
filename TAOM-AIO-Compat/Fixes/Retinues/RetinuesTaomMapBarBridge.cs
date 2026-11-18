// RetinuesTaomMapBarBridge: shows Retinues' Troops button properly on TAOM's own campaign map bar.
//
// Retinues v2 (by bolaft) adds a map-bar item with the id "troops" and puts its icon into the vanilla
// brush "MapBar.Left.Icons". TAOM (2.0.38 and 2.0.39) replaces the map bar with its own layout, which takes icons
// from the brush "TAOM.Campaign.Icons" and labels from CampaignNavigationLabelWidget. Neither knows
// "troops", so the button showed as an empty, unlabelled slot (owner playtest, 13 Nov 2026).
// This class:
//  1. after Retinues' TroopsIcon.EnsureApplied() runs, copies Retinues' troops icon sprite into a
//     "troops" layer of TAOM.Campaign.Icons (Retinues runs it before the bar widgets are built);
//  2. gives TAOM's label widget the text "Troops" for that id (TAOM's labels are hardcoded English).
// Nothing else changes. In Co-op TAOM shows the vanilla bar instead, and this class then has no effect.
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM_AIO_Compat.Fixes.Retinues
{
    internal static class RetinuesTaomMapBarBridge
    {
        private const string HarmonyId = "taom.compat.aio.retinues.mapbar";
        private const string TroopsIconTypeName = "Retinues.Editor.Integration.MapBar.TroopsIcon";
        private const string LabelWidgetTypeName = "TAOM.Features.FactionUI.Campaign.CampaignNavigationLabelWidget";
        private const string RetinuesIconsBrush = "MapBar.Left.Icons";
        private const string TaomIconsBrush = "TAOM.Campaign.Icons";
        private const string TemplateLayer = "character_developer";
        private const string TroopsId = "troops";
        private const string TroopsLabel = "Troops";

        private static Harmony _harmony;
        private static Action<string> _log;
        private static PropertyInfo _itemIdProp;
        private static bool _iconLogged, _iconErrorLogged, _labelErrorLogged;

        internal static void Install(Action<string> log)
        {
            _log = log ?? (_ => { });
            if (_harmony != null)
                return;
            var h = new Harmony(HarmonyId);
            bool any = false;

            // 1. Icon: postfix on Retinues' TroopsIcon.EnsureApplied()
            try
            {
                Type icon = FindType("Retinues", TroopsIconTypeName);
                MethodInfo ensure = icon?.GetMethod("EnsureApplied", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (ensure == null)
                    _log("Map bar icon: Retinues " + TroopsIconTypeName + ".EnsureApplied() not found. Skipped.");
                else
                {
                    h.Patch(ensure, postfix: new HarmonyMethod(typeof(RetinuesTaomMapBarBridge).GetMethod(nameof(EnsureAppliedPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                    any = true;
                    _log("Map bar icon: installed.");
                }
            }
            catch (Exception e) { _log("Map bar icon: install failed: " + e.Message + ". Skipped."); }

            // 2. Label: postfix on TAOM's CampaignNavigationLabelWidget.OnUpdate(float), declared on that class only
            try
            {
                Type label = FindType("TAOM.Client", LabelWidgetTypeName);
                MethodInfo onUpdate = label?.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(float) }, null);
                _itemIdProp = label?.GetProperty("ItemId", BindingFlags.Instance | BindingFlags.Public);
                if (onUpdate == null || onUpdate.DeclaringType != label || _itemIdProp == null || _itemIdProp.PropertyType != typeof(string) || !typeof(TextWidget).IsAssignableFrom(label))
                    _log("Map bar label: TAOM " + LabelWidgetTypeName + ".OnUpdate / ItemId not found as expected. Skipped.");
                else
                {
                    h.Patch(onUpdate, postfix: new HarmonyMethod(typeof(RetinuesTaomMapBarBridge).GetMethod(nameof(LabelOnUpdatePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                    any = true;
                    _log("Map bar label: installed.");
                }
            }
            catch (Exception e) { _log("Map bar label: install failed: " + e.Message + ". Skipped."); }

            if (any)
                _harmony = h;
        }

        internal static void Uninstall()
        {
            if (_harmony == null)
                return;
            try { _harmony.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
        }

        private static Type FindType(string assemblyName, string typeName)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            return asm?.GetType(typeName, false);
        }

        // Runs after Retinues has put its sprite on the "troops" layer of MapBar.Left.Icons.
        // Retinues calls EnsureApplied from its MapNavigationHandler constructor postfix (before the bar
        // widgets read their brushes) and again every frame on the map.
        private static void EnsureAppliedPostfix()
        {
            try
            {
                var factory = UIResourceManager.BrushFactory;
                if (factory == null)
                    return;
                Brush taom = factory.GetBrush(TaomIconsBrush);
                BrushLayer template = taom?.GetLayer(TemplateLayer);
                if (template == null)
                    return; // TAOM's brush isn't loaded (or this isn't TAOM's bar)
                BrushLayer src = factory.GetBrush(RetinuesIconsBrush)?.GetLayer(TroopsId);
                if (src?.Sprite == null)
                    return; // Retinues' icon isn't ready (or failed to load): leave the slot as it is

                BrushLayer dst = taom.GetLayer(TroopsId);
                if (dst == null)
                {
                    dst = new BrushLayer();
                    dst.FillFrom(template); // same defaults as TAOM's other icon layers
                    dst.Name = TroopsId;
                    taom.AddLayer(dst);
                }
                if (!ReferenceEquals(dst.Sprite, src.Sprite))
                {
                    dst.Sprite = src.Sprite;
                    dst.HorizontalFlip = false;
                    if (!_iconLogged)
                    {
                        _iconLogged = true;
                        _log?.Invoke("Retinues troops icon added to TAOM map bar.");
                    }
                }
            }
            catch (Exception e)
            {
                if (!_iconErrorLogged)
                {
                    _iconErrorLogged = true;
                    try { _log?.Invoke("Map bar icon error (ignored, logged once): " + e.Message); } catch { }
                }
            }
        }

        // TAOM sets the label text only when ItemId changes and leaves unknown ids empty.
        private static void LabelOnUpdatePostfix(object __instance)
        {
            try
            {
                if (__instance is TextWidget w && string.IsNullOrEmpty(w.Text) && (_itemIdProp.GetValue(__instance) as string) == TroopsId)
                    w.Text = TroopsLabel;
            }
            catch (Exception e)
            {
                if (!_labelErrorLogged)
                {
                    _labelErrorLogged = true;
                    try { _log?.Invoke("Map bar label error (ignored, logged once): " + e.Message); } catch { }
                }
            }
        }
    }
}
