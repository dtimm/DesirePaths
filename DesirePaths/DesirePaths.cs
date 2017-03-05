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
using HugsLib.Settings;

namespace DesirePaths
{
    public class DesirePaths : ModBase
    {
        private SettingHandle<bool> killPlants;

        public override string ModIdentifier
        {
            get { return "DesirePaths"; }
        }

        public override void Initialize()
        {
            base.Initialize();
            killPlants = Settings.GetHandle<bool>("killPlants", "Trample Plants", "Pawn will kill plants by walking over them repeatedly.", true);
        }

        public override void Tick(int currentTick)
        {
            //base.Tick(currentTick);

            if (Current.Game?.Maps?.First() == null)
                return;

            foreach (Map map in Current.Game.Maps)
            {
                // every 10 ticks run trample pass.
                if (currentTick % 10 == 0)
                {
                    foreach (Pawn pPawn in map.mapPawns.AllPawns)
                        Trample(pPawn);
                }
            }
        }

        public void Trample(Pawn pawn)
        {
            // Only check while moving.
            if (!(pawn?.pather?.Moving ?? false))
                return;

            var fLoc = pawn.Position;

            // Some Pawns have a null map on each Tick()...? Kick them out.
            if (pawn.Map == null)
                return;

            if (killPlants)
                foreach (var plant in pawn.Map.thingGrid.ThingsAt(fLoc).OfType<Plant>())
                {
                    // Damage plants and kill them if needed.
                    // Anything smaller than a human doesn't damage grass.
                    int damage = (int) pawn.BodySize;
                    plant.HitPoints -= damage;

#if DEBUG
                    Logger.Message($"plant trampled by {pawn} : {plant.HitPoints}");
#endif

                    if (plant.HitPoints <= 0)
                        plant.Destroy(DestroyMode.Kill);
                }

            float depth = fLoc.GetSnowDepth(pawn.Map);
            if (depth > 0f)
            {
                depth -= 0.01f * pawn.BodySize;
                if (depth < 0.0f)
                    depth = 0.0f;
#if DEBUG
                Logger.Message($"snow trampled by {pawn} : {depth}");
#endif
                pawn.Map.snowGrid.SetDepth(fLoc, depth);
            }
        }
    }
}
