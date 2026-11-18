// v1.5.7: TAOM's races under Dismemberment Plus (owner decision 18 Nov 2026; plan
// docs\2026-11-18-plan-v1.5.7-dp-race-heads-xl-guards.md).
//   - DP's own path (all cuts): race "human" (TAOM men), female elves, female Nazgul. Their skins use the vanilla
//     human body meshes that DP's pre-cut parts are made from (research 2026-11-18-dp-taom-races.md, Verified).
//   - Head cuts only, built here from the victim's own meshes and skeleton: male elves, male Nazgul, orcs, uruks,
//     Uruk-hai, berserkers, pale uruks, Dol Guldur uruks, goblins, Saruman, dwarves. DP's Dismember is skipped
//     for them: it would put a vanilla human head and body on them. Limbs stay whole (cut limbs need new art).
//   - Whole: cave trolls, hill trolls, Sauron, and any race not named here.
//
// The head cut works like DP's own head cut, with these differences: the cut part uses the victim's own
// skeleton (dwarves have their own; its neck and head bones must be numbers 12 and 13, as DP assumes), it holds
// copies of the victim's own head, hair, beard, eyebrow and helmet metameshes (DP uses its vanilla pre-cut
// head), and the body keeps every other mesh the game built for it; only those head metameshes are taken off
// (DP clears the body and rebuilds it from vanilla parts). Each agent's skin and armour meshes are its own
// copies (game 1.5.7: AgentVisuals.AddArmorMultiMeshesToAgentEntity uses GetMultiMeshCopy), so this changes
// that one body only. Visual only, per machine; nothing is sent, nothing in the battle changes.
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Fixes.DismembermentPlus
{
    internal enum DpRaceMode { Whole, DpPath, HeadOnly }

    internal static class DpRaceHeads
    {
        private static Action<string> _log = _ => { };
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static readonly Dictionary<string, bool> SkeletonOk = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static string[] _raceNames;
        private static bool _raceNamesTried;

        // Head, hair, beard and eyebrow meshes of every TAOM 2.0.40 race contain one of these words; the one
        // exception is the hair "short_a_test" (Nazgul, Saruman). No body, hands, feet or underwear mesh and no
        // body, cape, glove or leg armour of TAOM, LOTRLOME_Armory, Native or SandBoxCore does (checked 18 Nov 2026).
        private static readonly string[] HeadWords = { "head", "hair", "beard", "eyebrow" };
        private const string ExtraHair = "short_a_test";

        internal static void Init(Action<string> log) => _log = log ?? (_ => { });

        internal static void Reset()
        {
            _raceNames = null;
            _raceNamesTried = false;
            lock (SkeletonOk) SkeletonOk.Clear();
        }

        /// <summary>The rule per TAOM race id and sex. Internal for the offline check.</summary>
        internal static DpRaceMode ModeFor(string raceName, bool female)
        {
            switch (raceName)
            {
                case "human":
                    return DpRaceMode.DpPath;
                case "elf":
                case "nazghul":
                    return female ? DpRaceMode.DpPath : DpRaceMode.HeadOnly;
                case "orc":
                case "uruk":
                case "uruk_hai":
                case "berserker":
                case "pale_uruk":
                case "dg_uruk":
                case "goblin":
                case "saruman":
                case "dwarf":
                    return DpRaceMode.HeadOnly;
                default:
                    return DpRaceMode.Whole; // cave_troll, hill_troll, sauron, and anything new
            }
        }

        /// <summary>Mode of a humanoid victim. Never throws; anything unclear is Whole.</summary>
        internal static DpRaceMode ModeOf(Agent victim)
        {
            try
            {
                BasicCharacterObject character = victim?.Character;
                if (character == null)
                    return DpRaceMode.Whole;
                string name = RaceName(character.Race);
                return name == null ? DpRaceMode.Whole : ModeFor(name, victim.IsFemale);
            }
            catch (Exception e)
            {
                LogOnce("mode-error", "Race check failed (logged once); that victim stays whole: " + e.Message);
                return DpRaceMode.Whole;
            }
        }

        internal static string RaceName(int race)
        {
            if (!_raceNamesTried)
            {
                _raceNamesTried = true;
                try { _raceNames = TaleWorlds.Core.FaceGen.GetRaceNames(); } // MountAndBlade has a FaceGen too
                catch (Exception e) { LogOnce("races-error", "Race names could not be read; every race, men included, stays whole this session (logged once): " + e.Message); }
                if (_raceNames == null || _raceNames.Length == 0)
                    LogOnce("races-none", "No race names from the game; every race stays whole this session.");
                else
                    LogOnce("races", "Races: " + string.Join(", ", _raceNames.Select((n, i) => i + "=" + n + " " + Describe(n))) + ".");
            }
            string[] names = _raceNames;
            return names != null && race >= 0 && race < names.Length ? names[race] : null;
        }

        private static string Describe(string raceName)
        {
            DpRaceMode m = ModeFor(raceName, false), f = ModeFor(raceName, true);
            return m == f ? "(" + m + ")" : "(male " + m + ", female " + f + ")";
        }

        internal static bool IsHeadBone(sbyte bone) => bone == (sbyte)HumanBone.Neck || bone == (sbyte)HumanBone.Head;

        /// <summary>
        /// True when the victim's skeleton has "neck" and "head" at DP's bone numbers 12 and 13 (checked once per
        /// skeleton). False: that race stays whole.
        /// </summary>
        internal static bool HasHumanHeadBones(Agent victim)
        {
            string name = null;
            try
            {
                name = victim?.AgentVisuals?.GetSkeleton()?.GetName();
                if (string.IsNullOrEmpty(name))
                    return false;
                lock (SkeletonOk)
                {
                    if (SkeletonOk.TryGetValue(name, out bool known))
                        return known;
                }
                bool ok = Skeleton.SkeletonModelExist(name)
                    && Skeleton.GetBoneIndexFromName(name, "neck") == (sbyte)HumanBone.Neck
                    && Skeleton.GetBoneIndexFromName(name, "head") == (sbyte)HumanBone.Head;
                lock (SkeletonOk) SkeletonOk[name] = ok;
                LogOnce("skel-" + name, "Skeleton " + name + ": " + (ok ? "neck and head at bones 12 and 13, head cuts allowed." : "neck and head not at bones 12 and 13 (or no such model); its races stay whole."));
                return ok;
            }
            catch (Exception e)
            {
                LogOnce("skel-error-" + name, "Skeleton check failed for " + name + " (logged once); its races stay whole: " + e.Message);
                if (name != null)
                    lock (SkeletonOk) SkeletonOk[name] = false;
                return false;
            }
        }

        /// <summary>True for a mesh or metamesh name (lower case) that belongs to the head. Internal for the offline check.</summary>
        internal static bool IsHeadMeshName(string lowerName, IList<string> helmetNames)
        {
            if (string.IsNullOrEmpty(lowerName))
                return false;
            foreach (string w in HeadWords)
                if (lowerName.Contains(w))
                    return true;
            if (lowerName.StartsWith(ExtraHair, StringComparison.Ordinal))
                return true;
            if (helmetNames != null)
                foreach (string h in helmetNames)
                    if (lowerName.Contains(h))
                        return true;
            return false;
        }

        internal static string BaseName(string meshName)
        {
            int dot = meshName.IndexOf('.');
            return dot > 0 ? meshName.Substring(0, dot) : meshName;
        }

        /// <summary>Cuts the victim's head off with its own meshes. Never throws; on any problem before the cut the victim stays whole.</summary>
        internal static void HeadCut(Agent victim, AttackCollisionData collisionData)
        {
            string race = "?";
            bool cut = false;
            try
            {
                race = RaceName(victim.Character.Race) ?? "?";
                MBAgentVisuals visuals = victim.AgentVisuals;
                Skeleton skeleton = visuals?.GetSkeleton();
                string skeletonName = skeleton?.GetName();
                Scene scene = Mission.Current?.Scene;
                if (string.IsNullOrEmpty(skeletonName) || scene == null || !HasHumanHeadBones(victim))
                    return;

                var helmetNames = new List<string>();
                EquipmentElement helmet = victim.SpawnEquipment[EquipmentIndex.Head];
                AddName(helmetNames, helmet.Item?.MultiMeshName);
                AddName(helmetNames, helmet.CosmeticItem?.MultiMeshName);

                // 1. The head's metamesh components (face with its eyes and teeth, hair, beard, eyebrows, helmet).
                var headParts = new List<MetaMesh>();
                var left = new List<string>();
                int count = skeleton.GetComponentCount(GameEntity.ComponentType.MetaMesh);
                for (int i = 0; i < count; i++)
                {
                    MetaMesh mm = skeleton.GetComponentAtIndex(GameEntity.ComponentType.MetaMesh, i) as MetaMesh;
                    if (mm == null || !mm.IsValid)
                        continue;
                    string lower = (mm.GetName() ?? "").ToLowerInvariant();
                    if (IsHeadMeshName(lower, helmetNames))
                        headParts.Add(mm);
                    else
                        left.Add(lower);
                }

                // 2. Copies first, so nothing is made when nothing can be copied.
                var copies = new List<MetaMesh>();
                var copied = new List<string>();
                List<Mesh> hideMeshes = null;
                string path;
                if (headParts.Count > 0)
                {
                    path = "metamesh components";
                    foreach (MetaMesh mm in headParts)
                    {
                        string name = mm.GetName();
                        MetaMesh copy = string.IsNullOrEmpty(name) ? null : MetaMesh.GetCopy(name, false, true);
                        if (copy == null || !copy.IsValid)
                            continue;
                        int n = copy.MeshCount, m = mm.MeshCount;
                        for (int i = 0; i < n; i++)
                        {
                            Mesh sub = copy.GetMeshAtIndex(i);
                            if (sub == null)
                                continue;
                            Mesh source = i < m ? mm.GetMeshAtIndex(i) : null;
                            if (source != null)
                            {
                                sub.Color = source.Color;
                                sub.Color2 = source.Color2;
                            }
                            sub.SetVisibilityMask((VisibilityMaskFlags)33); // DP's value
                        }
                        copies.Add(copy);
                        copied.Add(name);
                    }
                }
                else
                {
                    path = "";
                }
                if (copies.Count == 0)
                {
                    // Fallback: DP's way, by mesh name; the originals are hidden instead of removed.
                    path = headParts.Count > 0 ? "mesh names (head metamesh components could not be copied)" : "mesh names (no head metamesh component found)";
                    headParts.Clear();
                    copied.Clear();
                    hideMeshes = new List<Mesh>();
                    left.Clear();
                    var bases = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Mesh mesh in skeleton.GetAllMeshes())
                    {
                        if (mesh == null)
                            continue;
                        string name = mesh.Name ?? "";
                        if (!IsHeadMeshName(name.ToLowerInvariant(), helmetNames))
                        {
                            left.Add(name.ToLowerInvariant());
                            continue;
                        }
                        hideMeshes.Add(mesh);
                        if (name.Contains(".lod"))
                            continue;
                        string baseName = BaseName(name);
                        if (!bases.Add(baseName))
                            continue;
                        MetaMesh copy = MetaMesh.GetCopy(baseName, false, true);
                        if (copy == null || !copy.IsValid)
                            continue;
                        for (int i = 0; i < copy.MeshCount; i++)
                        {
                            Mesh sub = copy.GetMeshAtIndex(i);
                            if (sub == null)
                                continue;
                            sub.Color = mesh.Color;
                            sub.Color2 = mesh.Color2;
                            sub.SetVisibilityMask((VisibilityMaskFlags)33);
                        }
                        copies.Add(copy);
                        copied.Add(baseName);
                    }
                }
                if (copies.Count == 0)
                {
                    LogOnce("cut-nocopy-" + race, "Head cut skipped for race " + race + " (logged once per race): no head mesh could be copied (" + path + "). Meshes on the body: "
                        + string.Join(", ", left.Where(x => !x.Contains(".lod")).Distinct()) + ".");
                    return;
                }

                // 3. The cut part, as DP's GetDismemberedEntity, with the victim's own skeleton.
                Skeleton partSkeleton = Skeleton.CreateFromModel(skeletonName);
                if (partSkeleton == null || !partSkeleton.IsValid)
                {
                    LogOnce("cut-noskel-" + race, "Head cut skipped for race " + race + ": skeleton " + skeletonName + " could not be made (logged once per race).");
                    return;
                }
                GameEntity part = GameEntity.CreateEmptyDynamic(scene, true);
                part.Name = part.GetGuid();
                part.Skeleton = partSkeleton;
                MBSkeletonExtensions.SetAnimationAtChannel(part.Skeleton, skeleton.GetAnimationAtChannel(0), 0, 1f, -1f, 0f);
                Vec3 position = victim.Position;
                part.SetLocalPosition(position);
                MatrixFrame globalFrame = visuals.GetGlobalFrame();
                var origin = new Vec3(position.x + 0.015f, position.y + 0.015f, position.z + 0.1f, -1f);
                var frame = new MatrixFrame(globalFrame.rotation, origin);
                part.SetGlobalFrame(in frame, true);
                part.BodyFlag = BodyFlags.None;
                part.EntityFlags = (EntityFlags)308674560; // DP's value
                part.SetPhysicsState(true, false);
                part.EnableDynamicBody();
                foreach (MetaMesh copy in copies)
                    part.Skeleton.AddComponent(copy);
                cut = true;

                // 4. The head off the body.
                if (hideMeshes == null)
                {
                    foreach (MetaMesh mm in headParts)
                        skeleton.RemoveComponent(mm);
                }
                else
                {
                    foreach (Mesh mesh in hideMeshes)
                        mesh.SetVisibilityMask((VisibilityMaskFlags)0);
                }

                visuals.SetVoiceDefinitionIndex(-1, 0f);
                victim.CreateBloodBurstAtLimb(collisionData.AttackBoneIndex, 0.5f + MBRandom.RandomFloat * 0.5f);
                if (!victim.HasMount)
                    part.ActivateRagdoll();

                LogOnce("cut-first-" + race, "First head cut for race " + race + " (skeleton " + skeletonName + ", by " + path + "): moved "
                    + string.Join(", ", copied) + "; left on the body " + string.Join(", ", left.Where(x => !x.Contains(".lod")).Distinct()) + ".");
            }
            catch (Exception e)
            {
                LogOnce("cut-error-" + race, "Head cut failed for race " + race + " (logged once per race; "
                    + (cut ? "the cut part was already made" : "the body was left whole") + "): " + e);
            }
        }

        private static void AddName(List<string> names, string meshName)
        {
            if (!string.IsNullOrEmpty(meshName))
                names.Add(meshName.ToLowerInvariant());
        }

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
