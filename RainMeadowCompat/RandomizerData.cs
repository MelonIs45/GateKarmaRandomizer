using System;
using System.Linq;
using RainMeadow;
using static GateKarmaRandomizer.Hooks;

namespace RainMeadowCompat;

/**<summary>
 * Syncs config data between the host and the clients.
 * 
 * Use EasyConfigSync to list which configs you want synced.
 * </summary>
 */
public class RandomizerData : ManuallyUpdatedData
{
    //We don't want clients overriding the host's settings
    public override bool HostControlled => true;

    public RandomizerData() {
        CurrentState = new RandomizerState(this);
    }
    

    public override void UpdateData()
    {
        CurrentState = new RandomizerState(this);
    }

    private class RandomizerState : ManuallyUpdatedState
    {
        public override Type GetDataType() => typeof(RandomizerData);

        [OnlineField]
        string[] GateKeys;
        [OnlineField]
        int[] GateLocks1;
        [OnlineField]
        int[] GateLocks2;

        /**<summary>
         * An empty constructor is necessary. Rain Meadow will not start without it.
         * I don't know why...
         * 
         * I have no clue when or why this happens, so just assume it "never does."
         * </summary>
         */
        public RandomizerState() : base(null) { }
        public RandomizerState(RandomizerData data) : base(data)
        {
            GateKeys = GateRequirements.Keys.ToArray();
            GateLocks1 = new int[GateRequirements.Count];
            GateLocks2 = new int[GateRequirements.Count];

            int i = 0;
            foreach ((int, int) locks in GateRequirements.Values)
            {
                GateLocks1[i] = locks.Item1;
                GateLocks2[i] = locks.Item2;
                i++;
            }

            MeadowCompatSetup.LogSomething("Initialized a new RandomizerState.");

        }

        public override void UpdateReceived(ManuallyUpdatedData data, OnlineResource resource)
        {
            GateRequirements.Clear();

            for (int i = 0; i < GateKeys.Length; i++)
            {
                //there are "simpler ways" to make a dictionary in one line, but manually looping through and adding is easiest to me
                GateRequirements.Add(GateKeys[i], (GateLocks1[i], GateLocks2[i]));
            }

            MeadowCompatSetup.LogSomething("Updated randomizer values.");
        }
    }
}
