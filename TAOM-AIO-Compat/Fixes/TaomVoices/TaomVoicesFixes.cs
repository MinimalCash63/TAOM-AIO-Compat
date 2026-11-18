// TAOM_Voices switch.
// 1. Keeps a failure in TAOM_Voices' campaign start from looping new-game loading.
//    TAOM.Voices.SubModule.OnGameStart builds its voice catalog and writes 5,325 clips to
//    %TEMP%\TAOM-VoiceFiles with no try/catch, at every campaign start. Any exception escaping there during
//    new-game loading makes the loading step repeat forever. The finalizer catches it, logs it once, and lets
//    loading continue; voices then stay off for that game.
// 2. (v1.5.1) Lets TAOM_Voices run on Bannerlord 1.5.7 in single player. Its build check
//    (TAOM.PlayerVoices.SubModule.CheckIdentity) compares the SHA-256 of three game files with the values of
//    the build it was made for (Bannerlord 1.4.8) and throws, so the whole mod turned itself off on 1.5.7.
//    A prefix accepts exactly the 1.5.7.125802 files checked by the AIO (plan, review and evidence:
//    docs\2026-11-18-plan-v1.5.1-taom-voices-on-1.5.7.md); any other file still fails as before.
//    Never in co-op: when Coop is ticked and a session is running (or that cannot be read), the check fails as
//    before, and a prefix on OnGameStart clears TAOM_Voices' Ready flag so a game that turned voices on earlier
//    in the same process is switched off again by TAOM_Voices' own clean-up. TAOM_Voices' files are not changed.
// 3. (v1.5.5) TAOM_Voices' sound tick (TAOM.PlayerVoices.PlayerAudio.Tick, every frame once voices are on) starts
//    with Hero.MainHero outside its try/catch. On 1.5.7 Hero.MainHero throws instead of returning null while the
//    game has no campaign player troop (new-game loading, main menu after a game, custom battle):
//    CharacterObject.PlayerCharacter is Game.Current.PlayerTroop as CharacterObject, and get_MainHero calls
//    HeroObject on it. Owner's test 18 Nov 2026 10:54: TAOM caught NullReferenceException at PlayerAudio.cs:73
//    during new-game loading. A prefix skips the tick (and stops a clip that is still playing) while there is no
//    such player troop. Part 2 is installed only together with this guard.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using TAOM_AIO_Compat.Core;
using TaleWorlds.Core;

namespace TAOM_AIO_Compat.Fixes.TaomVoices
{
    internal sealed class TaomVoicesFixes : FixGroup
    {
        internal override string SettingKey => "TaomVoicesFixes";
        internal override string Name => "TAOM Voices";
        internal override string TargetModuleId => "TAOM_Voices";

        internal override string OffWarning =>
            "If TAOM Voices then fails while a campaign starts, loading repeats forever. To be safe, untick TAOM_Voices in the launcher as well.";

        private const string HarmonyId = "taom.compat.aio.taomvoices";
        private const string SubModuleTypeName = "TAOM.Voices.SubModule";
        private const string PlayerSubModuleTypeName = "TAOM.PlayerVoices.SubModule";
        private const string PlayerAudioTypeName = "TAOM.PlayerVoices.PlayerAudio";

