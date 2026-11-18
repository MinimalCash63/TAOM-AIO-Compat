// TAOM escape menu switch (new in v1.5.6, 18 Nov 2026).
// What: TAOM's own escape menu shows every item, so "Exit to Main Menu" is not hidden below the list.
// Why (Verified, TAOM 2.0.40 decompile and Modules\TAOM\GUI\Prefabs\FactionUI\TAOMEscapeMenu.xml; research note
// src\bannerlord\BannerlordCoop\research\2026-11-18-client-no-leave-and-unexpected-end.md):
//   - TAOM's GauntletLayerLoadMoviePatch swaps the vanilla "EscapeMenu" movie for "TAOMEscapeMenu" (no setting).
//   - That prefab puts the buttons in "MenuArea", Fixed 450 px, clipped and scrollable; a row is 50 + 2 px, so
//     about 8.6 rows show. The scrollbar is 4 px and hides itself when nothing overflows.
//   - RBM (Ledger), Retinues (Universal Editor) and Co-op (client items) take the list to 10 rows, in single
//     player, on the co-op host and on the client. Row 10 is "Exit to Main Menu".
// How: a postfix on GauntletLayer.LoadMovie(string, ViewModel). TAOM patches the same method and renames the
// movie in its prefix, so the loaded movie is named "TAOMEscapeMenu" here. Only when the rows do not fit, MenuArea
// is made taller, up to the free space above TAOM's lower divider (measured from TAOM's loaded widgets: 534 px
// with 2.0.40, enough for 10 rows). If rows still do not fit, scrolling stays and the scrollbar is widened to
// 8 px so it can be seen. Nothing else is moved or stretched; TAOM's files are not changed.
// Off, or TAOM not ticked: nothing is patched; TAOM's layout is untouched. With 8 items or fewer nothing changes.
// Co-op: local UI only; no game state is read or written. Players do not have to use the same value.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace TAOM_AIO_Compat.Fixes.TaomEscapeMenu
{
    internal sealed class TaomEscapeMenuFixes : FixGroup
    {
        internal override string SettingKey => "TaomEscapeMenuFixes";
        internal override string Name => "TAOM escape menu";
        internal override string TargetModuleId => "TAOM";

        private const string HarmonyId = "taom.compat.aio.escapemenu";
        internal const string MovieName = "TAOMEscapeMenu";
        private const string PanelId = "EscapeMenu";
        private const string MenuAreaId = "MenuArea";
        private const string ButtonsId = "ButtonsContainer";
        private const string ScrollbarId = "VerticalScrollbar";
        private const string HandleId = "VerticalScrollbarHandle";
        private const float DefaultRowHeight = 52f;
        private const float WideScrollbar = 8f;

        private static Action<string> _log;
        private static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);
        private static bool _errorLogged;
        private Harmony _harmony;

        internal override bool Install()
        {
            _log = Log;
            MethodInfo target = typeof(GauntletLayer).GetMethod("LoadMovie", BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(string), typeof(ViewModel) }, null);
            if (target == null || target.ReturnType != typeof(GauntletMovieIdentifier))
            {
                Log("GauntletLayer.LoadMovie(string, ViewModel) not found as expected. Nothing applied.");
                return false;
            }
            var h = new Harmony(HarmonyId);
            var postfix = new HarmonyMethod(typeof(TaomEscapeMenuFixes).GetMethod(nameof(LoadMoviePostfix), BindingFlags.Static | BindingFlags.NonPublic))
            {
                priority = Priority.Low
            };
            h.Patch(target, postfix: postfix);
            _harmony = h;
            Log("Escape menu list fit: installed (acts only on TAOM's " + MovieName + " when its items do not fit).");
            return true;
        }

        internal override void Uninstall()
        {
            if (_harmony == null)
                return;
            try { _harmony.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
        }

        private static void LoadMoviePostfix(GauntletMovieIdentifier __result)
        {
            try
            {
                if (__result == null || __result.MovieName != MovieName)
                    return;
                Widget root = __result.Movie?.RootWidget;
                if (root == null)
                    return;
                Fit(root, CountItems(__result.DataSource));
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    try { _log?.Invoke("Escape menu error (ignored, logged once; TAOM's layout stays as it is): " + e.Message); } catch { }
                }
            }
        }

        // EscapeMenuVM.MenuItems (MBBindingList<EscapeMenuItemVM>), read by name in case TAOM wraps the view model.
        private static int CountItems(ViewModel vm)
        {
            try
            {
                PropertyInfo p = vm?.GetType().GetProperty("MenuItems", BindingFlags.Instance | BindingFlags.Public);
                if (p?.GetValue(vm) is ICollection list)
                    return list.Count;
            }
            catch { }
            return 0;
        }

        private static void Fit(Widget root, int vmItems)
        {
            Widget panel = root.FindChild(PanelId, true);
            Widget area = panel?.FindChild(MenuAreaId, true);
            Widget buttons = area?.FindChild(ButtonsId, true);
            if (panel == null || area == null || buttons == null || area.HeightSizePolicy != SizePolicy.Fixed || panel.HeightSizePolicy != SizePolicy.Fixed)
            {
                LogOnce("layout", "TAOM's escape menu layout is not as expected (panel, MenuArea or ButtonsContainer missing, or not fixed height). Nothing changed.");
                return;
            }

            int rows = Math.Max(vmItems, buttons.ChildCount);
            float rowHeight = DefaultRowHeight;
            if (buttons.ChildCount > 0)
            {
                Widget first = buttons.GetChild(0);
                float h = first.SuggestedHeight + first.MarginTop + first.MarginBottom;
                if (first.HeightSizePolicy == SizePolicy.Fixed && h > 0f)
                    rowHeight = h;
            }
            float current = area.SuggestedHeight;
            float needed = rows * rowHeight;
            if (needed <= current)
                return; // everything fits: TAOM's layout stays as it is

            float max = MaxHeight(panel, area, current);
            float height = Math.Min(needed, max);
            if (height > current)
                area.SuggestedHeight = height;

            bool widened = false;
            if (needed > height)
            {
                Widget bar = area.FindChild(ScrollbarId, true);
                Widget handle = bar?.FindChild(HandleId, true);
                if (bar != null && bar.SuggestedWidth < WideScrollbar) { bar.SuggestedWidth = WideScrollbar; widened = true; }
                if (handle != null && handle.SuggestedWidth < WideScrollbar) { handle.SuggestedWidth = WideScrollbar; widened = true; }
            }

            LogOnce(rows + "|" + current + "|" + height,
                "TAOM escape menu: " + rows + " items; list height " + current + " -> " + height
                + (needed <= height ? " (all items visible)." : ", scrolling for the rest" + (widened ? "; scrollbar widened to 8 px." : ".")));
        }

        // Free space for MenuArea inside the panel without moving anything else: down to the highest
        // bottom-aligned child (TAOM's lower divider), keeping the same gap as between the upper divider and MenuArea.
        internal static float MaxHeight(Widget panel, Widget area, float current)
        {
            float topEdge = 0f;     // lowest bottom edge of top-aligned siblings above MenuArea
            float bottomBlock = 0f; // tallest stack of bottom-aligned siblings, from the panel's bottom
            for (int i = 0; i < panel.ChildCount; i++)
            {
                Widget c = panel.GetChild(i);
                if (c == null || c == area || !c.IsVisible || c.HeightSizePolicy != SizePolicy.Fixed)
                    continue;
                if (c.VerticalAlignment == VerticalAlignment.Bottom)
                    bottomBlock = Math.Max(bottomBlock, c.MarginBottom + c.SuggestedHeight);
                else if (c.VerticalAlignment == VerticalAlignment.Top)
                {
                    float edge = c.MarginTop + c.SuggestedHeight;
                    if (edge <= area.MarginTop)
                        topEdge = Math.Max(topEdge, edge);
                }
            }
            return Compute(panel.SuggestedHeight, area.MarginTop, topEdge, bottomBlock, current);
        }

        // Pure arithmetic, kept separate for the offline check.
        internal static float Compute(float panelHeight, float areaTop, float topEdge, float bottomBlock, float current)
        {
            float gap = Math.Max(0f, areaTop - topEdge);
            float max = panelHeight - bottomBlock - gap - areaTop;
            return max > current ? max : current;
        }

        private static void LogOnce(string key, string text)
        {
            if (Logged.Count > 50 || !Logged.Add(key))
                return;
            try { _log?.Invoke(text); } catch { }
        }
    }
}
