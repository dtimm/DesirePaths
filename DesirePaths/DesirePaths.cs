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
using HugsLib;

namespace DesirePaths
{
    public class DesirePaths : ModBase
    {
        private static readonly FieldInfo ppf_last = typeof(Verse.AI.Pawn_PathFollower).GetField ("lastCell", BindingFlags.NonPublic | BindingFlags.Instance);
        private static TerrainDef s_tMud, s_tMarsh, s_tWater;
        private static WeatherDef s_wCurrentWeather;

        public override string ModIdentifier
        {
            get { return "DesirePaths"; }
        }

        public override void Initialize()
        {
            base.Initialize();

            s_tMud = GenDefDatabase.GetDef(typeof(TerrainDef), "Mud", false) as TerrainDef;
            s_tWater = GenDefDatabase.GetDef(typeof(TerrainDef), "WaterShallow", false) as TerrainDef;
            s_tMarsh = GenDefDatabase.GetDef(typeof(TerrainDef), "Marsh", false) as TerrainDef;

#if DEBUG
            Logger.Message($"Loaded Mud Def: {s_tMud?.defName ?? "FAILED!"}");
            Logger.Message($"Loaded Water Def: {s_tWater?.defName ?? "FAILED!"}");
            Logger.Message($"Loaded Marsh Def: {s_tMarsh?.defName ?? "FAILED!"}");
#endif
        }

        public override void Tick(int currentTick)
        {
            //base.Tick(currentTick);

            if (Current.Game?.Maps?.First() == null)
                return;

            foreach (Map pMap in Current.Game.Maps)
            {
                // every 10 ticks run trample pass.
                if (currentTick % 10 == 0)
                    foreach (Pawn pPawn in pMap.mapPawns.AllPawns)
                        Trample(pPawn);

                // Every 250 ticks, run wet/dry pass.
                if (currentTick % 250 == 0)
                    DoRareThings(pMap);
            }
        }

        public void Trample(Pawn pPawn)
        {
            // Only check while moving.
            if (!(pPawn.pather?.Moving ?? false))
                return;

            var fLoc = pPawn.Position;

            // Some Pawns have a null map on each Tick()...? Kick them out.
            if (pPawn.Map == null)
                return;

            // Get previous cell via reflection.
            var fLast = (IntVec3) ppf_last.GetValue(pPawn.pather);

            // Check for plants at this location.
            bool bFoundPlant = false;
            foreach (var pPlant in pPawn.Map.thingGrid.ThingsAt(fLoc).OfType<Plant>())
            {
                // Damage plants and kill them if needed.
                bFoundPlant = true;

                // Anything smaller than a human doesn't damage grass.
                int lDamage = (int) pPawn.BodySize;
                pPlant.HitPoints -= lDamage;

                if (pPlant.HitPoints <= 0)
                {
#if DEBUG
                    Logger.Message($"plant trampled by {pPawn}");
#endif
                    pPlant.Destroy(DestroyMode.Kill);
                }
            }

            // 
            if (!bFoundPlant)
            {
                // No plants here... check if it is soil.
                var tTerr = fLoc.GetTerrain(pPawn.Map);
                if (canWet(tTerr))
                {
                    // Chance of altering this tile.
                    float rCrapChance = 0.0f;
                    var tLast = fLast.GetTerrain(pPawn.Map);

                    // If the pawn is moving out of existing mud or marsh.
                    if (isMudMarsh(tLast))
                        rCrapChance += 0.003f;

                    // If the pawn is unroofed and it is raining.
                    if (!fLoc.Roofed(pPawn.Map))
                        rCrapChance += 0.002f * s_wCurrentWeather.rainRate;

                    // Multiply by terrain type.
                    rCrapChance *= getWetMod(tTerr);

                    if (UnityEngine.Random.Range(0f, 1f) < rCrapChance)
                    {
#if DEBUG
                        Logger.Message($"trampled {tTerr.defName} to {s_tMud.defName}");
#endif
                        pPawn.Map.terrainGrid.SetTerrain(fLoc, s_tMud);
                    }
                }
            }

            float rDepth = fLoc.GetSnowDepth(pPawn.Map);
            if (rDepth > 0f)
            {
                rDepth -= 0.01f * pPawn.BodySize;
                if (rDepth < 0.0f)
                    rDepth = 0.0f;
#if DEBUG
                Logger.Message($"Snow: {rDepth}");
#endif
                pPawn.Map.snowGrid.SetDepth(fLoc, rDepth);
            }
        }