        // Bannerlord 1.5.7.125802 (Steam), SHA-256 of the files TAOM_Voices checks, lower case as its own Catalog.Hash.
        // TAOM_Voices' 9 Harmony targets were checked on these files (plan review and offline harness, 18 Nov 2026).
        private static readonly Dictionary<string, string> Validated = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "TaleWorlds.MountAndBlade", "eaec5c773ac9e936670790b16333bc765f949629b6d10ecd5cf5ea2346bb2406" },
            { "TaleWorlds.CampaignSystem", "d7e5c67a04ef83964c882fda2a1f20f0e2afaf2bfe31345ed31d1061c495d462" },
            { "SandBox", "194b44e6b427bf49d50fa4235434a221c8d7d0581300cf82d3166c791f582b9c" },
        };

        private static readonly Dictionary<string, string> HashCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Accepted = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> LoggedKeys = new HashSet<string>(StringComparer.Ordinal);

        private static Action<string> _log;
        private static bool _logged;
        private static FieldInfo _readyField;
        private static FieldInfo _soundField;
        private static MethodInfo _stopMethod;
        private Harmony _harmony;

        internal override bool Install()
        {
            _log = Log;
            Type t = ModEnv.FindType("TAOM_Voices", SubModuleTypeName);
            MethodInfo start = t == null ? null : AccessTools.DeclaredMethod(t, "OnGameStart", new[] { typeof(Game), typeof(IGameStarter) });
            if (start == null)
            {
                Log(SubModuleTypeName + ".OnGameStart(Game, IGameStarter) not found. Nothing changed.");
                return false;
            }
            var h = new Harmony(HarmonyId);
            h.Patch(start, finalizer: new HarmonyMethod(typeof(TaomVoicesFixes).GetMethod(nameof(OnGameStartFinalizer), BindingFlags.Static | BindingFlags.NonPublic)));
            _harmony = h;
            Log("Guard on " + SubModuleTypeName + ".OnGameStart installed.");

            // Part 2: build check on 1.5.7. Both members must be there, or nothing of part 2 is installed.
            Type p = ModEnv.FindType("TAOM_Voices", PlayerSubModuleTypeName);
            MethodInfo check = p == null ? null : AccessTools.DeclaredMethod(p, "CheckIdentity", new[] { typeof(Assembly), typeof(string) });
            FieldInfo ready = p == null ? null : AccessTools.DeclaredField(p, "Ready");
            if (check == null || !check.IsStatic || check.ReturnType != typeof(void) || ready == null || !ready.IsStatic || ready.FieldType != typeof(bool))
            {
                Log(PlayerSubModuleTypeName + ".CheckIdentity(Assembly, String) or its Ready flag not found: voices stay off on 1.5.7 (TAOM_Voices' own build check).");
                return true;
            }
            // Part 3: the sound tick guard. Without it voices must stay off on 1.5.7 (part 2 is not installed).
            Type audio = ModEnv.FindType("TAOM_Voices", PlayerAudioTypeName);
            MethodInfo tick = audio == null ? null : AccessTools.DeclaredMethod(audio, "Tick", Type.EmptyTypes);
            MethodInfo stop = audio == null ? null : AccessTools.DeclaredMethod(audio, "Stop", Type.EmptyTypes);
            FieldInfo sound = audio == null ? null : AccessTools.DeclaredField(audio, "_sound");
            if (tick == null || !tick.IsStatic || stop == null || !stop.IsStatic || sound == null || !sound.IsStatic)
            {
                Log(PlayerAudioTypeName + ".Tick, Stop or _sound not found: voices stay off on 1.5.7 (TAOM_Voices' own build check).");
                return true;
            }
            _soundField = sound;
            _stopMethod = stop;
            h.Patch(tick, prefix: new HarmonyMethod(typeof(TaomVoicesFixes).GetMethod(nameof(PlayerAudioTickPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
            _readyField = ready;
            h.Patch(start, prefix: new HarmonyMethod(typeof(TaomVoicesFixes).GetMethod(nameof(OnGameStartPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
            h.Patch(check, prefix: new HarmonyMethod(typeof(TaomVoicesFixes).GetMethod(nameof(CheckIdentityPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
            Log("Build check on Bannerlord 1.5.7 installed: TAOM_Voices accepts the 1.5.7.125802 game files the AIO checked, in single player only.");
            return true;
        }

        internal override void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
        }

        // Before TAOM_Voices' campaign start: in co-op (or when that cannot be read) clear Ready, so TAOM_Voices runs
        // its build check again; the check then fails and TAOM_Voices' own catch unpatches itself and keeps Ready false.
        private static void OnGameStartPrefix()
        {
            try
            {
                if (_readyField == null || !CoopEnv.IsSessionActiveOrUnknown())
                    return;
                if ((bool)_readyField.GetValue(null))
                {
                    _readyField.SetValue(null, false);
                    LogOnce("coop-reset", "Co-op session: voices from an earlier game in this process are switched off (TAOM_Voices' own clean-up).");
                }
            }
            catch (Exception e)
            {
                LogOnce("prefix-error", "Campaign start check failed (nothing changed): " + e.Message);
            }
        }

        // true = TAOM_Voices' sound tick runs. false while the game has no campaign player troop, when the tick's
        // first line (Hero.MainHero) would throw; a clip that is still playing is stopped (Stop reads no hero).
        private static bool PlayerAudioTickPrefix()
        {
            try
            {
                Game game = Game.Current;
                if (game != null && game.PlayerTroop is TaleWorlds.CampaignSystem.CharacterObject)
                    return true;
                if (_soundField.GetValue(null) != null)
                    _stopMethod.Invoke(null, null);
                LogOnce("tick-skip", "Sound tick skipped while there is no campaign player (game loading, main menu, custom battle); this is the NullReferenceException at PlayerAudio.cs line 73 that TAOM caught on 18 Nov 2026.");
                return false;
            }
            catch (Exception e)
            {
                LogOnce("tick-error", "Sound tick guard failed (tick skipped this frame): " + e.Message);
                return false;
            }
        }

        // true = TAOM_Voices' own check runs (and throws for a file it does not know); false = accepted.
        private static bool CheckIdentityPrefix(Assembly assembly, string expected)
        {
            try
            {
                if (CoopEnv.IsSessionActiveOrUnknown())
                {
                    LogOnce("coop", "Co-op session (or not readable): TAOM_Voices' own build check runs, voices stay off.");
                    return true;
                }
                if (assembly == null)
                    return true;
                string name = assembly.GetName().Name;
                if (!Validated.TryGetValue(name, out string want))
                {
                    LogOnce("unknown|" + name, "Build check: " + name + " is not one of the files the AIO checked; TAOM_Voices' own check runs.");
                    return true;
                }
                string have = HashOf(assembly.Location);
                if (!string.Equals(have, want, StringComparison.Ordinal))
                {
                    LogOnce("mismatch|" + name, "Build check: " + name + ".dll is not the Bannerlord 1.5.7.125802 file the AIO checked (game updated?); voices stay off.");
                    return true;
                }
                bool all;
                lock (Accepted)
                {
                    Accepted.Add(name);
                    all = Accepted.Count == Validated.Count;
                }
                if (all)
                    LogOnce("accepted", "Build check: accepted the Bannerlord 1.5.7.125802 files (" + Validated.Count + " of " + Validated.Count + " checked by the AIO); build check passed (TAOM_Voices then sets itself up; its own log line says if that fails).");
                return false;
            }
            catch (Exception e)
            {
                LogOnce("check-error", "Build check failed (TAOM_Voices' own check runs, voices stay off): " + e.Message);
                return true;
            }
        }

        private static string HashOf(string path)
        {
            lock (HashCache)
            {
                if (HashCache.TryGetValue(path, out string cached))
                    return cached;
            }
            string hex;
            using (FileStream s = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                hex = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
            lock (HashCache)
                HashCache[path] = hex;
            return hex;
        }

        private static void LogOnce(string key, string message)
        {
            lock (LoggedKeys)
            {
                if (!LoggedKeys.Add(key))
                    return;
            }
            try { _log?.Invoke(message); } catch { }
        }

        private static Exception OnGameStartFinalizer(Exception __exception)
        {
            if (__exception == null)
                return null;
            if (!_logged)
            {
                _logged = true;
                try { _log?.Invoke("TAOM_Voices failed at campaign start (caught, loading continues; voices off for this game): " + __exception); } catch { }
            }
            return null;
        }
    }
}
