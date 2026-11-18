// A part of a switch that is installed once, as soon as it can be: the group calls Try() at the main menu,
// at game start and at mission start. The part's own TryInstall returns false while it cannot be done yet
// (its target assembly is not loaded, or it needs a running game) and true when it is finished, whether it
// installed something or found it cannot (it logs that itself). An exception counts as finished and is
// logged, so a part can never take its switch down or be retried forever.
using System;

namespace TAOM_AIO_Compat.Core
{
    internal sealed class LateInstall
    {
        private readonly string _what;
        private readonly Func<Action<string>, bool> _tryInstall;
        private readonly Action _uninstall;
        private bool _done;

        internal LateInstall(string what, Func<Action<string>, bool> tryInstall, Action uninstall)
        {
            _what = what;
            _tryInstall = tryInstall;
            _uninstall = uninstall;
        }

        internal void Try(Action<string> log)
        {
            if (_done)
                return;
            try { _done = _tryInstall(log); }
            catch (Exception e)
            {
                _done = true;
                try { log(_what + ": not installed (error caught, the rest of this switch is not affected): " + e.Message); } catch { }
            }
        }

        /// <summary>Removes the part and lets it be installed again (used by the group's Uninstall).</summary>
        internal void Uninstall()
        {
            try { _uninstall(); } catch { }
            _done = false;
        }
    }
}
