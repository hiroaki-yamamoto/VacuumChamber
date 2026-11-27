using HarmonyLib;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Shared helper for checking whether a cell or room is a sealed, roofed indoor space,
  /// and reading raw unsanitized room vacuum.
  /// </summary>
  public static class RoomSealUtility
  {
    private static readonly AccessTools.FieldRef<Room, float> RoomVacuumFieldRef =
      AccessTools.FieldRefAccess<Room, float>("vacuum");

    /// <summary>
    /// Reads the raw unsanitized <c>Room.vacuum</c> field without RimWorld's 1% clamp or biome gate.
    /// </summary>
    /// <param name="room">Target room.</param>
    /// <returns>Raw vacuum fraction in [0, 1], or <c>0f</c> if <paramref name="room"/> is <c>null</c>.</returns>
    public static float GetRawVacuum(Room? room)
    {
      return room != null ? RoomVacuumFieldRef(room) : 0f;
    }

    /// <summary>
    /// Determines whether <paramref name="room"/> is a valid, sealed indoor room.
    /// </summary>
    /// <param name="room">The room to inspect.</param>
    /// <returns><c>true</c> if the room is enclosed and fully roofed; otherwise <c>false</c>.</returns>
    public static bool IsRoomSealedIndoors(Room? room)
    {
      return room != null
        && !room.Dereferenced
        && !room.PsychologicallyOutdoors
        && !room.TouchesMapEdge
        && room.OpenRoofCount == 0;
    }

    /// <summary>
    /// Determines whether <paramref name="loc"/> on <paramref name="map"/> is roofed and inside a sealed indoor room.
    /// </summary>
    /// <param name="loc">Map cell coordinates.</param>
    /// <param name="map">Target map.</param>
    /// <param name="room">Resolved room at <paramref name="loc"/>, or <c>null</c>.</param>
    /// <returns><c>true</c> if the cell is roofed and inside a sealed indoor room.</returns>
    public static bool IsCellSealedIndoors(IntVec3 loc, Map? map, out Room? room)
    {
      room = null;
      if (map == null || !loc.InBounds(map) || !map.roofGrid.Roofed(loc))
      {
        return false;
      }

      room = loc.GetRoom(map);
      return IsRoomSealedIndoors(room);
    }
  }
}
