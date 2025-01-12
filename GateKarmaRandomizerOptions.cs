using Menu.Remix.MixedUI;
using UnityEngine;

namespace GateKarmaRandomizer;

public class GateKarmaRandomizerOptions : OptionInterface
{
    public static Configurable<int> Seed;
    public static Configurable<bool> ScugBasedSeed;
    public static Configurable<bool> DynamicRNG;
    public static Configurable<int> MaximumKarma;

    public GateKarmaRandomizerOptions()
    {
        Seed = this.config.Bind<int>("Seed", UnityEngine.Random.Range(0, int.MaxValue), new ConfigAcceptableRange<int>(0, int.MaxValue));
        ScugBasedSeed = this.config.Bind<bool>("ScugBasedRNG", true);
        DynamicRNG = this.config.Bind<bool>("RandomKarmaPerSession", false);

        MaximumKarma = this.config.Bind<int>("MaximumKarma", 5, new ConfigAcceptableRange<int>(1, Hooks.KarmaExpansionMaxKarma)); // TODO: Should be dynamic using Hooks.KarmaCap so its not dependent on KE
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
            new OpUpdown(Seed, new Vector2(10f, 490f), 120f) {description = "Seed used to determine gate rng. Change it if you want a different experience than before" },

            new OpLabel(10f, 460f, "Scug Based Seed"),
            new OpCheckBox(ScugBasedSeed, 10f, 430f) {description = "If enabled, will make campaigns have different gate requirements even with the same seed." },

            new OpLabel(10f, 400f, "Dynamic Gate Karma"),
            new OpCheckBox(DynamicRNG, 10f, 370f) {description = "If enabled, makes gate karma dynamic throughout a playthrough. (might not be as fun)" },

            new OpLabel(10f, 300f, "Maximum Karma Requirement"),
            new OpUpdown(MaximumKarma, new Vector2(10f, 270f), 120f) { description = "The maximum karma requirement that gates can randomly be assigned." }
        };

        opTab.AddItems(UIArrRandomOptions);
    }
    public override void Update()
    {
        //if (((OpUpdown)UIArrRandomOptions[2]).IsInt)
    }
}
