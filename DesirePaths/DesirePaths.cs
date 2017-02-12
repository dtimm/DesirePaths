using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace DesirePaths
{
    public static class DesirePaths
    {
        static readonly FieldInfo twc_comps = typeof(ThingWithComps).GetField ("comps", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void TickOverride(this Pawn _this)
        {
            // Run the normal Tick() code
            //DefaultTick(_this);

            var fLoc = _this.Position;

            // Some Pawns have a null map on each Tick()...? Kick them out.
            if (_this.Map == null)
                return;
            
            // Only check every 10 ticks
            if (_this.IsHashIntervalTick(10))
            {
#if DEBUG
                string sStamp = $"DesirePath - {GenTicks.TicksAbs} {_this.Name}";
#endif
                bool bFoundPlant = false;
                // Check for plants at this location.
                foreach (var pPlant in _this.Map.thingGrid.ThingsAt(fLoc).OfType<Plant>())
                {
                    // Damage plants and kill them if needed.
                    bFoundPlant = true;

                    // Anything smaller than a human doesn't damage grass.
                    int lDamage = (int)_this.BodySize;
                    pPlant.HitPoints -= lDamage;

                    if (pPlant.HitPoints <= 0)
                    {
#if DEBUG
                        Log.Message($"{sStamp}\t{pPlant.ToString()} HP: {pPlant.HitPoints}");
#endif
                        pPlant.Destroy(DestroyMode.Kill);
                    }
                }

                if (!bFoundPlant)
                {
                    // No plants here... check if it is soil.
                    var pTerr = _this.Map.terrainGrid.TerrainAt(fLoc);
                    if (pTerr.ToString().Contains("Soil"))
                    {
#if DEBUG
                        Log.Message($"{sStamp}\t {pTerr}");
#endif
                        pTerr = new TerrainDef() { description = "Mud" };
                        _this.Map.terrainGrid.SetTerrain(fLoc, pTerr);
                    }
                    else if (pTerr.ToString().Contains("Mud"))
                    {
#if DEBUG
                        Log.Message($"{sStamp}\t {pTerr}");
#endif
                        
                    }
                }

                var pSnow = _this.Map.snowGrid;
                var rDepth = pSnow.GetDepth(fLoc);
                if (rDepth > 0f)
                {
                    rDepth -= 0.005f * _this.BodySize;
                    if (rDepth < 0.0f)
                        rDepth = 0.0f;
#if DEBUG
                    Log.Message($"{sStamp}\tSnow: {rDepth}");
#endif
                    pSnow.SetDepth(fLoc, rDepth);
                }
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
