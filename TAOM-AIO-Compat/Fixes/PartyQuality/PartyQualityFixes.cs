// Party Quality switch (new in v1.4.0, audit 14 Nov 2026).
// What: while TAOM's Hidden Hosts hides a party or army in its map tooltip, Party Quality 1.2.2 adds nothing
// to that tooltip.
// Why (Verified, decompiles of the installed PartyQuality.dll and TAOM.dll 2.0.39, 14 Nov 2026):
//   - PartyQuality.MobilePartyTooltipPatch.Postfix and PartyQuality.ArmyTooltipPatch.Postfix (postfixes on
//     TooltipRefresherCollection.RefreshMobilePartyTooltip / RefreshArmyTooltip) rewrite the tooltip title with
//     the leader's name, a quality rating computed from the real troop roster, and the rating's colour.
//   - TAOM's Hidden Hosts shows far parties as "Unknown party" (Horizon) and masks the troop count and troop
//     rows of nearer ones (Masked). Party Quality's title gives away what TAOM hides in both cases.
//   - While a party or army tooltip is built, TAOM keeps its verdict in
//     TAOM.Features.HiddenHosts.HiddenHostsTooltipScope.Current (thread-static; set by TAOM's prefix, cleared by
//     its finalizer after all postfixes, so the order of the two mods' postfixes does not matter).
// How: a prefix on the two Party Quality postfix methods skips them when Current is not null, its Kind is
// Party (1) or Army (2) and its Verdict.Mode is not Vanilla (0). TAOM is only read, never patched. TAOM's types
// are internal and are found by name; if any is missing the switch installs nothing and says so in the log.
// When: at load. If PartyQuality.dll is not loaded yet at that point, once more at the main menu.
// Off, or Party Quality / TAOM not ticked: nothing is patched.
// Co-op: tooltips are local UI. The prefix reads one thread-static value and writes nothing, so host, client
// and single player behave the same and the setting does not have to match between players.
using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.PartyQuality
{
    internal sealed class PartyQualityFixes : FixGroup
    {
        internal override string SettingKey => "PartyQualityFixes";
        internal override string Name => "Party Quality";
        internal override string TargetModuleId => "PartyQuality";

        private const string HarmonyId = "taom.compat.aio.partyquality";
        private const string TaomModuleId = "TAOM";
        private const string TaomAssembly = "TAOM";
        private const string ScopeTypeName = "TAOM.Features.HiddenHosts.HiddenHostsTooltipScope";
        private const string PqAssembly = "PartyQuality";
        private const string PartyPatchTypeName = "PartyQuality.MobilePartyTooltipPatch";
        private const string ArmyPatchTypeName = "PartyQuality.ArmyTooltipPatch";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        // True while TAOM hides the party or army whose tooltip is being built on this thread.
        private static Func<bool> _taomHides;
        private static Action<string> _log;
        private static bool _skipLogged;
        private static bool _errorLogged;

        private Harmony _harmony;
        private bool _patched;
        private bool _retryAtMenu;

        internal override bool Install()
        {
            _log = Log;
            if (!ModEnv.IsActive(TaomModuleId))
            {
                Log("TAOM is not ticked: there is nothing for Party Quality to give away. Nothing applied.");
                return false;
            }
            string why;
            Func<bool> test = BuildScopeTest(ModEnv.FindAssembly(TaomAssembly), out why);
            if (test == null)
            {
                Log("TAOM's Hidden Hosts tooltip scope not found (" + why + "). Nothing changed; Party Quality shows its title for hidden parties too.");
                return false;
            }
            _taomHides = test;

            if (ModEnv.FindAssembly(PqAssembly) == null)
            {
                _retryAtMenu = true;
                Log("PartyQuality.dll is not loaded yet; the tooltip guard is applied at the main menu.");
                return true;
            }
            return Patch("load");
        }

        internal override void OnMainMenu()
        {
            if (!_retryAtMenu || _patched)
                return;
            _retryAtMenu = false; // one attempt
            try
            {
                if (!Patch("main menu"))
                    Broken = true;
            }
            catch (Exception e)
            {
                Broken = true;
                Uninstall();
                Log("Tooltip guard could not be installed at the main menu; switched off for this session: " + e.Message);
            }
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _patched = false;
            _retryAtMenu = false;
            _taomHides = null;
        }

        private bool Patch(string when)
        {
            MethodInfo party = FindPostfix(PartyPatchTypeName);
            MethodInfo army = FindPostfix(ArmyPatchTypeName);
            MethodInfo prefix = typeof(PartyQualityFixes).GetMethod(nameof(SkipWhenTaomHides), BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null || (party == null && army == null))
            {
                Log(when + ": " + PartyPatchTypeName + ".Postfix and " + ArmyPatchTypeName + ".Postfix not found. Nothing changed.");
                return false;
            }
            var h = new Harmony(HarmonyId);
            _harmony = h;
            int done = 0;
            done += PatchOne(h, party, prefix, PartyPatchTypeName);
            done += PatchOne(h, army, prefix, ArmyPatchTypeName);
            if (done == 0)
            {
                Uninstall();
                return false;
            }
            _patched = true;
            Log(when + ": Party Quality adds nothing to the tooltip of a party or army that TAOM's Hidden Hosts hides (" + done + " of 2 guards).");
            return true;
        }

        private int PatchOne(Harmony h, MethodInfo target, MethodInfo prefix, string typeName)
        {
            if (target == null)
            {
                Log(typeName + ".Postfix not found; that tooltip is not guarded.");
                return 0;
            }
            try
            {
                h.Patch(target, prefix: new HarmonyMethod(prefix));
                return 1;
            }
            catch (Exception e)
            {
                Log("Guard on " + typeName + ".Postfix could not be installed: " + e.Message);
                return 0;
            }
        }

        private static MethodInfo FindPostfix(string typeName)
        {
            try
            {
                Type t = ModEnv.FindType(PqAssembly, typeName);
                MethodInfo m = t == null ? null : AccessTools.DeclaredMethod(t, "Postfix");
                return m != null && m.IsStatic ? m : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Compiles "Current != null &amp;&amp; (Kind == 1 || Kind == 2) &amp;&amp; Verdict.Mode != 0" against TAOM's internal types,
        /// found by name. Returns null and the reason when a member is missing or has an unexpected type.
        /// </summary>
        internal static Func<bool> BuildScopeTest(Assembly taom, out string why)
        {
            why = null;
            try
            {
                if (taom == null) { why = "assembly " + TaomAssembly + " not loaded"; return null; }
                Type scope = taom.GetType(ScopeTypeName, false);
                if (scope == null) { why = "type " + ScopeTypeName; return null; }
                PropertyInfo current = scope.GetProperty("Current", Any);
                if (current == null || current.GetGetMethod(true) == null || !current.GetGetMethod(true).IsStatic || current.PropertyType.IsValueType)
                { why = "static property Current"; return null; }
                Type frame = current.PropertyType;
                PropertyInfo kind = frame.GetProperty("Kind", Any);
                PropertyInfo verdict = frame.GetProperty("Verdict", Any);
                if (kind == null || !kind.PropertyType.IsEnum) { why = "property Kind (enum) on " + frame.FullName; return null; }
                if (verdict == null) { why = "property Verdict on " + frame.FullName; return null; }
                PropertyInfo mode = verdict.PropertyType.GetProperty("Mode", Any);
                if (mode == null || !mode.PropertyType.IsEnum) { why = "property Mode (enum) on " + verdict.PropertyType.FullName; return null; }

                ParameterExpression f = Expression.Variable(frame, "frame");
                Expression kindInt = Expression.Convert(Expression.Property(f, kind), typeof(int));
                Expression modeInt = Expression.Convert(Expression.Property(Expression.Property(f, verdict), mode), typeof(int));
                Expression body = Expression.Block(typeof(bool), new[] { f },
                    Expression.Assign(f, Expression.Property(null, current)),
                    Expression.AndAlso(
                        Expression.NotEqual(f, Expression.Constant(null, frame)),
                        Expression.AndAlso(
                            Expression.OrElse(
                                Expression.Equal(kindInt, Expression.Constant(1)),   // TooltipSubjectKind.Party
                                Expression.Equal(kindInt, Expression.Constant(2))),  // TooltipSubjectKind.Army
                            Expression.NotEqual(modeInt, Expression.Constant(0))))); // TooltipMode.Vanilla
                return Expression.Lambda<Func<bool>>(body).Compile();
            }
            catch (Exception e)
            {
                why = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        // Harmony prefix on Party Quality's two tooltip postfixes. False = Party Quality's postfix is skipped.
        private static bool SkipWhenTaomHides()
        {
            try
            {
                Func<bool> test = _taomHides;
                if (test == null || !test())
                    return true;
                if (!_skipLogged)
                {
                    _skipLogged = true;
                    try { _log?.Invoke("First tooltip of a party or army hidden by TAOM: Party Quality's title was left out (logged once)."); } catch { }
                }
                return false;
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    try { _log?.Invoke("Tooltip guard error (caught, logged once; Party Quality runs as usual): " + e); } catch { }
                }
                return true;
            }
        }
    }
}
