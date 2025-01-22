﻿

namespace RainMeadowCompat;

/**<summary>
 * Apparently I don't understand how soft-compatibility works too well...
 * Trying to call or reference anything in any file that says:
 * using RainMeadow;
 * will cause an error.
 * Therefore, this file exists for you to safely signal to signal your Rain Meadow data
 * or receive Rain Meadow information
 * without crashing the game if Rain Meadow is not installed.
 * Thanks to try/catches. Put a lot of them in here.
 * </summary>
 */
public class SafeMeadowInterface
{
    /**<summary>
     * The easiest way to set up Meadow compatibility, since everything is managed here.
     * Should be called by OnEnable().
     * MUST be called before mods are initialized.
     * If mods are already initialized, use ModsInitialized() instead.
     * </summary>
     */
    public static void InitializeMeadowCompatibility()
    {
        try
        {
            MeadowCompatSetup.InitializeMeadowCompatibility();
        } catch { }
    }

    /**<summary>
     * Should be called when or after mods are initialized.
     * Automatically called if InitializeMeadowCompatibility() was called at OnEnable().
     * Checks if Rain Meadow is installed.
     * </summary>
     */
    public static void ModsInitialized()
    {
        try
        {
            MeadowCompatSetup.ModsInitialized();
        } catch { }
    }

    /**<summary>
     * Should be called by OnDisable().
     * Removes any hooks added by this file.
     * </summary>
     */
    public static void RemoveHooks()
    {
        try
        {
            MeadowCompatSetup.RemoveHooks();
        } catch { }
    }

    /**<summary>
     * Signals that the randomizer data should be updated.
     * </summary>
     */
    public static void UpdateRandomizerData()
    {
        try
        {
            MeadowInterface.UpdateRandomizerData();
        }
        catch { }
    }

}
