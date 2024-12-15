using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Runtime.Remoting.Contexts;
using System.Text.RegularExpressions;
using System.Reflection;
using IL.MoreSlugcats;
using System.IO;
using Menu;
using static ExtraExtentions;
using SlugcatSelectMenu = On.Menu.SlugcatSelectMenu;

namespace GateKarmaRandomizer;
internal class Hooks
{
    public static ManualLogSource Logger;
    public static string CurrentSlug;
    public static string GateRoomName;
    public static Dictionary<string, (int, int)> GateRequirements;

    static bool IsInit;

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
    }

    public static void Unapply()
    {
        On.RainWorld.OnModsInit -= RainWorld_OnModsInit; ;
        On.RainWorld.PostModsInit -= RainWorld_PostModsInit;
        On.SaveState.ctor -= SaveState_ctor;
        On.SaveState.LoadGame -= SaveState_LoadGame;
        On.RainWorldGame.ShutDownProcess -= RainWorldGameOnShutDownProcess;
        On.GameSession.ctor -= GameSessionOnctor;
        IL.RegionGate.ctor -= RegionGate_ctor;
    }

    private static void SetSlugName(SaveState save)
    {
        if (save.saveStateNumber.value != "Slugpup")
        {
            CurrentSlug = save.saveStateNumber.value; // Character playing currently

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

        Logger.LogMessage("Hooking Il.RegionGate_ctor");
        IL.RegionGate.ctor += RegionGate_ctor;

    }

    private static void RandomizeGates()
    {
        GateRequirements = new Dictionary<string, (int, int)>();
        Logger.LogMessage($"Randomizing gates with seed: {GateKarmaRandomizerOptions.Seed.Value}, dynamicrng: {GateKarmaRandomizerOptions.DynamicRNG.Value}");
        if (GateKarmaRandomizerOptions.Seed.Value != 0)
        {
            if (GateKarmaRandomizerOptions.ScugBasedSeed.Value)
            {
                UnityEngine.Random.InitState(CurrentSlug.GetHashCode() + GateKarmaRandomizerOptions.Seed.Value);
            }
            else
            {
                UnityEngine.Random.InitState(GateKarmaRandomizerOptions.Seed.Value);
            }
        }
        else // If seed is 0, make it random
        {
            // TODO: fix this since its currently random everytime even if dynamicrng is off
            UnityEngine.Random.InitState((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        string[] gateRequirements = File.ReadAllLines(AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + "Gates" + Path.DirectorySeparatorChar + "locks.txt"));
        Logger.LogMessage($"Found {gateRequirements.Length} gates in gates.txt");
        try
        {
            for (int i = 0; i < gateRequirements.Length; ++i)
            {
                string[] splitGate = Regex.Split(gateRequirements[i], " : ");
                int rng1 = UnityEngine.Random.Range(1, 6);
                int rng2 = UnityEngine.Random.Range(1, 6);

                // hack for underhang -> pebbles since for some reason mergedmods' gates.txt has 2 SS_UW gates
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
    }

    private static void RegionGate_ctor(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        Logger.LogMessage("IN RegionGate_ctor");

        cursor.GotoNext(MoveType.After,
            i => i.MatchLdcI4(2),
            i => i.MatchLdelemRef(),
            i => i.MatchCallvirt(typeof(string), nameof(string.Trim)),
            i => i.MatchLdcI4(0),
            i => i.MatchNewobj(typeof(RegionGate.GateRequirement)
                .GetConstructor(new[] { typeof(string), typeof(bool) })),
            i => i.MatchStelemRef()
        );

        Logger.LogMessage("Cursor directed");

        try
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(RegionGate).GetField("karmaRequirements"));
            cursor.Emit(OpCodes.Ldc_I4, 0);

            if (!GateKarmaRandomizerOptions.DynamicRNG.Value) // Keep karma static throughout playthrough
            {
                // Randomises gate requirements everytime a gate is *first* loaded (i.e when joining world/travelling to new region)
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(RegionGate).GetField("room"));
                cursor.EmitDelegate<Func<Room, RegionGate.GateRequirement>>(currentRoom =>
                {
                    GateRoomName = currentRoom.abstractRoom.name;
                    return new RegionGate.GateRequirement(GateRequirements[GateRoomName].Item1.ToString());
                });
                cursor.Emit(OpCodes.Stelem_Ref);

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(RegionGate).GetField("karmaRequirements"));
                cursor.Emit(OpCodes.Ldc_I4, 1);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(RegionGate).GetField("room"));
                cursor.EmitDelegate<Func<Room, RegionGate.GateRequirement>>(currentRoom =>
                {
                    GateRoomName = currentRoom.abstractRoom.name;
                    return new RegionGate.GateRequirement(GateRequirements[GateRoomName].Item2.ToString());
                });
                cursor.Emit(OpCodes.Stelem_Ref);
            }
            else // Make gate karma dynamic throughout session
            {
                // Randomises gate requirements everytime a gate is *first* loaded (i.e when joining world/travelling to new region)
                cursor.Emit(OpCodes.Ldc_I4, 5);
                cursor.Emit(OpCodes.Ldc_I4, 0);
                cursor.EmitDelegate<Func<int, int, RegionGate.GateRequirement>>((min, max) =>
                {
                    var rng1 = UnityEngine.Random.Range(min, max);
                    return new RegionGate.GateRequirement(rng1.ToString());

                });
                cursor.Emit(OpCodes.Stelem_Ref);

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(RegionGate).GetField("karmaRequirements"));
                cursor.Emit(OpCodes.Ldc_I4, 1);
                cursor.Emit(OpCodes.Ldc_I4, 5);
                cursor.Emit(OpCodes.Ldc_I4, 0);
                cursor.EmitDelegate<Func<int, int, RegionGate.GateRequirement>>((min, max) =>
                {
                    var rng2 = UnityEngine.Random.Range(min, max);
                    return new RegionGate.GateRequirement(rng2.ToString());
                });
                cursor.Emit(OpCodes.Stelem_Ref);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;
        IsInit = true;

        Logger.LogDebug("IN RainWorld_OnModsInit");
        MachineConnector.SetRegisteredOI("melons.gatekarmarandomizer", new GateKarmaRandomizerOptions());


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
        //YourList.Clear();
    }

    #endregion
}
