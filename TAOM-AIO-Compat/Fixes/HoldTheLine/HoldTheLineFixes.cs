// Hold the Line switch: brings back Hold the Line's "Depth morale" under TAOM.
// Hold the Line (b0.2.4) patches CalculateMoraleChangeToCharacter(Agent,float) on the running morale model's
// own type. Under TAOM that is TaomBattleMoraleModel, which only inherits the method from
// SandboxBattleMoraleModel, and Harmony refuses inherited methods ("Depth morale could not be set up").
// This group patches the declaring method instead and calls Hold the Line's own public
// DepthMorale.MoraleChangePostfix from there, only when the running model inherits the method (otherwise
// Hold the Line's own patch is in place and this does nothing). It also tells Hold the Line the method is
// handled, so it does not try, fail and show its error message.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.HoldTheLine
{
    internal sealed class HoldTheLineFixes : FixGroup
    {
        internal override string SettingKey => "HoldTheLineFixes";
        internal override string Name => "Hold the Line";
        internal override string TargetModuleId => "HoldTheLine";

        private const string HarmonyId = "taom.compat.aio.holdtheline";
        private const string AssemblyName = "HoldTheLine";
        private const string DepthMoraleTypeName = "HoldTheLine.DepthMorale";
        private const string MethodName = "CalculateMoraleChangeToCharacter";
        private static readonly Type[] MethodArgs = { typeof(Agent), typeof(float) };

        private delegate void MoralePostfix(Agent agent, ref float result);

        private static MoralePostfix _htlPostfix;
        private static FieldInfo _htlModels;
        private static Action<string> _log;
        private static bool _errorLogged, _activeLogged;
        // Per running model type: does it inherit the patched method (true = we must call Hold the Line)?
        private static readonly Dictionary<Type, bool> Inherits = new Dictionary<Type, bool>();
        private static readonly HashSet<MethodBase> Declaring = new HashSet<MethodBase>();
        private Harmony _harmony;

        internal override bool Install()
        {
            _log = Log;
            Type depth = ModEnv.FindType(AssemblyName, DepthMoraleTypeName);
            MethodInfo htl = depth?.GetMethod("MoraleChangePostfix", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Agent), typeof(float).MakeByRefType() }, null);
            if (htl == null)
            {
                Log("Hold the Line " + DepthMoraleTypeName + ".MoraleChangePostfix(Agent, ref float) not found. Nothing changed.");
                return false;
            }
            _htlPostfix = (MoralePostfix)Delegate.CreateDelegate(typeof(MoralePostfix), htl);
            _htlModels = depth.GetField("_models", BindingFlags.NonPublic | BindingFlags.Static);

            var h = new Harmony(HarmonyId);
            _harmony = h; // v1.3.0: set before patching, so Uninstall can remove a half-applied set
            int patched = 0;
            foreach (string typeName in new[] { "SandBox.GameComponents.SandboxBattleMoraleModel", "TaleWorlds.MountAndBlade.CustomBattleMoraleModel" })
            {
                Type t = AccessTools.TypeByName(typeName);
                MethodInfo m = t == null ? null : AccessTools.DeclaredMethod(t, MethodName, MethodArgs);
                if (m == null)
                {
                    Log(typeName + "." + MethodName + " not found (skipped).");
                    continue;
                }
                h.Patch(m, postfix: new HarmonyMethod(typeof(HoldTheLineFixes).GetMethod(nameof(MoralePostfixOnBase), BindingFlags.Static | BindingFlags.NonPublic)));
                Declaring.Add(m);
                patched++;
                Log("Postfix on " + typeName + "." + MethodName + ".");
            }
            if (patched == 0)
            {
                Log("No morale model method found. Nothing changed.");
                return false;
            }

            MethodInfo ensure = depth.GetMethod("EnsurePatched", BindingFlags.Public | BindingFlags.Static);
            if (ensure != null && _htlModels != null)
            {
                try
                {
                    h.Patch(ensure, prefix: new HarmonyMethod(typeof(HoldTheLineFixes).GetMethod(nameof(EnsurePatchedPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                    Log("Hold the Line's error message for the inherited method suppressed.");
                }
                catch (Exception e) { Log("Could not hook EnsurePatched (only its message is affected): " + e.Message); }
            }
            return true;
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
        }

        // True when the running model's type gets the method from one of the patched declaring methods
        // (no type between it and the declaring type declares its own version).
        private static bool ModelInherits(Type modelType)
        {
            lock (Inherits)
            {
                if (Inherits.TryGetValue(modelType, out bool v))
                    return v;
                // AccessTools.Method walks up from modelType and returns the most-derived declaration.
                MethodInfo resolved = AccessTools.Method(modelType, MethodName, MethodArgs);
                MethodBase declared = resolved == null ? null : AccessTools.DeclaredMethod(resolved.DeclaringType, MethodName, MethodArgs);
                v = declared != null && resolved.DeclaringType != modelType && Declaring.Contains(declared);
                Inherits[modelType] = v;
                return v;
            }
        }

        private static void MoralePostfixOnBase(object __instance, object[] __args, ref float __result)
        {
            try
            {
                // v1.2.3 (14 Nov 2026): field battles only. Hold the Line's Depth morale acts only when
                // Mission.Current.IsFieldBattle; outside field battles (tournaments, arenas, sieges, hideouts,
                // town fights) the AIO no longer calls into Hold the Line at all. Managed checks only.
                Mission m = Mission.Current;
                if (m == null || !m.IsFieldBattle)
                    return;
                if (__instance == null || !ModelInherits(__instance.GetType()) || __args == null || __args.Length < 1)
                    return;
                if (!_activeLogged)
                {
                    _activeLogged = true;
                    _log?.Invoke("Depth morale hook first used in a field battle (" + __instance.GetType().FullName + ").");
                }
                _htlPostfix(__args[0] as Agent, ref __result);
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    try { _log?.Invoke("Depth morale call error (ignored, logged once): " + e.Message); } catch { }
                }
            }
        }

        // Runs before Hold the Line's DepthMorale.EnsurePatched: if the running model only inherits the method,
        // mark it as handled in Hold the Line's own _models set so it does not try (and fail) to patch it.
        private static void EnsurePatchedPrefix()
        {
            try
            {
                Type modelType = MissionGameModels.Current?.BattleMoraleModel?.GetType();
                if (modelType == null || !ModelInherits(modelType))
                    return;
                MethodInfo m = AccessTools.Method(modelType, MethodName, MethodArgs); // same call Hold the Line makes
                if (m != null && _htlModels.GetValue(null) is HashSet<MethodInfo> set)
                    set.Add(m);
            }
            catch { }
        }
    }
}
