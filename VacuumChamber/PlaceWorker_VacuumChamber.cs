using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Validates placement of a Vacuum Chamber: rejects water tiles and unsealed/outdoor locations,
  /// while permitting any roofed indoor tile regardless of foundation, flooring, fertility, or plantability.
  /// </summary>
  public class PlaceWorker_VacuumChamber : PlaceWorker
  {
    /// <inheritdoc />
    public override AcceptanceReport AllowsPlacing(
      BuildableDef checkingDef,
      IntVec3 loc,
      Rot4 rot,
      Map map,
      Thing? thingToIgnore = null,
      Thing? thing = null)
    {
      if (map == null || !loc.InBounds(map))
      {
        return false;
      }

      TerrainDef? terrain = loc.GetTerrain(map);
      if (terrain == null || terrain.IsWater)
      {
        return new AcceptanceReport("VC_CannotPlaceOnWater".Translate());
      }

      if (!RoomSealUtility.IsCellSealedIndoors(loc, map, out _))
      {
        return new AcceptanceReport("VC_MustPlaceIndoors".Translate());
      }

      return AcceptanceReport.WasAccepted;
    }
  }
}
