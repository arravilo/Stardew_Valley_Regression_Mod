using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Runtime.CompilerServices;

namespace Regression.PrimevalTitmouse
{
    internal class ObjectPatches
    {
        private static IMonitor Monitor;

        // call this method from your Entry class
        internal static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        // patches need to be static!
        internal static bool Debris__findBestPlayer(Debris __instance, GameLocation location, ref Farmer __result)
        {
            try
            {
                if (__instance.itemId.Value == "Poop")
                {
                    __result = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed in Regression mod patched {nameof(Debris__findBestPlayer)}:\n{ex}", LogLevel.Error);
            }
            return true; // run original logic
        }
    }
}
