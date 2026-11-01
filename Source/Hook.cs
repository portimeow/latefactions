using HarmonyLib;
using RimWorld;
using Verse;

namespace LateFactions
{
	[StaticConstructorOnStartup]
	public static class Hook
	{
		static Hook()
		{
			var harmony = new Harmony("portimeow.factionlanguages.latefactions");
			harmony.Patch(
				AccessTools.Method(typeof(FactionManager), nameof(FactionManager.Add)),
				postfix: new HarmonyMethod(typeof(Hook), nameof(AddPostfix)));
		}

		public static void AddPostfix() => LateFactionAllocator.Pending = true;
	}
}
