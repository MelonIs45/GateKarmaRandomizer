using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using MonoMod.RuntimeDetour;
using RainMeadowCompat;
using System.Reflection;
using MoreSlugcats;
using System.Globalization;
using System.Linq;
using DevInterface;
using Expedition;
using HUD;
using Menu;
using static ExtraExtentions;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace GateKarmaRandomizer;
internal class Hooks
{
    public static ManualLogSource Logger;
    public static string CurrentSlug;
    public static Dictionary<string, (int, int)> GateRequirements = new();
    public static int KarmaCap = 5;
    public const int RegionKitMaxKarma = 10;
    public const int KarmaExpansionMaxKarma = 34;
    public static int MaxKarmaReq => Math.Min(KarmaCap, GateKarmaRandomizerOptions.MaximumKarma.Value);

    private static bool IsInit;

    public static void Apply(ManualLogSource logger)
    {
        Logger = logger;
        Logger.LogMessage("IN Hooks.Apply");

        On.RainWorld.OnModsInit += RainWorld_OnModsInit; ;
        On.RainWorld.PostModsInit += RainWorld_PostModsInit;
        On.SaveState.ctor += SaveState_ctor;
        On.SaveState.LoadGame += SaveState_LoadGame;
        On.RainWorldGame.ShutDownProcess += RainWorldGameOnShutDownProcess;
        On.GameSession.ctor += GameSessionOnctor;
        On.RegionGate.ctor += RegionGateOnctor;
        On.RegionGate.customKarmaGateRequirements += RegionGate_customKarmaGateRequirements;
        On.RegionGate.customOEGateRequirements += RegionGate_customOEGateRequirements;
        On.HUD.Map.MapData.KarmaOfGate += MapData_KarmaOfGate;
        On.HUD.Map.ctor += Map_ctor;
    }

    public static void Unapply()
    {
        On.RainWorld.OnModsInit -= RainWorld_OnModsInit; ;
        On.RainWorld.PostModsInit -= RainWorld_PostModsInit;
        On.SaveState.ctor -= SaveState_ctor;
        On.SaveState.LoadGame -= SaveState_LoadGame;
        On.RainWorldGame.ShutDownProcess -= RainWorldGameOnShutDownProcess;
        On.GameSession.ctor -= GameSessionOnctor;
        On.RegionGate.ctor -= RegionGateOnctor;
        On.RegionGate.customKarmaGateRequirements -= RegionGate_customKarmaGateRequirements;
        On.RegionGate.customOEGateRequirements -= RegionGate_customOEGateRequirements;
        On.HUD.Map.MapData.KarmaOfGate -= MapData_KarmaOfGate;
        On.HUD.Map.ctor -= Map_ctor;
    }

    private static void SetSlugName(SaveState save)
    {
        if (save.saveStateNumber.value != "Slugpup")
        {
            CurrentSlug = save.saveStateNumber.value; // Character currently being playing 

            if (!GateKarmaRandomizerOptions.DynamicRNG.Value)
            {
                Logger.LogMessage("Randomizing Gates...");
                RandomizeGates();
            }
            Logger.LogMessage(CurrentSlug);
        }
    }

    private static void SaveState_LoadGame(On.SaveState.orig_LoadGame orig, SaveState self, string str, RainWorldGame game)
    {
        orig(self, str, game);
            
        Logger.LogMessage("IN SaveState_LoadGame");
        SetSlugName(self);
    }

