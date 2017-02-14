using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.Reflection;
using System.Threading;
using RimWorld;
using UnityEngine;
using Verse;

namespace DesirePaths
{
    [StaticConstructorOnStartup]
    internal static class WhatTest
    {
        private static int i = 0;
        static WhatTest()
        {
            //Log.Message("This runs!");
            //var pT = new Thread(DoStuff);
            //pT.Start();
            //Log.Message("Thread started.");
        }

        public static void DoStuff()
        {
            Thread.Sleep(5);
            Game pGame = Current.Game;

            if (pGame?.Maps == null)
            {
                Thread.Sleep(250);
                DoStuff();
                return;
            }

            Log.Message($"iteration {i++}");
            DesirePaths.DoRareThings(pGame.Maps.First());
            

            DoStuff();
        }
    }

    public static class DesirePaths
    {
        private static readonly FieldInfo twc_comps = typeof(ThingWithComps).GetField ("comps", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ppf_last = typeof(Verse.AI.Pawn_PathFollower).GetField ("lastCell", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool s_bFirst = true, s_bUseNasty = false;
        private static TerrainDef s_tMudMarsh, s_tShallowWater;
        private static int s_lLastUpdate;
        private static WeatherDef s_wCurrentWeather;

        public static void TickOverride(this Pawn _this)
        {
            // Run the normal Tick() code
            DefaultTick(_this);
            // Only check every 10 ticks while moving.
            if (!(_this.pather?.Moving ?? false) || !_this.IsHashIntervalTick(10))
                return;

            var fLoc = _this.Position;

            // Some Pawns have a null map on each Tick()...? Kick them out.
            if (_this.Map == null)
                return;

            if (s_bFirst)
            {
                // See if this map has mud.
                s_bFirst = false;

                getTerrains(_this.Map, out s_tMudMarsh, out s_tShallowWater);
                if (s_tMudMarsh != null)
                    s_bUseNasty = true;
            }

            // Update some things every two seconds.
            if (GenTicks.TicksAbs - s_lLastUpdate > 250)
            {
                s_lLastUpdate = GenTicks.TicksAbs;

                DoRareThings(_this.Map);
            }

#if DEBUG
            string sStamp = $"DesirePath - {GenTicks.TicksAbs} {_this.Name}";
#endif
            // Get previous cell via reflection.
            var fLast = (IntVec3) ppf_last.GetValue(_this.pather);

            // Check for plants at this location.
            bool bFoundPlant = false;
            foreach (var pPlant in _this.Map.thingGrid.ThingsAt(fLoc).OfType<Plant>())
            {
                // Damage plants and kill them if needed.
                bFoundPlant = true;

                // Anything smaller than a human doesn't damage grass.
                int lDamage = (int) _this.BodySize;
                pPlant.HitPoints -= lDamage;

                if (pPlant.HitPoints <= 0)
                {
#if DEBUG
                    Log.Message($"{sStamp}\t{pPlant.ToString()} HP: {pPlant.HitPoints}");
#endif
                    pPlant.Destroy(DestroyMode.Kill);
                }
            }

            // 
            if (s_bUseNasty && !bFoundPlant)
            {
                // No plants here... check if it is soil.
                var tTerr = fLoc.GetTerrain(_this.Map);
                if (canWet(tTerr))
                {
#if DEBUG
                    //Log.Message($"{sStamp}\t {fLoc} {pTerr}");
#endif
                    // Chance of altering this tile.
                    float rCrapChance = 0.0f;
                    var tLast = fLast.GetTerrain(_this.Map);

                    // If the pawn is moving out of existing mud or marsh.
                    if (tLast.defName.Contains("Mud") || tLast.defName.Contains("Marsh"))
                        rCrapChance += 0.003f;

                    // If the pawn is unroofed and it is raining.
                    if (!fLoc.Roofed(_this.Map))
                        rCrapChance += 0.002f * s_wCurrentWeather.rainRate;

                    if (UnityEngine.Random.Range(0f, 1f) < rCrapChance)
                    {
#if DEBUG
                        Log.Message($"\t made it bad at {fLoc}!");
#endif
                        _this.Map.terrainGrid.SetTerrain(fLoc, s_tMudMarsh);
                    }
                }
            }

            float rDepth = fLoc.GetSnowDepth(_this.Map);
            if (rDepth > 0f)
            {
                rDepth -= 0.01f * _this.BodySize;
                if (rDepth < 0.0f)
                    rDepth = 0.0f;
#if DEBUG
                Log.Message($"{sStamp}\tSnow: {rDepth}");
#endif
                _this.Map.snowGrid.SetDepth(fLoc, rDepth);
            }
        }

        public static void DoRareThings(Map pMap)
        {
            // Update the weather.
            s_wCurrentWeather = pMap.weatherManager.curWeather;

            foreach (var fLoc in pMap.AllCells)
            {
                var tTerr = fLoc.GetTerrain(pMap);
                if (fLoc.OnEdge(pMap)) // Edge of the map stays wet.
                    // Not changing
                    continue;

                float rTemp = fLoc.GetTemperature(pMap);
                if (s_wCurrentWeather.rainRate <= 0f && rTemp > 20f && tTerr.driesTo != null)
                {
                    if (!fLoc.Roofed(pMap) && s_wCurrentWeather.rainRate > 0.0f)
                        continue;

                    float rDryChance = (rTemp - 20) / 25f;
                   
                    // Reduce chance per adjacent tile of goop/liquid.
                    IntVec3 fTest;
                    TerrainDef tTerrAdj;

                    fTest = new IntVec3(fLoc.x - 1, 0, fLoc.z - 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x - 1, 0, fLoc.z);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x - 1, 0, fLoc.z + 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x, 0, fLoc.z - 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x, 0, fLoc.z + 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x + 1, 0, fLoc.z - 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x + 1, 0, fLoc.z);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x + 1, 0, fLoc.z + 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rDryChance += getDryAdjMod(tTerrAdj);

                    // Potentially dry it out.
                    if (UnityEngine.Random.Range(0f, 1f) < rDryChance)
                    {
                        var tDries = tTerr.driesTo;
                        if (tTerr.defName.Contains("Water"))
                            tDries = s_tMudMarsh;
#if DEBUG
                        Log.Message(
                            $"{fLoc} dry {tTerr.defName} to {tDries.defName} Temp: {rTemp} / {rDryChance}");
#endif
                        pMap.terrainGrid.SetTerrain(fLoc, tDries);
                    }
                }
                else if (s_wCurrentWeather.rainRate > 0f && canWet(tTerr))
                {
                    // Dry tiles... get wet in the rain sometimes?
                    float rWetChance = 0f;

                    // Increase chance per adjacent tile of liquid.
                    IntVec3 fTest;
                    TerrainDef tTerrAdj;

                    fTest = new IntVec3(fLoc.x - 1, 0, fLoc.z - 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x - 1, 0, fLoc.z);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x - 1, 0, fLoc.z + 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x, 0, fLoc.z - 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x, 0, fLoc.z + 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x + 1, 0, fLoc.z - 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x + 1, 0, fLoc.z);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    fTest = new IntVec3(fLoc.x + 1, 0, fLoc.z + 1);
                    tTerrAdj = fTest.GetTerrain(pMap);
                    rWetChance += getWetAdjMod(tTerrAdj);

                    // Potentially de-dry it out.
                    if (UnityEngine.Random.Range(0f, 1f) < rWetChance)
                    {
#if DEBUG
                        Log.Message($"{fLoc} wet {tTerr.defName} to {s_tMudMarsh.defName} {rWetChance}");
#endif
                        pMap.terrainGrid.SetTerrain(fLoc, s_tMudMarsh);
                    }
                }
            }
        }

