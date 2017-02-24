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
        public override string ModIdentifier
        {
            get { return "DesirePaths"; }
        }

        public override void Initialize()
        {
            base.Initialize();
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
                {
                    foreach (Pawn pPawn in pMap.mapPawns.AllPawns)
                        Trample(pPawn);
                }
            }
        }

        public void Trample(Pawn pPawn)
        {
            // Only check while moving.
            if (!(pPawn?.pather?.Moving ?? false))
                return;

            var fLoc = pPawn.Position;

            // Some Pawns have a null map on each Tick()...? Kick them out.
            if (pPawn.Map == null)
                return;


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
    }
}