    private static void SaveState_ctor(On.SaveState.orig_ctor orig, SaveState self, SlugcatStats.Name saveStateNumber, PlayerProgression progression)
    {
        orig(self, saveStateNumber, progression);

        Logger.LogMessage("IN SaveState_ctor");
        SetSlugName(self);
    }
    private static void RandomizeGates()
    {
        //GateRequirements = new Dictionary<string, (int, int)>();
        GateRequirements.Clear();
        Logger.LogMessage($"Randomizing gates with seed: {GateKarmaRandomizerOptions.Seed.Value}, DynamicRNG: {GateKarmaRandomizerOptions.DynamicRNG.Value}");

        UnityEngine.Random.InitState(GateKarmaRandomizerOptions.Seed.Value);
        if (GateKarmaRandomizerOptions.ScugBasedSeed.Value)
        {
            UnityEngine.Random.InitState(CurrentSlug.GetHashCode() + GateKarmaRandomizerOptions.Seed.Value);
        }

        string[] gateRequirements = File.ReadAllLines(AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "Gates" + Path.DirectorySeparatorChar + "locks.txt"));
        Logger.LogMessage($"Found {gateRequirements.Length} gates in gates.txt");
        try
        {
            for (int i = 0; i < gateRequirements.Length; ++i)
            {
                string[] splitGate = Regex.Split(gateRequirements[i], " : ");
                int rng1 = UnityEngine.Random.Range(1, MaxKarmaReq + 1);
                int rng2 = UnityEngine.Random.Range(1, MaxKarmaReq + 1);

                // Hack for underhang -> pebbles since for some reason mergedmods' gates.txt has 2 SS_UW gates
                // even those there are is a seperate UW_SS aswell :/
                if (!GateRequirements.ContainsKey(splitGate[0]))
                {
                    GateRequirements.Add(splitGate[0], (Convert.ToInt32(rng1), Convert.ToInt32(rng2)));
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }

        SafeMeadowInterface.UpdateRandomizerData();
    }

    // Randomizes the gate lock if DynamicRNG is enabled
    private static void RegionGateOnctor(On.RegionGate.orig_ctor orig, RegionGate self, Room room)
    {
        if (GateKarmaRandomizerOptions.DynamicRNG.Value)
        {
            var reqs = (UnityEngine.Random.Range(1, MaxKarmaReq + 1), UnityEngine.Random.Range(1, MaxKarmaReq + 1));
            string gateName = room.abstractRoom.name;
            if (GateRequirements.ContainsKey(gateName))
                GateRequirements[gateName] = reqs;
            else
            {
                GateRequirements.Add(gateName, reqs);
                SafeMeadowInterface.UpdateRandomizerData();
            }
        }

        orig(self, room);
    }

    // Alters gate locks without manually merging changes into locks.txt
    public static void RegionGate_customKarmaGateRequirements(On.RegionGate.orig_customKarmaGateRequirements orig, RegionGate self)
    {
        orig(self);

        try
        {
            // Alter gate locks
            if (GateRequirements.TryGetValue(self.room.abstractRoom.name, out var reqs))
            {
                self.karmaRequirements[0].value = Math.Min(reqs.Item1, MaxKarmaReq).ToString();
                self.karmaRequirements[1].value = Math.Min(reqs.Item2, MaxKarmaReq).ToString();
                Logger.LogDebug("Set custom gate locks for " + self.room.abstractRoom.name + ": " + reqs.Item1 + ", " + reqs.Item2);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    // Alters the map symbols, again without manually merging locks.txt
    public static RegionGate.GateRequirement MapData_KarmaOfGate(On.HUD.Map.MapData.orig_KarmaOfGate orig, HUD.Map.MapData self, PlayerProgression progression, World initWorld, string roomName)
    {
        RegionGate.GateRequirement origRequirement = orig(self, progression, initWorld, roomName);

        try
        {
            if (GateRequirements.TryGetValue(roomName, out var reqs)
                && origRequirement != null && origRequirement?.value != null)
            {
                //look through locks file to figure out if mapswapped or not
                bool mapSwapped = false;
                foreach (string line in progression.karmaLocks)
                {
                    string[] data = Regex.Split(line, " : ");
                    if (data[0] == roomName)
                    {
                        if (data.Length >= 4 && data[3] == "SWAPMAPSYMBOL")
                            mapSwapped = true;
                        break;
                    }
                }

                origRequirement.value = Math.Min(reqs.Item1, MaxKarmaReq).ToString();
                // Correct karma value
                if (Region.EquivalentRegion(Regex.Split(roomName, "_")[1], initWorld.region.name) == mapSwapped)
                {
                    origRequirement.value = Math.Min(reqs.Item2, MaxKarmaReq).ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }

        return origRequirement;
    }


    private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;
        IsInit = true;

        Logger.LogDebug("IN RainWorld_OnModsInit");
        MachineConnector.SetRegisteredOI("melons.gatekarmarandomizer", new GateKarmaRandomizerOptions());

        // Set the karma cap according to currently applied mods
        KarmaCap = 5;
        foreach (ModManager.Mod mod in ModManager.ActiveMods)
        {
            if (mod.id == "rwmodding.coreorg.rk") // Region Kit
                KarmaCap = Math.Max(KarmaCap, RegionKitMaxKarma);
            else if (mod.id == "LazyCowboy.KarmaExpansion") // Karma Expansion
                KarmaCap = Math.Max(KarmaCap, KarmaExpansionMaxKarma);
        }
    }

    // Doesn't make the outer expanse gate icons red
    public static bool RegionGate_customOEGateRequirements(On.RegionGate.orig_customOEGateRequirements orig, RegionGate self)
    {
        if (self.karmaRequirements[0] != MoreSlugcatsEnums.GateRequirement.OELock && self.karmaRequirements[1] != MoreSlugcatsEnums.GateRequirement.OELock)
            return true;
        return orig(self);
    }

    public static void Map_ctor(On.HUD.Map.orig_ctor orig, Map self, HUD.HUD hud, Map.MapData mapData)
    {
        // Remove oe karma lock

        //orig(self, hud, mapData);
        SaveState saveState = null;

        if (hud.owner.GetOwnerType() == HUD.HUD.OwnerType.Player ||
            hud.owner.GetOwnerType() == HUD.HUD.OwnerType.FastTravelScreen ||
            hud.owner.GetOwnerType() == HUD.HUD.OwnerType.RegionOverview ||
            ModManager.MSC && hud.owner.GetOwnerType() == MoreSlugcatsEnums.OwnerType.SafariOverseer)
        {
            saveState = hud.rainWorld.progression.currentSaveState;
        }
        else if (hud.owner.GetOwnerType() != HUD.HUD.OwnerType.RegionOverview)
        {
            saveState = (hud.owner as SleepAndDeathScreen)?.saveState;
        }

        bool originalMarkState = saveState.deathPersistentSaveData.theMark;

        saveState.deathPersistentSaveData.theMark = true;
        orig(self, hud, mapData);
        saveState.deathPersistentSaveData.theMark = originalMarkState;

        // remove old map karma
        foreach (var item in GateRequirements)
        {
            Logger.LogMessage($"GAte: {item.Key}, left: {item.Value.Item1}, right: {item.Value.Item2}");
        }
        try
        {
            for (int index = 0; index < mapData.gateData.Length; ++index)
            {
                RegionGate.GateRequirement karma = mapData.gateData[index].karma;
                if (ModManager.MSC)
                {
                    if (self.mapData.NameOfRoom(mapData.gateData[index].roomIndex) == "GATE_SB_OE")
                    {

                        if (!karma.Equals(MoreSlugcatsEnums.GateRequirement.OELock))
                        {

                            self.mapObjects.RemoveAll(marker => marker is Map.GateMarker gateMarker && gateMarker.room == mapData.gateData[index].roomIndex);
                            if (self.RegionName != "SB")
                            {
                                karma = new RegionGate.GateRequirement(GateRequirements["GATE_SB_OE"].Item1.ToString());
                            }
                            else
                            {
                                Logger.LogMessage(GateRequirements.Count.ToString());
                                karma = new RegionGate.GateRequirement(GateRequirements["GATE_SB_OE"].Item2.ToString());
                            }

                            self.mapObjects.Add((Map.MapObject)new Map.GateMarker(self,
                                mapData.gateData[index].roomIndex, karma, true));
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
        
    }

    private static void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);
        if (!IsInit) return;

        Logger.LogDebug("IN RainWorld_PostModsInit");
    }

    private static void RainWorldGameOnShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        ClearMemory();
    }
    private static void GameSessionOnctor(On.GameSession.orig_ctor orig, GameSession self, RainWorldGame game)
    {
        orig(self, game);
        ClearMemory();
    }

    #region Helper Methods
    private static void ClearMemory()
    {
        //If you have any collections (lists, dictionaries, etc.)
        //Clear them here to prevent a memory leak
        //GateRequirements.Clear(); // FRICK YOU 6 HOURS GONE OF MY LIFE!!!!!
        //not that it really matters... the game's closing anyway
    }

    #endregion

    #region Unused Code

    //BindingFlags propFlags = BindingFlags.Instance | BindingFlags.Public;
    //BindingFlags myMethodFlags = BindingFlags.Static | BindingFlags.Public;
    //
    //try
    //{
    //    Hook meetRequirementsHook = new Hook( // RegionGate.MeetRequirements get method hook
    //        typeof(RegionGate).GetProperty("MeetRequirement", propFlags).GetGetMethod(),
    //        typeof(Hooks).GetMethod("RegionGate_MeetRequirement_Get", myMethodFlags)
    //    );
    //}
    //catch (Exception ex) { Logger.LogError(ex); }

    // Thought i'd need this but turns out not, will keep incase since rain meadow metropolis gate has a bug, put above hook into RainWorld_OnModsInit
    //
    //public delegate bool orig_MeetRequirement(RegionGate self);
    //public static bool RegionGate_MeetRequirement_Get(orig_MeetRequirement orig, RegionGate self)
    //{
    //    Logger.LogMessage("IN RegionGate_MeetRequirement_Get");
    //    RegionGate.GateRequirement originalRequirement = self.karmaRequirements[!self.letThroughDir ? 1 : 0];
    //    Logger.LogMessage(originalRequirement);


    //    if (originalRequirement == MoreSlugcatsEnums.GateRequirement.RoboLock)
    //    {
    //        self.karmaRequirements[!self.letThroughDir ? 1 : 0] = new RegionGate.GateRequirement(GateRequirements[self.room.abstractRoom.ToString()].Item1.ToString());
    //        Logger.LogMessage("set kr to: " + GateRequirements[self.room.abstractRoom.ToString()].Item1.ToString());
    //        //self.karmaRequirements[!self.letThroughDir ? 1 : 0] = new RegionGate.GateRequirement("1"); // Temp value
    //    }

    //    bool result = orig(self);
    //    Logger.LogMessage("Result: " + result);
    //    self.karmaRequirements[!self.letThroughDir ? 1 : 0] = originalRequirement;

    //    return result;
    //}

    #endregion
}
