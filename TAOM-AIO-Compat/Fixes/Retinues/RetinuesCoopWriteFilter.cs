// RetinuesCoopWriteFilter: on a co-op client, drops Retinues' writes of a troop property when the troop
// already has that value (Retinues 2.0.0.15 by bolaft). Retinues.dll is not referenced; the target is found
// by name and is left alone when it is not found (one log line).
//
// Why (audit 14 Nov 2026, Coop_client.log copies of 13 and 14 Nov and the installed DLLs): Co-op logs every
// write a client makes to a synced troop property, with a stack trace, also when the value does not change.
// Retinues re-sets three such properties all the time through Retinues.Utilities.Reflection.SetPropertyValue:
//   HiddenInEncyclopedia   AIClanRetinuesBehavior.SyncEncyclopediaVisibilityForClan on every load
//                          (about 380 writes per join)
//   DefaultFormationClass, DefaultFormationGroup   WCharacter.UpdateFormationClass after every equipment
//                          touch, including StagedItemProgressBehavior's hourly progress step
//                          (1,660 to 5,100 writes per session)
// These write the value that is already there: log noise and stack-trace cost, no state damage.
//
// What: prefix on Retinues.Utilities.Reflection.SetPropertyValue(object instance, string propertyName,
// object value) (public static void). The call is skipped only when all of this holds: Coop is ticked, the
// property is one of the three above, the instance is a BasicCharacterObject, Co-op is blocking local writes
// on this client right now (CoopEnv.IsClientWriteBlocked()), and the current value equals the new value after
// converting it to the property's type. A real change always goes through, and so does every call where
// anything is unclear, so a client can never end up with a different troop than without this filter.
//
// Installed once (TryInstall is called at the main menu, game start and mission start until it returns
// true) and kept until the switch is uninstalled; the condition is evaluated on every call.
// Co-op: host's server process and single player: never skips. Client: nothing visible changes; the log
// lines for unchanged values stop. Retinues' own bookkeeping (its "changed" mark for saving) is set before
// this call and is not affected.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.Retinues
{
    internal static class RetinuesCoopWriteFilter
    {
        private const string HarmonyId = "taom.compat.aio.retinues.coopwritefilter";
        private const string RetinuesAssembly = "Retinues";
        private const string ReflectionType = "Retinues.Utilities.Reflection";
        private const string HiddenInEncyclopedia = "HiddenInEncyclopedia";
        private const string DefaultFormationClass = "DefaultFormationClass";
        private const string DefaultFormationGroup = "DefaultFormationGroup";

        private static Harmony _harmony;
        private static bool _done;
        private static bool _loggedSkip, _loggedError;
        private static Action<string> _log = _ => { };

        // (runtime type, property name) -> property with a getter, or null when there is none. Game thread
        // in practice; the lock keeps it safe if Retinues ever calls from another thread.
        private static readonly Dictionary<KeyValuePair<Type, string>, PropertyInfo> Properties = new Dictionary<KeyValuePair<Type, string>, PropertyInfo>();

        /// <summary>
        /// False while Retinues.dll is not loaded yet (call again at the next event). True when finished:
        /// installed, or not installable (one log line). Never throws.
        /// </summary>
        internal static bool TryInstall(Action<string> log)
        {
            if (_done)
                return true;
            _log = log ?? (_ => { });
            try
            {
                Assembly asm = ModEnv.FindAssembly(RetinuesAssembly);
                if (asm == null)
                    return false;
                _done = true;
                Type reflection = asm.GetType(ReflectionType, false);
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo target = reflection?.GetMethod("SetPropertyValue", flags, null, new[] { typeof(object), typeof(string), typeof(object) }, null);
                if (target == null || target.ReturnType != typeof(void) || target.IsGenericMethodDefinition)
                {
                    _log("Co-op write filter: " + ReflectionType + ".SetPropertyValue(object, string, object) not found. Not installed.");
                    return true;
                }
                var h = new Harmony(HarmonyId);
                _harmony = h;
                h.Patch(target, prefix: new HarmonyMethod(typeof(RetinuesCoopWriteFilter).GetMethod(nameof(SkipUnchangedClientWrite), BindingFlags.Static | BindingFlags.NonPublic)));
                _log("Co-op write filter installed: Reflection.SetPropertyValue(Object, String, Object).");
            }
            catch (Exception e)
            {
                _done = true;
                _log("Co-op write filter: install failed: " + e.Message + ".");
            }
            return true;
        }

        internal static void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _done = false;
            lock (Properties) { Properties.Clear(); }
        }

        // Returning false skips Retinues' SetPropertyValue. Parameter names are those of Retinues' method.
        // Cheapest tests first: this runs for every property Retinues sets.
        private static bool SkipUnchangedClientWrite(object instance, string propertyName, object value)
        {
            try
            {
                if (!CoopEnv.IsCoopTicked)
                    return true;
                if (!IsFilteredProperty(propertyName))
                    return true;
                if (!(instance is BasicCharacterObject))
                    return true;
                if (!CoopEnv.IsClientWriteBlocked())
                    return true;
                if (!IsUnchanged(instance, propertyName, value))
                    return true;
                if (!_loggedSkip)
                {
                    _loggedSkip = true;
                    try { _log("Co-op client: dropped a Retinues write of " + propertyName + " that would not change the value; further ones are dropped without a line. Logged once."); } catch { }
                }
                return false;
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    try { _log("Co-op write filter failed (Retinues writes as usual): " + e.Message); } catch { }
                }
                return true;
            }
        }

        internal static bool IsFilteredProperty(string propertyName) =>
            propertyName == HiddenInEncyclopedia || propertyName == DefaultFormationClass || propertyName == DefaultFormationGroup;

        /// <summary>
        /// True only when the instance has a readable property of that name and its current value equals the
        /// new value converted to the property's type. Anything unclear (no property, null, a value that does
        /// not convert) is "changed", so the write goes through.
        /// </summary>
        internal static bool IsUnchanged(object instance, string propertyName, object value)
        {
            if (instance == null || propertyName == null || value == null)
                return false;
            PropertyInfo property = FindProperty(instance.GetType(), propertyName);
            if (property == null)
                return false;
            Type type = property.PropertyType;
            if (!type.IsValueType)
                return false; // the three properties are bool, an enum and int; anything else is not ours
            object wanted;
            if (value.GetType() == type)
                wanted = value;
            else if (type.IsEnum)
            {
                if (!IsIntegral(value))
                    return false;
                wanted = Enum.ToObject(type, value);
            }
            else if (type == typeof(int) && (value is Enum || IsIntegral(value)))
                wanted = Convert.ToInt32(value);
            else
                return false;
            object current = property.GetValue(instance, null);
            return current != null && current.Equals(wanted);
        }

        private static bool IsIntegral(object value) =>
            value is int || value is byte || value is sbyte || value is short || value is ushort || value is uint || value is long || value is ulong;

        private static PropertyInfo FindProperty(Type type, string name)
        {
            var key = new KeyValuePair<Type, string>(type, name);
            lock (Properties)
            {
                if (Properties.TryGetValue(key, out PropertyInfo known))
                    return known;
                PropertyInfo found = null;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (Type t = type; t != null && found == null; t = t.BaseType)
                {
                    PropertyInfo p = t.GetProperty(name, flags);
                    if (p != null && p.GetIndexParameters().Length == 0 && p.GetGetMethod(true) != null)
                        found = p;
                }
                Properties[key] = found;
                return found;
            }
        }
    }
}
