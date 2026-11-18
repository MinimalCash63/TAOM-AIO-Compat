// Xorberax's Legacy switch, v1.4.0 (plan item 3, audit 14 Nov 2026): TAOM's special creatures keep TAOM's rules
// under Xorberax's "Deadly Horse Charge" and "Deadly Combat". No file of either mod is changed.
//
// Why (Verified in the installed Xorberax's Legacy 1.0.8.8, TAOM 2.0.39 and Coop build of 14 Nov 2026):
//  - XorberaxLegacy.Patches.ChargeDamagePatch multiplies the charge's specialMagnitude by its "Charge Damage
//    Multiplier" setting (default 5) for every charging mount, on top of TAOM's culture charge factor.
//  - XorberaxLegacy.Logic.DeadlyCombatMissionLogic kills on one vital hit of 65% of the health limit, or at
//    35% health sets the speed limit to 0 and takes 10 health per second until the agent dies. That pins a
//    troll or a mumak in place.
//  - Co-op: Deadly Combat's OnRegisterBlow only runs where attacker and victim are owned by this machine, but
//    its bleed list keeps an agent whose ownership moved to another machine later; changing the health of, or
//    killing, an agent this machine does not own leaves the machines with different battles.
//
// Which creatures: the monster ids for which TAOM's own ICreatureCombatService says IsUnstoppable(id, 0) or
// CalculateCleaveMomentum(id, 1, true) has a value (TAOM's combat_mechanics_config.json: trolls, mumakil, war
// elephants, spiders, Sauron, with TAOM's settlement variants; TAOM's two creature settings are honoured).
// The id is Agent.Monster.StringId, as TAOM's own hooks pass it. The service is read through TAOM.IoC.Resolve
// by reflection, once per mission; answers are kept per id for that mission. Ordinary troops and cavalry are
// never touched: they keep Xorberax's rules, as the owner set them.
//
// Patches (all on the switch's Harmony id, removed by its Uninstall):
//  3a. MissionCombatMechanicsHelper.ComputeBlowMagnitudeFromHorseCharge(in AttackInformation,
//      in AttackCollisionData, Vec2, Vec2, out float baseMagnitude, out float specialMagnitude) (private static):
//      postfix, last, after "XorberaxLegacy": for an exempt charging creature specialMagnitude = baseMagnitude
//      (the value the game, or RBM's replacement, set before Xorberax multiplied it). Applied at the main menu.
//  3b. DeadlyCombatMissionLogic.OnRegisterBlow(Agent, Agent, WeakGameEntity, Blow, ref AttackCollisionData,
//      in MissionWeapon): prefix that skips Xorberax's method when the victim is exempt or is not owned by this
//      machine. The base method it would have called is empty.
//      v1.5.7: it also skips a victim the game has already put down, or that cannot die now (CanStillDie).
//  3c. DeadlyCombatMissionLogic.OnMissionTick(float) (v1.5.7: always; before only with Coop ticked): prefix that
//      first takes agents that are down, no longer mortal or owned by another machine out of Xorberax's private
//      bleed list (_agentsThatAreBleedingOut); Xorberax itself drops only deleted agents and would Die the rest
//      again.
//  3b and 3c are applied at the first mission start, not earlier: the class's static initializer reads MCM and
//  an action index.
//
// Co-op: host and client run the same code, each for the agents it owns, so the switch must be the same on all
// machines (as before). Single player (also with Coop ticked): every agent is owned here, so only the creature
// exemption and the v1.5.7 dead-agent guards act.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.XorberaxLegacy
{
    internal static class XorberaxCreatureRules
    {
        private const string XorberaxHarmonyId = "XorberaxLegacy";
        private const string XorberaxAssembly = "XorberaxLegacy";
        private const string DeadlyCombatType = "XorberaxLegacy.Logic.DeadlyCombatMissionLogic";
        private const string BleedField = "_agentsThatAreBleedingOut";
        private const string BleedAgentProperty = "BleedingAgent";
        private const string ChargeMethod = "ComputeBlowMagnitudeFromHorseCharge";
        private const string TaomAssembly = "TAOM";
        private const string TaomIoCType = "TAOM.IoC";
        private const string TaomServiceType = "TAOM.Features.CombatMechanics.ICreatureCombatService";

        private static Action<string> _log = _ => { };
        private static readonly HashSet<string> Logged = new HashSet<string>();

        // ---- TAOM's creature list ----

        private static readonly Dictionary<string, bool> ExemptById = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static Func<string, int, bool> _isUnstoppable;
        private static Func<string, float, bool, float?> _cleaveMomentum;
        private static bool _serviceTried;

        internal static void Init(Action<string> log)
        {
            _log = log ?? (_ => { });
        }

        /// <summary>Mission start: TAOM's answers are asked again (its settings can change between missions).</summary>
        internal static void OnMissionStart()
        {
            lock (ExemptById)
            {
                ExemptById.Clear();
                _serviceTried = false;
                _isUnstoppable = null;
                _cleaveMomentum = null;
            }
        }

        /// <summary>Uninstall: forget everything.</summary>
        internal static void Reset()
        {
            OnMissionStart();
            _bleedField = null;
            _bleedAgent = null;
            _bleedAgentProperty = null;
        }

        /// <summary>For the offline check only: the two TAOM answers are supplied directly.</summary>
        internal static void UseServiceForTest(Func<string, int, bool> isUnstoppable, Func<string, float, bool, float?> cleaveMomentum)
        {
            lock (ExemptById)
            {
                ExemptById.Clear();
                _isUnstoppable = isUnstoppable;
                _cleaveMomentum = cleaveMomentum;
                _serviceTried = true;
            }
        }

        /// <summary>True for a monster id that TAOM treats as one of its special creatures. Never throws.</summary>
        internal static bool IsExempt(Monster monster)
        {
            string id;
            try { id = monster?.StringId; }
            catch { return false; }
            return IsExemptId(id);
        }

        internal static bool IsExemptId(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
                return false;
            try
            {
                lock (ExemptById)
                {
                    if (ExemptById.TryGetValue(monsterId, out bool known))
                        return known;
                    if (!_serviceTried)
                    {
                        _serviceTried = true;
                        ResolveService();
                    }
                    bool exempt = false;
                    if (_isUnstoppable != null && _cleaveMomentum != null)
                        exempt = _isUnstoppable(monsterId, 0) || _cleaveMomentum(monsterId, 1f, true).HasValue;
                    ExemptById[monsterId] = exempt;
                    return exempt;
                }
            }
            catch (Exception e)
            {
                LogOnce("exempt", "TAOM's creature list could not be asked (no creature is exempt until the next mission; logged once): " + e.Message);
                try { lock (ExemptById) ExemptById[monsterId] = false; } catch { } // code review: do not ask (and throw) again on every hit this mission
                return false;
            }
        }

        // TAOM.IoC.Resolve<ICreatureCombatService>() (public static, generic, no parameters), then the two
        // interface methods bound to that instance. Any miss: no creature is exempt, one log line.
        private static void ResolveService()
        {
            try
            {
                Type ioc = ModEnv.FindType(TaomAssembly, TaomIoCType);
                Type service = ModEnv.FindType(TaomAssembly, TaomServiceType);
                MethodInfo resolve = ioc?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 0);
                MethodInfo unstoppable = service?.GetMethod("IsUnstoppable", new[] { typeof(string), typeof(int) });
                MethodInfo cleave = service?.GetMethod("CalculateCleaveMomentum", new[] { typeof(string), typeof(float), typeof(bool) });
                if (resolve == null || unstoppable == null || unstoppable.ReturnType != typeof(bool) || cleave == null || cleave.ReturnType != typeof(float?))
                {
                    LogOnce("service-missing", TaomIoCType + ".Resolve<T>() or " + TaomServiceType + " (IsUnstoppable, CalculateCleaveMomentum) not found in TAOM.dll: no creature is exempt from Xorberax's charge and Deadly Combat rules.");
                    return;
                }
                object instance = resolve.MakeGenericMethod(service).Invoke(null, null);
                if (instance == null)
                {
                    LogOnce("service-null", "TAOM's creature combat service is not available: no creature is exempt this mission.");
                    return;
                }
                _isUnstoppable = (Func<string, int, bool>)Delegate.CreateDelegate(typeof(Func<string, int, bool>), instance, unstoppable);
                _cleaveMomentum = (Func<string, float, bool, float?>)Delegate.CreateDelegate(typeof(Func<string, float, bool, float?>), instance, cleave);
            }
            catch (Exception e)
            {
                _isUnstoppable = null;
                _cleaveMomentum = null;
                Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                LogOnce("service-error", "TAOM's creature combat service could not be read (no creature is exempt this mission; logged once): " + inner.Message);
            }
        }

        // ---- 3a. charge ----

        /// <summary>Main menu. Returns a text for the log; never throws.</summary>
        internal static string InstallCharge(Harmony harmony)
        {
            try
            {
                MethodInfo target = AccessTools.DeclaredMethod(typeof(MissionCombatMechanicsHelper), ChargeMethod);
                ParameterInfo[] p = target?.GetParameters();
                if (target == null || !target.IsStatic || p.Length != 6
                    || p[0].ParameterType != typeof(AttackInformation).MakeByRefType() || p[0].Name != "attackInformation"
                    || p[4].ParameterType != typeof(float).MakeByRefType() || p[4].Name != "baseMagnitude"
                    || p[5].ParameterType != typeof(float).MakeByRefType() || p[5].Name != "specialMagnitude")
                    return "MissionCombatMechanicsHelper." + ChargeMethod + " (6 parameters, attackInformation, baseMagnitude, specialMagnitude) not found: charge part left off.";
                bool xorberax = false;
                Patches info = Harmony.GetPatchInfo(target);
                if (info != null)
                    xorberax = info.Postfixes.Any(x => x.owner == XorberaxHarmonyId);
                if (!xorberax)
                    return "Xorberax's charge postfix is not on MissionCombatMechanicsHelper." + ChargeMethod + ": charge part left off (nothing to undo).";
                HarmonyMethod post = Hook(nameof(ChargePostfix), Priority.Last);
                post.after = new[] { XorberaxHarmonyId };
                harmony.Patch(target, postfix: post);
                return "Patched: postfix (after " + XorberaxHarmonyId + ") on MissionCombatMechanicsHelper." + ChargeMethod
                    + ". TAOM's creatures charge with the game's value, without Xorberax's charge multiplier.";
            }
            catch (Exception e)
            {
                return "Charge part not installed (ignored; the rest of this switch is unchanged): " + e.Message;
            }
        }

        // The charging agent is the mount itself (IsAttackerAgentMount); AttackerAgentMonster is its monster.
        private static void ChargePostfix(in AttackInformation attackInformation, float baseMagnitude, ref float specialMagnitude)
        {
            try
            {
                if (specialMagnitude == baseMagnitude)
                    return;
                Monster monster = attackInformation.AttackerAgentMonster ?? attackInformation.AttackerAgent?.Monster;
                if (!IsExempt(monster))
                    return;
                LogOnce("charge-first", "First creature charge kept at the game's value this session: " + monster.StringId + ", "
                    + specialMagnitude.ToString("0.##") + " (Xorberax) -> " + baseMagnitude.ToString("0.##") + ".");
                specialMagnitude = baseMagnitude;
            }
            catch (Exception e) { LogOnce("charge-error", "Error in the charge postfix (ignored, logged once): " + e.Message); }
        }

        // ---- 3b and 3c. Deadly Combat ----

        private static FieldInfo _bleedField;          // DeadlyCombatMissionLogic._agentsThatAreBleedingOut (Dictionary<int, AgentInfo>)
        private static PropertyInfo _bleedAgentProperty; // AgentInfo.BleedingAgent (private nested class)
        private static Func<object, Agent> _bleedAgent;

        /// <summary>First mission start. Returns a text for the log; never throws.</summary>
        internal static string InstallDeadlyCombat(Harmony harmony)
        {
            Type logic;
            try
            {
                logic = ModEnv.FindType(XorberaxAssembly, DeadlyCombatType);
                if (logic == null)
                    return DeadlyCombatType + " not found: Deadly Combat part left off.";
            }
            catch (Exception e) { return "Deadly Combat part not installed (ignored): " + e.Message; }

            string blowText;
            try
            {
                MethodInfo blow = AccessTools.DeclaredMethod(logic, "OnRegisterBlow");
                ParameterInfo[] p = blow?.GetParameters();
                if (blow == null || blow.IsStatic || p.Length != 6 || p[1].ParameterType != typeof(Agent) || p[1].Name != "victim")
                    blowText = DeadlyCombatType + ".OnRegisterBlow (6 parameters, Agent victim) not found: left off.";
                else
                {
                    harmony.Patch(blow, prefix: Hook(nameof(DeadlyBlowPrefix), Priority.First));
                    blowText = "prefix on DeadlyCombatMissionLogic.OnRegisterBlow (agents already down or unable to die, TAOM's creatures, and agents this machine does not own, are left out of Deadly Combat).";
                }
            }
            catch (Exception e) { blowText = "OnRegisterBlow prefix not installed (ignored): " + e.Message; }

            string tickText;
            try
            {
                // v1.5.7: always (was: only with Coop ticked); dead or routed agents leave the list everywhere.
                {
                    MethodInfo tick = AccessTools.DeclaredMethod(logic, "OnMissionTick", new[] { typeof(float) });
                    FieldInfo field = AccessTools.DeclaredField(logic, BleedField);
                    Type infoType = field != null && field.FieldType.IsGenericType && field.FieldType.GetGenericArguments().Length == 2
                        ? field.FieldType.GetGenericArguments()[1] : null;
                    PropertyInfo agent = infoType?.GetProperty(BleedAgentProperty, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (tick == null || field == null || field.IsStatic || !typeof(IDictionary).IsAssignableFrom(field.FieldType)
                        || agent == null || !typeof(Agent).IsAssignableFrom(agent.PropertyType) || agent.GetGetMethod(true) == null)
                        tickText = "DeadlyCombatMissionLogic.OnMissionTick(float) or its bleed list (" + BleedField + ", " + BleedAgentProperty + ") not found: bleed-list clean-up left off.";
                    else
                    {
                        _bleedField = field;
                        _bleedAgentProperty = agent;
                        _bleedAgent = BuildAgentGetter(infoType, agent);
                        harmony.Patch(tick, prefix: Hook(nameof(DeadlyTickPrefix), Priority.First));
                        tickText = "prefix on DeadlyCombatMissionLogic.OnMissionTick (agents that are down, no longer mortal or, in co-op, owned elsewhere leave the bleed list).";
                    }
                }
            }
            catch (Exception e) { tickText = "OnMissionTick prefix not installed (ignored): " + e.Message; }

            return "Deadly Combat (first mission): " + blowText + " " + tickText;
        }

        // Compiled getter for the private nested AgentInfo.BleedingAgent; plain reflection if that fails.
        private static Func<object, Agent> BuildAgentGetter(Type infoType, PropertyInfo property)
        {
            try
            {
                ParameterExpression o = Expression.Parameter(typeof(object), "o");
                return Expression.Lambda<Func<object, Agent>>(
                    Expression.Convert(Expression.Property(Expression.Convert(o, infoType), property), typeof(Agent)), o).Compile();
            }
            catch
            {
                return null;
            }
        }

        private static Agent BleedingAgentOf(object info)
        {
            if (info == null)
                return null;
            Func<object, Agent> getter = _bleedAgent;
            if (getter != null)
            {
                try { return getter(info); }
                catch { _bleedAgent = null; } // fall back to reflection from now on
            }
            return _bleedAgentProperty?.GetValue(info, null) as Agent;
        }

        // False = Xorberax's OnRegisterBlow does not run for this blow (no instant kill, no bleed entry).
        private static bool DeadlyBlowPrefix(Agent victim)
        {
            try
            {
                if (victim == null)
                    return true;
                if (!CanStillDie(victim))
                {
                    LogOnce("deadly-down", "A blow on an agent that is already down, or cannot die here, was left out of Xorberax's Deadly Combat (first time this session); the game has already handled it.");
                    return false;
                }
                if (IsExempt(victim.Monster))
                {
                    LogOnce("deadly-exempt", "First TAOM creature left out of Xorberax's Deadly Combat this session: " + victim.Monster.StringId + ".");
                    return false;
                }
                if (!CoopEnv.IsOwnedHere(victim))
                {
                    LogOnce("deadly-notowned", "Co-op: a blow on an agent this machine does not own was left out of Xorberax's Deadly Combat (first time this session); the owning machine decides.");
                    return false;
                }
            }
            catch (Exception e) { LogOnce("deadly-error", "Error in the Deadly Combat blow prefix (ignored, logged once): " + e.Message); }
            return true;
        }

        /// <summary>
        /// v1.5.7: false when the game itself would not let this agent die from a blow now. Game 1.5.7 (Verified):
        /// Mission.RegisterBlow applies the blow (Agent.HandleBlow, which calls Die below 1 health) before any
        /// OnRegisterBlow, and HandleBlow does no damage to non-mortal agents, with Mission.DisableDying, or in a
        /// conversation or cut scene. Xorberax's rule would Die such an agent anyway, or a second time.
        /// </summary>
        internal static bool CanStillDie(Agent agent)
        {
            if (agent == null || !agent.IsActive() || agent.Health < 1f || agent.CurrentMortalityState != Agent.MortalityState.Mortal)
                return false;
            Mission mission = Mission.Current;
            return mission == null || (!mission.DisableDying && mission.Mode != MissionMode.Conversation && mission.Mode != MissionMode.CutScene);
        }

        // Always lets Xorberax's tick run; first empties its list of agents that are no longer up and mortal
        // (v1.5.7: Xorberax only drops deleted agents, so it would Die a dead or routed one again) and, in co-op,
        // of agents owned elsewhere.
        private static void DeadlyTickPrefix(object __instance)
        {
            try
            {
                int removed = RemoveNotOwned(__instance, a => a.IsActive() && a.CurrentMortalityState == Agent.MortalityState.Mortal && CoopEnv.IsOwnedHere(a), out int down);
                if (down > 0)
                    LogOnce("bleed-down", down + " bleeding agent(s) that were already down or no longer mortal were taken off Xorberax's bleed list (first time this session), so they are not killed again.");
                if (removed - down > 0)
                    LogOnce("bleed-notowned", "Co-op: " + (removed - down) + " bleeding agent(s) this machine no longer owns were taken off Xorberax's bleed list (first time this session); the owning machine decides.");
            }
            catch (Exception e) { LogOnce("bleed-error", "Error in the bleed-list clean-up (ignored, logged once): " + e.Message); }
        }

        /// <summary>
        /// Removes the bleed-list entries whose agent fails the test. Keys are collected first, then removed
        /// (the list is a Dictionary&lt;int, private class&gt;, read through the non-generic IDictionary).
        /// </summary>
        internal static int RemoveNotOwned(object logic, Func<Agent, bool> ownedHere) => RemoveNotOwned(logic, ownedHere, out _);

        /// <summary>As above; also removes entries without an agent. down = how many of the removed were not active or not mortal.</summary>
        internal static int RemoveNotOwned(object logic, Func<Agent, bool> keep, out int down)
        {
            down = 0;
            if (logic == null || _bleedField == null || keep == null)
                return 0;
            if (!(_bleedField.GetValue(logic) is IDictionary list) || list.Count == 0)
                return 0;
            List<object> gone = null;
            List<Agent> goneAgents = null;
            foreach (DictionaryEntry entry in list)
            {
                Agent agent = BleedingAgentOf(entry.Value);
                bool remove;
                try { remove = agent == null || !keep(agent); }
                catch { remove = true; }
                if (!remove)
                    continue;
                (gone ?? (gone = new List<object>())).Add(entry.Key);
                if (agent == null)
                {
                    down++;
                    continue;
                }
                bool isDown;
                try { isDown = !agent.IsActive() || agent.CurrentMortalityState != Agent.MortalityState.Mortal; }
                catch { isDown = true; }
                if (isDown)
                    down++;
                bool active;
                try { active = agent.IsActive(); }
                catch { active = false; }
                if (active) // still up (owned elsewhere, or made immortal): its speed limit is lifted below
                    (goneAgents ?? (goneAgents = new List<Agent>())).Add(agent);
            }
            if (gone == null)
                return 0;
            foreach (object key in gone)
                list.Remove(key);
            // Code review 14 Nov 2026: Xorberax sets a speed limit of 0 on a bleeding agent once per second and
            // never clears it. Taken off the list, the local copy would stay pinned, so the limit is lifted
            // (-1 = no limit, the game's own reset). The owning machine never had the limit.
            foreach (Agent agent in goneAgents ?? new List<Agent>())
            {
                try
                {
                    if (agent.IsActive())
                        agent.SetMaximumSpeedLimit(-1f, false);
                }
                catch { }
            }
            return gone.Count;
        }

        /// <summary>For the offline check only: binds the bleed list members without patching.</summary>
        internal static bool BindBleedListForTest(Type logic)
        {
            FieldInfo field = AccessTools.DeclaredField(logic, BleedField);
            Type infoType = field?.FieldType.GetGenericArguments()[1];
            PropertyInfo agent = infoType?.GetProperty(BleedAgentProperty, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || agent == null)
                return false;
            _bleedField = field;
            _bleedAgentProperty = agent;
            _bleedAgent = BuildAgentGetter(infoType, agent);
            return true;
        }

        // ---- shared ----

        private static HarmonyMethod Hook(string name, int priority) =>
            new HarmonyMethod(typeof(XorberaxCreatureRules).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)) { priority = priority };

        private static void LogOnce(string key, string message)
        {
            lock (Logged)
            {
                if (!Logged.Add(key))
                    return;
            }
            try { _log(message); } catch { }
        }
    }
}
