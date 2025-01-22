using RainMeadow;

namespace RainMeadowCompat;

/**<summary>
 * This file is here for you to safely signal to signal your Rain Meadow data
 * or receive Rain Meadow information
 * through the SafeMeadowInterface class.
 * 
 * Anything you want to call here should always be called by SafeMeadowInterface instead.
 * 
 * All functions in this file are PURELY examples.
 * </summary>
 */
public class MeadowInterface
{
    /**<summary>
     * Signals that the randomizer data should be updated.
     * </summary>
     */
    public static void UpdateRandomizerData()
    {
        if (!MeadowCompatSetup.MeadowEnabled) return;

        try
        {
            OnlineManager.lobby.GetData<RandomizerData>().UpdateData();
        }
        catch { return; }
    }

}