        public void DoRareThings(Map pMap)
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
                    
                    //Max dry chance of 10% per check.
                    if (rDryChance > 0.10f)
                        rDryChance = 0.10f;

                    var tDries = tTerr.driesTo;
                    if (isWater(tTerr))
                    {
                        // Lower change of drying water to marsh.
                        tDries = s_tMarsh;
                        rDryChance *= 0.25f;
                    }
                    // Marsh dries into mud.
                    else if (isMarsh(tTerr))
                        tDries = s_tMud;

                    // Potentially dry it out.
                    if (UnityEngine.Random.Range(0f, 1f) < rDryChance)
                    {
#if DEBUG
                        Logger.Message($"dry {tTerr.defName} to {tDries.defName}");
#endif
                        pMap.terrainGrid.SetTerrain(fLoc, tDries);
                    }
                }
                else if (s_wCurrentWeather.rainRate > 0f && (canWet(tTerr) || isMudMarsh(tTerr)))
                {
                    // Rain doesn't fall through roofs.
                    if (fLoc.Roofed(pMap))
                        return;

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

                    // Modifier for type.
                    rWetChance *= getWetMod(tTerr);

                    // Mud becomes marsh, marsh becomes water.
                    TerrainDef tWet = s_tMud;
                    if (isMud(tTerr))
                        tWet = s_tMarsh;
                    else if (isMarsh(tTerr))
                        tWet = s_tWater;

                    // Potentially de-dry it out.
                    if (UnityEngine.Random.Range(0f, 1f) < rWetChance)
                    {
#if DEBUG
                        Logger.Message($"wet {tTerr.defName} to {tWet.defName}");
#endif
                        pMap.terrainGrid.SetTerrain(fLoc, tWet);
                    }
                }
            }
        }

        private static float getWetAdjMod(TerrainDef tTerrAdj)
        {
            if (isMudMarsh(tTerrAdj))
                return 0.001f;
            else if (isWater(tTerrAdj))
                return 0.005f;

            return 0f;
        }

        private static float getDryAdjMod(TerrainDef tTerrAdj)
        {
            if (isMudMarsh(tTerrAdj))
                return -0.03f;
            else if (isWater(tTerrAdj))
                return -0.07f;
            else
                return 0f;
        }

        private static float getWetMod(TerrainDef tTerr)
        {
            string sLower = tTerr.defName.ToLower();

            if (sLower.Contains("soil"))
                return 1.5f;

            if (sLower.Contains("dirt"))
                return 1.0f;

            if (sLower.Contains("mossy"))
                return 0.8f;

            if (sLower.Contains("sand") && !sLower.Contains("stone"))
                return 0.5f;

            if (isMudMarsh(tTerr))
                return 0.25f;

            return 0f;
        }

        private static bool canWet(TerrainDef tTerr)
        {
            string sLower = tTerr.defName.ToLower();
            return sLower.Contains("soil") ||
                   sLower.Contains("mossy") ||
                   (sLower.Contains("sand") && !sLower.Contains("stone")) ||
                   sLower.Contains("dirt");
        }

        private static bool isMudMarsh(TerrainDef tTerr)
        {
            string sLower = tTerr.defName.ToLower();
            return  sLower.Contains("mud")   ||
                    sLower.Contains("marsh") ;
        }

        private static bool isMud(TerrainDef tTerr)
        {
            string sLower = tTerr.defName.ToLower();
            return sLower.Contains("mud");
        }

        private static bool isMarsh(TerrainDef tTerr)
        {
            string sLower = tTerr.defName.ToLower();
            return sLower.Contains("marsh");
        }

        private static bool isWater(TerrainDef tTerr)
        {
            string sLower = tTerr.defName.ToLower();
            return sLower.Contains("water");
        }
    }
}