        private static float getWetAdjMod(TerrainDef tTerrAdj)
        {
            if (isMudMarsh(tTerrAdj))
                return 0.001f;
            else if (isWater(tTerrAdj))
                return 0.003f;

            return 0f;
        }

        private static float getDryAdjMod(TerrainDef tTerrAdj)
        {
            if (isMudMarsh(tTerrAdj))
                return -0.01f;
            else if (isWater(tTerrAdj))
                return -0.03f;
            else
                return 0f;
        }

        private static bool canWet(TerrainDef tTerr)
        {
            return  tTerr.defName.ToLower().Contains("soil")  ||
                    tTerr.defName.ToLower().Contains("mossy") ||
                    tTerr.defName.ToLower().Contains("sand")  ||
                    tTerr.defName.ToLower().Contains("dirt")  ;
        }

        private static bool isMudMarsh(TerrainDef tTerr)
        {
            return  tTerr.defName.ToLower().Contains("mud")   ||
                    tTerr.defName.ToLower().Contains("marsh") ;
        }

        private static bool isWater(TerrainDef tTerr)
        {
            return  tTerr.defName.ToLower().Contains("water");
        }

        private static void getTerrains(Map pMap, out TerrainDef tMudMarsh, out TerrainDef tWater)
        {
            tMudMarsh = tWater = null;
            foreach (var tTerr in pMap.terrainGrid.topGrid)
            {
                if (tTerr.defName.Contains("Mud"))
                    tMudMarsh = tTerr;

                if (tTerr.defName.Contains("Marsh"))
                    tMudMarsh = tTerr;

                if (tTerr.defName.Contains("Shallow"))
                    tWater = tTerr;

                if (tMudMarsh != null && tWater != null)
                    return;
            }
        }

