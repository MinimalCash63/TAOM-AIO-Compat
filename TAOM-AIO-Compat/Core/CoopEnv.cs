// What the Co-op mod (module id "Coop") is doing right now, read by reflection only: this DLL never
// references Co-op's or TAOM's assemblies. Every member returns a safe default when Coop is not ticked,
// when a name is not found (logged once) or when anything throws. Nothing here patches anything.
// Names checked against the installed Coop build of 14 Nov 2026 and TAOM 2.0.39 (plan review, v1.4.0):
//   Common.ModInformation.IsServer                                  public static bool property
//   GameInterface.ContainerProvider.TryGetContainer(out ILifetimeScope)   public static bool method
//       the container exists only while a co-op session runs (server process and clients, from the join
//       screen on); it is not there in single player with Coop ticked. ModInformation.IsServer alone cannot
//       tell a client from single player (it is false in both).
//   GameInterface.Policies.CallOriginalPolicy.IsOriginalAllowed()    public static bool method
//       false on a client in campaign or mission state outside Co-op's own allow scopes: the gate Co-op's
//       own prefixes use to block local writes. Only asked after the container check.
//   GameInterface.Services.MapEvents.BattleSpawnGate.IsCoopBattleActive   public static bool property
//   TAOM.Features.CoopInterop.Missions.CoopMissionOwnership.IsOwnedHere(Agent)   public static bool method
//       true when no co-op session is active.
using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Core
{
    internal static class CoopEnv
    {
        private const string CoopModuleId = "Coop";

        private static bool? _ticked;
        private static readonly Lookup<Func<bool>> IsServerGetter = new Lookup<Func<bool>>("Common", "Common.ModInformation", "IsServer",
            t => PropertyGetter(t, "IsServer"));
        private static readonly Lookup<MethodInfo> TryGetContainer = new Lookup<MethodInfo>("GameInterface", "GameInterface.ContainerProvider", "TryGetContainer",
            t =>
            {
                MethodInfo m = t.GetMethod("TryGetContainer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return m != null && m.ReturnType == typeof(bool) && m.GetParameters().Length == 1 && m.GetParameters()[0].IsOut ? m : null;
            });
        private static readonly Lookup<Func<bool>> OriginalAllowed = new Lookup<Func<bool>>("GameInterface", "GameInterface.Policies.CallOriginalPolicy", "IsOriginalAllowed",
            t =>
            {
                MethodInfo m = t.GetMethod("IsOriginalAllowed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                return m != null && m.ReturnType == typeof(bool) ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), m) : null;
            });
        private static readonly Lookup<Func<bool>> CoopBattleActive = new Lookup<Func<bool>>("GameInterface", "GameInterface.Services.MapEvents.BattleSpawnGate", "IsCoopBattleActive",
            t => PropertyGetter(t, "IsCoopBattleActive"));
        private static readonly Lookup<Func<Agent, bool>> OwnedHere = new Lookup<Func<Agent, bool>>("TAOM", "TAOM.Features.CoopInterop.Missions.CoopMissionOwnership", "IsOwnedHere",
            t =>
            {
                MethodInfo m = t.GetMethod("IsOwnedHere", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Agent) }, null);
                return m != null && m.ReturnType == typeof(bool) ? (Func<Agent, bool>)Delegate.CreateDelegate(typeof(Func<Agent, bool>), m) : null;
            });

        /// <summary>The Coop module is ticked in the launcher (says nothing about a session running).</summary>
        internal static bool IsCoopTicked
        {
            get
            {
                if (_ticked == null)
                    _ticked = ModEnv.IsActive(CoopModuleId);
                return _ticked.Value;
            }
        }

        /// <summary>A co-op session is running in this process (server or client). False in single player.</summary>
        internal static bool IsSessionActive()
        {
            if (!IsCoopTicked)
                return false;
            try
            {
                MethodInfo tryGet = TryGetContainer.Get();
                if (tryGet == null)
                    return false;
                return (bool)tryGet.Invoke(null, new object[1]);
            }
            catch (Exception e)
            {
                LogOnce("session", "Co-op session check failed (treated as: no session): " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Like IsSessionActive, but "cannot tell" (Coop ticked and the session check is missing or fails) counts as
        /// a session. For parts that must never act in co-op. False when Coop is not ticked.
        /// </summary>
        internal static bool IsSessionActiveOrUnknown()
        {
            if (!IsCoopTicked)
                return false;
            try
            {
                MethodInfo tryGet = TryGetContainer.Get();
                if (tryGet == null)
                    return true;
                return (bool)tryGet.Invoke(null, new object[1]);
            }
            catch (Exception e)
            {
                LogOnce("session-unknown", "Co-op session check failed (treated as: session running): " + e.Message);
                return true;
            }
        }

        /// <summary>This process is a client of a running co-op session. False on the host's server process and in single player.</summary>
        internal static bool IsCoopClient()
        {
            if (!IsCoopTicked)
                return false;
            try
            {
                Func<bool> isServer = IsServerGetter.Get();
                if (isServer == null || isServer())
                    return false;
                return IsSessionActive();
            }
            catch (Exception e)
            {
                LogOnce("client", "Co-op client check failed (treated as: not a client): " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// On a co-op client, and Co-op is blocking local writes right now (campaign or mission state, outside
        /// Co-op's own allow scopes). Mods must not build or change campaign objects locally while this is true.
        /// </summary>
        internal static bool IsClientWriteBlocked()
        {
            if (!IsCoopClient())
                return false;
            try
            {
                Func<bool> allowed = OriginalAllowed.Get();
                return allowed != null && !allowed();
            }
            catch (Exception e)
            {
                LogOnce("blocked", "Co-op write-gate check failed (treated as: not blocked): " + e.Message);
                return false;
            }
        }

        /// <summary>A co-op battle is running (host or client). Coop ticked but not readable: true, the careful answer.</summary>
        internal static bool IsCoopBattleActive()
        {
            if (!IsCoopTicked)
                return false;
            try
            {
                Func<bool> active = CoopBattleActive.Get();
                return active == null || active();
            }
            catch (Exception e)
            {
                LogOnce("battle", "Co-op battle check failed (treated as: co-op battle running): " + e.Message);
                return true;
            }
        }

        /// <summary>This machine owns the agent (TAOM's own probe of Co-op's registry). True without a co-op session or when not readable.</summary>
        internal static bool IsOwnedHere(Agent agent)
        {
            if (!IsCoopTicked || agent == null)
                return true;
            try
            {
                Func<Agent, bool> owned = OwnedHere.Get();
                return owned == null || owned(agent);
            }
            catch (Exception e)
            {
                LogOnce("owned", "Agent ownership check failed (treated as: owned here): " + e.Message);
                return true;
            }
        }

        private static Func<bool> PropertyGetter(Type type, string name)
        {
            PropertyInfo p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo getter = p == null || p.PropertyType != typeof(bool) ? null : p.GetGetMethod(true);
            return getter == null ? null : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
        }

        private static readonly System.Collections.Generic.HashSet<string> Logged = new System.Collections.Generic.HashSet<string>();

        private static void LogOnce(string key, string message)
        {
            lock (Logged)
            {
                if (!Logged.Add(key))
                    return;
            }
            AioLog.Write("[Co-op] " + message);
        }

        /// <summary>
        /// One member looked up by name. Tried again while its assembly is not loaded yet; once the assembly is
        /// there the answer (found or not) is kept, and "not found" is logged once.
        /// </summary>
        private sealed class Lookup<T> where T : class
        {
            private readonly string _assembly, _type, _member;
            private readonly Func<Type, T> _find;
            private T _value;
            private bool _done;

            internal Lookup(string assembly, string type, string member, Func<Type, T> find)
            {
                _assembly = assembly;
                _type = type;
                _member = member;
                _find = find;
            }

            internal T Get()
            {
                if (_done)
                    return _value;
                Assembly a = ModEnv.FindAssembly(_assembly);
                if (a == null)
                    return null; // not loaded yet
                T found = null;
                try
                {
                    Type t = a.GetType(_type, false);
                    found = t == null ? null : _find(t);
                }
                catch (Exception e)
                {
                    LogOnce("lookup|" + _type + "." + _member, _type + "." + _member + " lookup failed: " + e.Message);
                }
                if (found == null)
                    LogOnce("missing|" + _type + "." + _member, _type + "." + _member + " not found in " + _assembly + ".dll; the checks that need it use their safe default.");
                _value = found;
                _done = true;
                return _value;
            }
        }
    }
}
