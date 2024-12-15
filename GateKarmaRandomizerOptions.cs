using HarmonyLib;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace GateKarmaRandomizer;

public class GateKarmaRandomizerOptions : OptionInterface
{
    public static Configurable<int> Seed;
    public static Configurable<bool> ScugBasedSeed;
    public static Configurable<bool> DynamicRNG;
    public GateKarmaRandomizerOptions()
    {
        Seed = this.config.Bind<int>("Seed", UnityEngine.Random.Range(0, int.MaxValue), new ConfigAcceptableRange<int>(0, int.MaxValue));
        ScugBasedSeed = this.config.Bind<bool>("ScugBasedRNG", true);
        DynamicRNG = this.config.Bind<bool>("RandomKarmaPerSession", false);
    }

    private UIelement[] UIArrRandomOptions;
    public override void Initialize()
    {

        var opTab = new OpTab(this, "Options");
        this.Tabs = new[]
        {
            opTab
        };

        UIArrRandomOptions = new UIelement[]
        {
            new OpLabel(10f, 550f, "Options", true),
            new OpLabel(10f, 520f, "Karma RNG Seed"),
            new OpUpdown(Seed, new Vector2(10f, 490f), 120f),
        new OpLabel(10f, 460f, "Scug Based Seed"),
        new OpCheckBox(ScugBasedSeed, 10f, 430f),
        new OpLabel(10f, 400f, "Dynamic Playthrough Gate Karma"),
        new OpCheckBox(DynamicRNG, 10f, 370f)
        };

        UIArrRandomOptions[2].description = "Seed used to determine gate rng. Currently there is not a way to randomize, so make sure to set one yourself";
        UIArrRandomOptions[4].description = "If enabled, will set the seed in conjunction with the scug being played.\n" +
                                            "Useful for different scugs having different gates without having to change the seed.";
        UIArrRandomOptions[6].description = "If enabled, makes gate karma dynamic throughout a playthrough. (might not be as fun)";

        opTab.AddItems(UIArrRandomOptions);
    }
    public override void Update()
    {
        //if (((OpUpdown)UIArrRandomOptions[2]).IsInt)
    }
}