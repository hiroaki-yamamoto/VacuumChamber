using RimWorld;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Cached RimWorld definitions resolved once on startup by <see cref="DefOfHelper"/>.
  /// Eliminates repeated runtime <see cref="DefDatabase{T}"/> dictionary lookups.
  /// </summary>
  [DefOf]
  public static class VacuumChamberDefOf
  {
    /// <summary>ThingDef for the Vacuum Chamber building.</summary>
    public static ThingDef? VC_VacuumChamber;

    static VacuumChamberDefOf()
    {
      DefOfHelper.EnsureInitializedInCtor(typeof(VacuumChamberDefOf));
    }
  }
}
