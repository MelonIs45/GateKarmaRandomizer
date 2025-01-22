using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using RainMeadowCompat;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace GateKarmaRandomizer;

[BepInPlugin("melons.gatekarmarandomizer", "Gate Karma Randomizer", "1.0.5")]
public class GateKarmaRandomizer : BaseUnityPlugin
{

    public new static ManualLogSource Logger;
    public static bool IsEnabled;

    public void OnEnable()
    {
        if (IsEnabled) return;
        IsEnabled = true;

        Logger = base.Logger;
        Hooks.Apply(Logger);

        SafeMeadowInterface.InitializeMeadowCompatibility();
        
        Logger.LogDebug("IN OnEnable");
    }

    public void OnDisable()
    {
        if (!IsEnabled) return;

        Hooks.Unapply();

        SafeMeadowInterface.RemoveHooks();

        Logger.LogDebug("In OnDisable");
    }
}
