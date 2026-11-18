// Copied from TAOM_Standalone_Compat_RBM v1.0.11 (TargetResolver, RbmPatchBoolFinalizer, RbmPatchVoidFinalizer,
// RbmBlowFinalizer, RbmTournamentTierFinalizer, SuppressionReporter, CustomArcherRange, CompatResolver.SafeMethods)
// by Theb0yys, TAOM team, with permission (owner, 13 Nov 2026). Updated for RBM 4.8.1:
//  - the root "RBMAI.OverrideFormation" is now "RBMAI.Frontline+OverrideFormation" (RBM 4.8.1 moved the class;
//    RBMAI\AiModule\Frontline\FrontlinePositioning.cs). With it all 34 roots resolve: 48 bool prefixes and
//    43 void patch methods (checked with Mono.Cecil against the installed RBM 4.8.1, 13 Nov 2026);
//  - the finalizers are unchanged (owner/reviewer decision 13 Nov 2026): an exception inside one of RBM's patch
//    methods is swallowed and logged once; a bool prefix then returns true so the game's own method runs.
//    This can hide real RBM errors; the AIO log names each method the first time;
//  - CustomArcherRange's prefix and postfix are in try/catch; log lines go to the AIO log.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Fixes.RBM.Content;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.RBM.Battle
{
    internal static class TargetResolver
    {
        private sealed class PatchTargetCatalog
        {
            internal MethodBase[] BoolMethods;
            internal HashSet<MethodBase> BoolPrefixes;
            internal MethodBase[] VoidMethods;
            internal string[] MissingTypes;
        }

        private static readonly string[] PatchTypeRoots =
        {
            "RBM.XmlLoadingPatches", "RBMCombat.ModifyArmorPatch", "RBMCombat.ModifyModifyDamagePatch", "RBMCombat.GetAttackCollisionResultsPatch",
            "RBMCombat.RangedRework+OverrideRangedSiegeWeapon", "RBMCombat.RangedRework+OverrideMangonel", "RBMCombat.RangedRework+HandleMissileCollisionReactionPatch",
            "RBMCombat.RangedRework+MeleeHitCallbackPatch", "RBMCombat.RangedRework+MissileHitCallbackPatch", "RBMCombat.RealisticWeaponCollision",
            "RBMAI.OverrideBehaviorCautiousAdvance", "RBMAI.OverrideBehaviorMountedSkirmish", "RBMAI.OverrideBehaviorProtectFlank", "RBMAI.AdjustSpeedLimitPatch",
            "RBMAI.OverrideBehaviorHorseArcherSkirmish", "RBMAI.OverrideBehaviorPullBack", "RBMAI.OverrideBehaviorVanguard", "RBMAI.OverrideHumanAIComponent",
            "RBMAI.OverrideMovementOrder", "RBMAI.OverrideAgent",
            "RBMAI.OverrideUpdateFormationOrders", "RBMAI.SetPositioningPatch", "RBMAI.OverrideSetMovementOrder", "RBMAI.OverrideBehaviorRegroup",
            "RBMAI.OverrideBehaviorAdvance", "RBMAI.OverrideLineFormation", "RBMAI.Frontline+OverrideFormation", "RBMAI.OverrideFormationMovementComponent",
            "RBMAI.TacticDefensiveEngagementPatch", "RBMAI.SiegePatches",
            "RBMAI.AiModule.SpawningPatches", "RBMAI.AiModule.RbmTactics.TacticDefensiveLinePatch", "RBMAI.AiModule.RbmTactics.TacticFullScaleAttackPatch",
            "RBMAI.AiModule.RbmBehaviors.OverrideBehaviorCharge",
        };

        private static PatchTargetCatalog _catalog;
        private static MethodBase[] _blowMethods = Array.Empty<MethodBase>();
        private static MethodBase[] _tournamentMethods = Array.Empty<MethodBase>();

        internal static string[] LastMissingTypes = Array.Empty<string>();
        internal static int BoolCount => _catalog?.BoolMethods.Length ?? 0;
        internal static int VoidCount => _catalog?.VoidMethods.Length ?? 0;

        internal static IEnumerable<MethodBase> PatchBoolMethods() => _catalog?.BoolMethods ?? Array.Empty<MethodBase>();
        internal static bool IsBoolPrefix(MethodBase method) => _catalog?.BoolPrefixes.Contains(method) ?? false;
        internal static IEnumerable<MethodBase> PatchVoidMethods() => _catalog?.VoidMethods ?? Array.Empty<MethodBase>();
        internal static IEnumerable<MethodBase> RbmBlowMethods() => _blowMethods;
        internal static IEnumerable<MethodBase> TournamentTierMethods() => _tournamentMethods;

        /// <summary>True when every root, the Blow method and the tier method resolve. Logs the counts when it is not ready.</summary>
        internal static bool TryInitialize(out string problem)
        {
            problem = null;
            if (_catalog != null)
                return true;
            PatchTargetCatalog catalog = ResolvePatchCatalog();
            MethodBase[] blow = ResolveBlowMethods();
            MethodBase[] tier = ResolveTournamentMethods();
            LastMissingTypes = catalog.MissingTypes;
            if (catalog.MissingTypes.Length != 0 || blow.Length != 1 || tier.Length != 1)
            {
                problem = "RBM battle targets not complete: " + catalog.BoolMethods.Length + " bool and " + catalog.VoidMethods.Length
                    + " void patch methods, missing roots " + catalog.MissingTypes.Length
                    + (catalog.MissingTypes.Length == 0 ? "" : " (" + string.Join(", ", catalog.MissingTypes) + ")")
                    + ", Blow targets " + blow.Length + ", tournament tier targets " + tier.Length + ".";
                return false;
            }
            _blowMethods = blow;
            _tournamentMethods = tier;
            _catalog = catalog;
            return true;
        }

        private static PatchTargetCatalog ResolvePatchCatalog()
        {
            var bools = new List<MethodBase>();
            var boolPrefixes = new HashSet<MethodBase>();
            var voids = new List<MethodBase>();
            var missing = new List<string>();
            var seen = new HashSet<MethodBase>();
            foreach (string root in PatchTypeRoots)
            {
                Type rootType = AccessTools.TypeByName(root);
                if (rootType == null)
                {
                    missing.Add(root);
                    continue;
                }
                foreach (Type type in GetTypeTree(rootType))
                {
                    foreach (MethodInfo method in SafeMethods(type))
                    {
                        if (method == null || method.IsSpecialName || method.DeclaringType != type || !IsHarmonyPrefixOrPostfix(method) || !seen.Add(method))
                            continue;
                        if (method.ReturnType == typeof(bool))
                        {
                            bools.Add(method);
                            if (IsHarmonyPrefix(method))
                                boolPrefixes.Add(method);
                        }
                        else if (method.ReturnType == typeof(void))
                        {
                            voids.Add(method);
                        }
                    }
                }
            }
            return new PatchTargetCatalog { BoolMethods = bools.ToArray(), BoolPrefixes = boolPrefixes, VoidMethods = voids.ToArray(), MissingTypes = missing.ToArray() };
        }

        private static List<Type> GetTypeTree(Type rootType)
        {
            var list = new List<Type>();
            var stack = new Stack<Type>();
            stack.Push(rootType);
            while (stack.Count > 0)
            {
                Type type = stack.Pop();
                list.Add(type);
                try
                {
                    foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                        stack.Push(nested);
                }
                catch (Exception e)
                {
                    RbmLog.Write("Could not inspect nested RBM patch types of " + type.FullName + ": " + e.Message);
                }
            }
            return list;
        }

        private static IEnumerable<MethodInfo> SafeMethods(Type type)
        {
            try
            {
                return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch (Exception e)
            {
                RbmLog.Write("Could not inspect " + type.FullName + ": " + e.Message);
                return Array.Empty<MethodInfo>();
            }
        }

        private static bool IsHarmonyPrefixOrPostfix(MethodInfo method)
        {
            foreach (CustomAttributeData attribute in method.CustomAttributes)
            {
                string name = attribute.AttributeType.FullName;
                if (name == typeof(HarmonyPrefix).FullName || name == typeof(HarmonyPostfix).FullName)
                    return true;
            }
            return method.Name.StartsWith("Prefix", StringComparison.Ordinal) || method.Name.StartsWith("Postfix", StringComparison.Ordinal)
                || method.Name.EndsWith("Prefix", StringComparison.Ordinal) || method.Name.EndsWith("Postfix", StringComparison.Ordinal);
        }

        private static bool IsHarmonyPrefix(MethodInfo method)
        {
            bool prefixAttribute = false;
            foreach (CustomAttributeData attribute in method.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == typeof(HarmonyPostfix).FullName)
                    return false;
                if (attribute.AttributeType.FullName == typeof(HarmonyPrefix).FullName)
                    prefixAttribute = true;
            }
            return prefixAttribute || method.Name.StartsWith("Prefix", StringComparison.Ordinal) || method.Name.EndsWith("Prefix", StringComparison.Ordinal);
        }

        private static MethodBase[] ResolveBlowMethods()
        {
            Type type = AccessTools.TypeByName("RBMCombat.RealisticWeaponCollision");
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "CreateMeleeBlowPostFix", new[]
            {
                typeof(Blow), typeof(Agent), typeof(Agent), typeof(AttackCollisionData).MakeByRefType(), typeof(MissionWeapon).MakeByRefType(),
                typeof(CrushThroughState), typeof(Vec3), typeof(Vec3), typeof(bool),
            });
            return method != null && method.ReturnType == typeof(Blow) ? new MethodBase[] { method } : Array.Empty<MethodBase>();
        }

        private static MethodBase[] ResolveTournamentMethods()
        {
            Type type = AccessTools.TypeByName("RBMTournament.RBMTournament");
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "calculatePlayerTournamentTier", Type.EmptyTypes);
            return method != null && method.ReturnType == typeof(int) ? new MethodBase[] { method } : Array.Empty<MethodBase>();
        }
    }

    internal static class SuppressionReporter
    {
        internal static void ReportOnce(string group, MethodBase method, Exception exception)
        {
            string name = method == null ? "<unknown>" : (method.DeclaringType == null ? "<no type>" : method.DeclaringType.FullName) + "::" + method.Name;
            RbmLog.WriteOnce("suppressed|" + group + "|" + name, "Suppressed exception in " + group + " target " + name + " (crash guard, logged once): " + exception);
        }
    }

    [HarmonyPatch]
    internal static class PatchBoolFinalizer
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> TargetMethods() => TargetResolver.PatchBoolMethods();

        internal static Exception Finalizer(MethodBase __originalMethod, Exception __exception, ref bool __result)
        {
            if (__exception != null)
            {
                if (TargetResolver.IsBoolPrefix(__originalMethod))
                    __result = true;
                SuppressionReporter.ReportOnce("RBM bool patch", __originalMethod, __exception);
            }
            return null;
        }
    }

    [HarmonyPatch]
    internal static class PatchVoidFinalizer
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> TargetMethods() => TargetResolver.PatchVoidMethods();

        internal static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
        {
            if (__exception != null)
                SuppressionReporter.ReportOnce("RBM void patch", __originalMethod, __exception);
            return null;
        }
    }

    [HarmonyPatch]
    internal static class BlowFinalizer
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> TargetMethods() => TargetResolver.RbmBlowMethods();

        // __args, not the parameter: RBM's first parameter is itself named __result (Harmony's special name).
        internal static Exception Finalizer(MethodBase __originalMethod, object[] __args, Exception __exception, ref Blow __result)
        {
            if (__exception != null)
            {
                if (__args != null && __args.Length != 0 && __args[0] is Blow blow)
                    __result = blow;
                SuppressionReporter.ReportOnce("RBM Blow patch", __originalMethod, __exception);
            }
            return null;
        }
    }

    [HarmonyPatch]
    internal static class TournamentTierFinalizer
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> TargetMethods() => TargetResolver.TournamentTierMethods();

        internal static Exception Finalizer(MethodBase __originalMethod, Exception __exception, ref int __result)
        {
            if (__exception != null)
            {
                __result = 1;
                SuppressionReporter.ReportOnce("RBM tournament tier", __originalMethod, __exception);
            }
            return null;
        }
    }

    /// <summary>Keeps Agent.AISpecialCombatModeFlags.IgnoreAmmoLimitForRangeCalculation (0x400) on ranged agents after RBM's AI sets its flags.</summary>
    [HarmonyPatch]
    internal static class ArcherRange
    {
        internal struct RangeState
        {
            internal bool Active;
            internal Agent.AISpecialCombatModeFlags Previous;
        }

        private const Agent.AISpecialCombatModeFlags RangeFlag = Agent.AISpecialCombatModeFlags.IgnoreAmmoLimitForRangeCalculation;

        [HarmonyTargetMethod]
        internal static MethodBase TargetMethod() =>
            AccessTools.DeclaredMethod(AccessTools.TypeByName("RBMAI.AgentAi+OverrideSetAiRelatedProperties"), "Postfix");

        [HarmonyPrefix]
        internal static void Prefix(Agent agent, out RangeState __state)
        {
            __state = default;
            try
            {
                if (ContentRepairs.CustomAmmoLoaded && agent != null && agent.IsRangedCached)
                {
                    __state.Active = true;
                    __state.Previous = agent.GetScriptedCombatFlags();
                }
            }
            catch (Exception e)
            {
                __state = default;
                RbmLog.WriteOnce("archerrange", "Archer range flag: not kept (error caught): " + e.Message);
            }
        }

        [HarmonyPostfix]
        internal static void Postfix(Agent agent, RangeState __state)
        {
            try
            {
                if (!__state.Active || agent == null)
                    return;
                Agent.AISpecialCombatModeFlags current = agent.GetScriptedCombatFlags();
                Agent.AISpecialCombatModeFlags restored = RestoreRangeFlag(__state.Previous, current);
                if (restored != current)
                    agent.SetScriptedCombatFlags(restored);
            }
            catch (Exception e)
            {
                RbmLog.WriteOnce("archerrange", "Archer range flag: not kept (error caught): " + e.Message);
            }
        }

        internal static Agent.AISpecialCombatModeFlags RestoreRangeFlag(Agent.AISpecialCombatModeFlags previous, Agent.AISpecialCombatModeFlags current) =>
            (current & ~RangeFlag) | (previous & RangeFlag);
    }
}