        /// <summary>
        /// This is the decompiled code for the original Pawn.Tick() function.
        /// </summary>
        /// <param name="_this"></param>
        private static void DefaultTick(Pawn _this)
        {
            if (DebugSettings.noAnimals && _this.RaceProps.Animal)
            {
                _this.Destroy(DestroyMode.Vanish);
            }
            else
            {
                // Run the base Tick code from ThingsWithComps.Tick().
                // This is pretty hack-y.
                var pCompsThing = _this as ThingWithComps;
                if (pCompsThing != null)
                {
                    var pComps = (List<ThingComp>) twc_comps.GetValue(pCompsThing);
                    for (int index = 0; index < pComps.Count; ++index)
                        pComps[index].CompTick();
                }

                if (Find.TickManager.TicksGame % 250 == 0)
                    _this.TickRare();
                if (_this.Spawned)
                    _this.pather.PatherTick();
                if (_this.Spawned)
                    _this.jobs.JobTrackerTick();
                if (_this.Spawned)
                {
                    _this.stances.StanceTrackerTick();
                    _this.verbTracker.VerbsTick();
                    _this.natives.NativeVerbsTick();
                    _this.Drawer.DrawTrackerTick();
                }
                _this.health.HealthTick();
                if (!_this.Dead)
                {
                    _this.mindState.MindStateTick();
                    _this.carryTracker.CarryHandsTick();
                    _this.needs.NeedsTrackerTick();
                }
                if (_this.equipment != null)
                    _this.equipment.EquipmentTrackerTick();
                if (_this.apparel != null)
                    _this.apparel.ApparelTrackerTick();
                if (_this.interactions != null)
                    _this.interactions.InteractionsTrackerTick();
                if (_this.caller != null)
                    _this.caller.CallTrackerTick();
                if (_this.skills != null)
                    _this.skills.SkillsTick();
                if (_this.inventory != null)
                    _this.inventory.InventoryTrackerTick();
                if (_this.drafter != null)
                    _this.drafter.DraftControllerTick();
                if (_this.relations != null)
                    _this.relations.SocialTrackerTick();
                if (_this.RaceProps.Humanlike)
                    _this.guest.GuestTrackerTick();
                _this.ageTracker.AgeTick();
                _this.records.RecordsTick();
            }
        }
    }
}
