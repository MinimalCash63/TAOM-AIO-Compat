// XorberaxCoopGuards: keeps Xorberax's Legacy "Banks of Calradia" from running on a co-op client
// (Xorberax's Legacy 1.0.8.8). XorberaxLegacy.dll is not referenced; every target is found by name and a
// target that is not found is left alone (one log line).
//
// Why (audit 14 Nov 2026, Coop_client.log 14 Nov 11:35:31 and the installed DLLs): opening the bank menu
// on a client runs BanksofCalradiaCampaignBehavior.GetBankDataAtSettlement, which builds a Hero and a Clan
// for the banker. Co-op logs these but does not stop them, so the client is left with a hero and a clan
// that have no id ("Failed to get id for ... Clan, null"). Deposits, withdrawals and loans change the
// player's gold with Hero.ChangeHeroGold, which on a client is applied locally only and later overwritten
// by the host, while the bank fees (GiveGoldAction) are dropped by Co-op. The bank accounts a client holds
// in memory are the host player's (they arrive with the host's save), so the daily interest and the
// penalties for the host's unpaid loans would be applied to the joining player's hero. Nothing a client
// does at the bank is saved.
//
// What, only while CoopEnv.IsCoopClient():
//   BanksofCalradiaCampaignBehavior.CanPlayerAccessBankAtSettlement(Settlement) : ValueTuple<bool,string>
//       returns (false, reason). Its only caller is the "Go to the bank" option of the town menu, which
//       then shows the option greyed out with the reason as its tooltip; the bank menu cannot open.
//   BanksofCalradiaCampaignBehavior.OnDailyTick()                                   skipped
//   BanksofCalradiaCampaignBehavior.ApplyInitialBankRetaliationForUnpaidLoan(Settlement)   skipped
// Honest Work, Training Field and Scholars are not touched: on a client Co-op itself drops their gold,
// relation and skill changes.
//
// Installed once and kept until the switch is uninstalled; the condition is evaluated on every call.
// TryInstall is called at the main menu, game start and mission start until it returns true. It waits for
// the first game start (Game.Current is set): Harmony runs the static initializer of the class it patches
// (shown with Harmony 2.4.2 in the offline proof, 14 Nov 2026), and this class's initializer reads Xorberax's
// MCM settings into a static field for good. At a game start MCM is ready and Xorberax builds this class
// itself; at the main menu that is not certain.
// Co-op: host's server process and single player: never skips, Xorberax runs as before. Client: the bank is
// not available; the host's bank is not affected. Nothing here changes synced state.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Fixes.XorberaxLegacy
{
    internal static class XorberaxCoopGuards
    {
        private const string HarmonyId = "taom.compat.aio.xorberax.coopguards";
        private const string XorberaxAssembly = "XorberaxLegacy";
        private const string BankBehaviorType = "XorberaxLegacy.Logic.BanksofCalradiaCampaignBehavior";
        internal const string ClientReason = "Banks are not available to joining players in a co-op session.";

        private static Harmony _harmony;
        private static bool _done;
        private static Action<string> _log = _ => { };
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>A game (campaign or battle) has been created. Replaced by the offline proof only.</summary>
        internal static Func<bool> GameExists = () => Game.Current != null;

        /// <summary>
        /// False while XorberaxLegacy.dll is not loaded or no game has started yet (call again at the next
        /// event). True when finished: installed, or not installable (one log line per missing target).
        /// Never throws.
        /// </summary>
        internal static bool TryInstall(Action<string> log)
        {
            if (_done)
                return true;
            _log = log ?? (_ => { });
            try
            {
                Assembly asm = ModEnv.FindAssembly(XorberaxAssembly);
                if (asm == null)
                    return false;
                if (!GameExists())
                    return false; // not at the main menu: see the header
                _done = true;
                Type bank = asm.GetType(BankBehaviorType, false);
                if (bank == null)
                {
                    _log("Co-op bank guard: " + BankBehaviorType + " not found. Not installed.");
                    return true;
                }
                var h = new Harmony(HarmonyId);
                _harmony = h;
                int installed = 0;
                if (Guard(h, bank, "CanPlayerAccessBankAtSettlement", nameof(ClientDenyBankAccess), typeof(ValueTuple<bool, string>), "Settlement")) installed++;
                if (Guard(h, bank, "OnDailyTick", nameof(ClientSkipDailyTick), typeof(void))) installed++;
                if (Guard(h, bank, "ApplyInitialBankRetaliationForUnpaidLoan", nameof(ClientSkipLoanRetaliation), typeof(void), "Settlement")) installed++;
                _log("Co-op bank guard: " + installed + " of 3 parts installed.");
            }
            catch (Exception e)
            {
                _done = true;
                _log("Co-op bank guard: install failed: " + e.Message + ".");
            }
            return true;
        }

        internal static void Uninstall()
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _done = false;
        }

        /// <summary>Finds type.method declared on the type itself with this return type and these parameter type names and adds the prefix.</summary>
        private static bool Guard(Harmony h, Type type, string method, string prefix, Type returnType, params string[] parameterTypeNames)
        {
            string label = method + "(" + string.Join(", ", parameterTypeNames) + ")";
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo[] hits = type.GetMethods(flags).Where(m => m.Name == method && m.ReturnType == returnType
                    && m.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(parameterTypeNames)).ToArray();
                if (hits.Length != 1)
                {
                    _log("Co-op bank guard " + label + ": " + hits.Length + " matches in " + type.Name + ". Skipped.");
                    return false;
                }
                MethodInfo patch = typeof(XorberaxCoopGuards).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
                h.Patch(hits[0], prefix: new HarmonyMethod(patch));
                return true;
            }
            catch (Exception e)
            {
                _log("Co-op bank guard " + label + ": install failed: " + e.Message + ". Skipped.");
                return false;
            }
        }

        // ---- Prefixes. Returning false skips Xorberax's method.

        private static bool ClientDenyBankAccess(ref ValueTuple<bool, string> __result)
        {
            if (!IsClient("access"))
                return true;
            __result = new ValueTuple<bool, string>(false, ClientReason);
            LogOnce("access", "Co-op client: the bank is switched off on this machine (the host's game owns the bank accounts). Logged once.");
            return false;
        }

        private static bool ClientSkipDailyTick()
        {
            if (!IsClient("daily"))
                return true;
            LogOnce("daily", "Co-op client: skipped the bank's daily interest and loan check on this machine. Logged once.");
            return false;
        }

        private static bool ClientSkipLoanRetaliation()
        {
            if (!IsClient("loan"))
                return true;
            LogOnce("loan", "Co-op client: skipped a bank loan penalty on this machine (the loan belongs to the host's game). Logged once.");
            return false;
        }

        private static bool IsClient(string key)
        {
            try
            {
                return CoopEnv.IsCoopClient();
            }
            catch (Exception e)
            {
                LogOnce("error|" + key, "Co-op bank guard (" + key + ") failed (Xorberax runs as usual): " + e.Message);
                return false;
            }
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
