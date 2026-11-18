// One FixGroup per target mod = one MCM switch. SubModule installs the groups whose switch is on and
// whose mod is active, then forwards game events only to installed groups. Any exception from a group
// is caught by SubModule; the group is then switched off for the session.
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM_AIO_Compat.Core
{
    internal abstract class FixGroup
    {
        /// <summary>Name of the bool property in AioSettings (and key in the saved json).</summary>
        internal abstract string SettingKey { get; }

        /// <summary>Short name for logs and the main-menu line, e.g. "Retinues".</summary>
        internal abstract string Name { get; }

        /// <summary>Module id that must be active for this group to do anything.</summary>
        internal abstract string TargetModuleId { get; }

        /// <summary>
        /// Install as soon as possible, from the SubModule constructor, when the target's assembly is already
        /// loaded at that point (needed when a fix must be in place before the target's OnSubModuleLoad and
        /// the AIO happens to load after the target). Null = install at OnSubModuleLoad only.
        /// </summary>
        internal virtual string EarlyInstallAssembly => null;

        /// <summary>Our older stand-alone fix module for the same mod. If it is still ticked, this group stays off.</summary>
        internal virtual string ReplacesModuleId => null;

        /// <summary>
        /// Off means off (owner, 13 Nov 2026): a switched-off group applies nothing. If that can crash or loop
        /// the game while the target mod is ticked, this says what to untick; it is logged at start-up.
        /// The same text is in the MCM hint, the README and the test checklist.
        /// </summary>
        internal virtual string OffWarning => null;

        /// <summary>Set by SubModule before Install: writes "[Name] message" to the AIO log.</summary>
        internal System.Action<string> Log = _ => { };

        internal bool Installed;
        internal bool Broken;

        /// <summary>Applies the group's fixes. Return false (with the reason logged) if nothing could be applied.</summary>
        internal abstract bool Install();

        internal virtual void OnMainMenu() { }
        internal virtual void OnApplicationTick(float dt) { }
        internal virtual void OnBeforeMissionBehaviorInitialize(Mission mission) { }
        internal virtual void OnGameStart(Game game, IGameStarter starter) { }
        internal virtual void OnGameEnd(Game game) { }
        internal virtual void Uninstall() { }
    }
}
