using System.Reflection;
using HarmonyLib;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Mod entry point that applies Harmony patches for Vacuum Chamber on RimWorld startup.
  /// </summary>
  [StaticConstructorOnStartup]
  public static class VacuumChamberMod
  {
    private const string HarmonyId = "hiroakiyamamoto.VacuumChamber";

    static VacuumChamberMod()
    {
      Harmony harmony = new Harmony(HarmonyId);
      harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
  }
}
