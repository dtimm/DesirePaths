using System;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DesirePaths
{
	[StaticConstructorOnStartup]
	internal static class DetourInjector
	{
		private static Assembly Assembly => Assembly.GetAssembly(typeof(DetourInjector));

		private static string AssemblyName => Assembly.FullName.Split(',').First();

		static DetourInjector()
		{
			LongEventHandler.QueueLongEvent(Inject, "Initializing", true, null);
		}

		private static void Inject()
		{
			if (DoInject())
				Log.Message(AssemblyName + " injected.");
			else
				Log.Error(AssemblyName + " failed to get injected properly.");
		}

		private const BindingFlags UniversalBindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool DoInject()
		{
			// First MethodInfo is source method to detour
			// Second MethodInfo is our method taking its place
			MethodInfo Verse_Pawn_Tick = typeof(Verse.Pawn).GetMethod("Tick");
			MethodInfo DesirePaths_DesirePaths_TickOverride = typeof(DesirePaths).GetMethod("TickOverride");

			if (!Detours.TryDetourFromTo(Verse_Pawn_Tick, DesirePaths_DesirePaths_TickOverride))
            {
				ErrorDetouring("Pawn.Tick method");
				return false;
			}

			// You can do as many detours as you like.

			// All our detours must have succeeded. Hooray!
			return true;
		}

		// Just saves some writing for throwing errors on failed detours
		internal static void ErrorDetouring(string classmethod){
			Log.Error("Failed to inject " + classmethod + " detour!");
		}
	}
}