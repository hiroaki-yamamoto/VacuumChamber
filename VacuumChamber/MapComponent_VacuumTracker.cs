using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Tracks sealed planetary rooms containing active vacuum so Odyssey vacuum mechanics
  /// (room equalization, overlay rendering, pawn exposure, fire suppression, and persistence)
  /// operate cleanly on non-orbital maps without leaking outdoors.
  /// </summary>
  public class MapComponent_VacuumTracker : MapComponent
  {
    private static readonly ConditionalWeakTable<Map, MapComponent_VacuumTracker> MapTrackers =
      new ConditionalWeakTable<Map, MapComponent_VacuumTracker>();

    private readonly object syncLock = new object();
    private readonly HashSet<Room> trackedRooms = new HashSet<Room>();
    private readonly List<Room> sweepScratch = new List<Room>();

    private volatile bool hasActiveVacuum;

    // Save/load backing buffers for planetary room vacuum levels.
    private List<IntVec3> savedRoomCells = new List<IntVec3>();
    private List<float> savedRoomVacuums = new List<float>();

    /// <summary>
    /// Initializes a new instance of <see cref="MapComponent_VacuumTracker"/> for <paramref name="map"/>.
    /// </summary>
    /// <param name="map">Owning RimWorld map.</param>
    public MapComponent_VacuumTracker(Map map)
      : base(map)
    {
      MapTrackers.AddOrUpdate(map, this);
    }

    /// <summary>
    /// Retrieves the <see cref="MapComponent_VacuumTracker"/> for <paramref name="map"/> in O(1) time.
    /// </summary>
    /// <param name="map">Target map.</param>
    /// <returns>The tracker instance, or <c>null</c> if unavailable.</returns>
    public static MapComponent_VacuumTracker? GetTracker(Map? map)
    {
      if (map == null)
      {
        return null;
      }

      if (MapTrackers.TryGetValue(map, out MapComponent_VacuumTracker? tracker))
      {
        return tracker;
      }

      tracker = map.GetComponent<MapComponent_VacuumTracker>();
      if (tracker != null)
      {
        MapTrackers.AddOrUpdate(map, tracker);
      }

      return tracker;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="map"/> currently tracks at least one depressurized planetary room.
    /// </summary>
    /// <param name="map">Target map.</param>
    /// <returns><c>true</c> when planetary vacuum tracking is active on <paramref name="map"/>.</returns>
    public static bool HasActiveVacuumOnMap(Map? map)
    {
      return map != null
        && MapTrackers.TryGetValue(map, out MapComponent_VacuumTracker? tracker)
        && tracker.hasActiveVacuum;
    }

    /// <summary>
    /// Registers a sealed indoor room on a planetary map as containing vacuum.
    /// </summary>
    /// <param name="room">Room to track.</param>
    public void RegisterRoom(Room? room)
    {
      if (map == null
        || map.Biome == null
        || map.Biome.inVacuum
        || !RoomSealUtility.IsRoomSealedIndoors(room)
        || room == null)
      {
        return;
      }

      bool added;
      lock (syncLock)
      {
        added = trackedRooms.Add(room);
        hasActiveVacuum = trackedRooms.Count > 0;
      }

      if (added)
      {
        map.GetComponent<VacuumComponent>()?.Dirty();
      }
    }

    /// <summary>
    /// Removes a planetary room from vacuum tracking and marks the map's vacuum component dirty if removed.
    /// </summary>
    /// <param name="room">Room to remove from tracking.</param>
    public void UnregisterRoom(Room? room)
    {
      if (room == null || map == null || map.Biome == null || map.Biome.inVacuum)
      {
        return;
      }

      bool removed;
      lock (syncLock)
      {
        removed = trackedRooms.Remove(room);
        hasActiveVacuum = trackedRooms.Count > 0;
      }

      if (removed)
      {
        map.GetComponent<VacuumComponent>()?.Dirty();
      }
    }

    /// <summary>
    /// Vents any vacuum from an unsealed planetary room and removes it from tracking.
    /// </summary>
    /// <param name="room">Room that lost its roof or enclosure.</param>
    public void NotifyRoomUnsealed(Room? room)
    {
      if (room == null || map == null || map.Biome == null || map.Biome.inVacuum)
      {
        return;
      }

      if (!room.Dereferenced && RoomSealUtility.GetRawVacuum(room) > 0f)
      {
        room.Vacuum = 0f;
      }

      UnregisterRoom(room);
    }

    /// <inheritdoc />
    public override void MapComponentTick()
    {
      base.MapComponentTick();

      if (map == null
        || map.Biome == null
        || map.Biome.inVacuum
        || !hasActiveVacuum
        || !map.IsHashIntervalTick(250))
      {
        return;
      }

      bool changed = false;
      lock (syncLock)
      {
        if (trackedRooms.Count == 0)
        {
          hasActiveVacuum = false;
          return;
        }

        sweepScratch.Clear();
        foreach (Room room in trackedRooms)
        {
          if (room.Dereferenced
            || !RoomSealUtility.IsRoomSealedIndoors(room)
            || RoomSealUtility.GetRawVacuum(room) <= 0f)
          {
            sweepScratch.Add(room);
          }
        }

        for (int i = 0; i < sweepScratch.Count; i++)
        {
          Room invalid = sweepScratch[i];
          if (!invalid.Dereferenced && RoomSealUtility.GetRawVacuum(invalid) > 0f)
          {
            invalid.Vacuum = 0f;
          }

          trackedRooms.Remove(invalid);
          changed = true;
        }

        sweepScratch.Clear();
        hasActiveVacuum = trackedRooms.Count > 0;
      }

      if (changed)
      {
        map.GetComponent<VacuumComponent>()?.Dirty();
      }
    }

    /// <inheritdoc />
    public override void ExposeData()
    {
      base.ExposeData();

      if (Scribe.mode == LoadSaveMode.Saving)
      {
        savedRoomCells.Clear();
        savedRoomVacuums.Clear();

        if (map != null && map.Biome != null && !map.Biome.inVacuum)
        {
          lock (syncLock)
          {
            foreach (Room room in trackedRooms)
            {
              if (room.Dereferenced
                || !RoomSealUtility.IsRoomSealedIndoors(room)
                || RoomSealUtility.GetRawVacuum(room) <= 0f)
              {
                continue;
              }

              using IEnumerator<IntVec3> cellEnum = room.Cells.GetEnumerator();
              if (cellEnum.MoveNext())
              {
                savedRoomCells.Add(cellEnum.Current);
                savedRoomVacuums.Add(RoomSealUtility.GetRawVacuum(room));
              }
            }
          }
        }
      }

      Scribe_Collections.Look(ref savedRoomCells, "savedRoomCells", LookMode.Value);
      Scribe_Collections.Look(ref savedRoomVacuums, "savedRoomVacuums", LookMode.Value);

      if (Scribe.mode == LoadSaveMode.LoadingVars)
      {
        savedRoomCells ??= new List<IntVec3>();
        savedRoomVacuums ??= new List<float>();
      }
    }

    /// <inheritdoc />
    public override void FinalizeInit()
    {
      base.FinalizeInit();

      if (map == null || map.Biome == null || map.Biome.inVacuum)
      {
        return;
      }

      if (savedRoomCells.Count == 0 || savedRoomCells.Count != savedRoomVacuums.Count)
      {
        return;
      }

      for (int i = 0; i < savedRoomCells.Count; i++)
      {
        IntVec3 cell = savedRoomCells[i];
        float vacuum = savedRoomVacuums[i];
        if (!cell.InBounds(map) || vacuum <= 0f)
        {
          continue;
        }

        Room? room = cell.GetRoom(map);
        if (RoomSealUtility.IsRoomSealedIndoors(room) && room != null)
        {
          room.Vacuum = vacuum;
          RegisterRoom(room);
        }
      }

      savedRoomCells.Clear();
      savedRoomVacuums.Clear();
    }

    /// <inheritdoc />
    public override void MapRemoved()
    {
      base.MapRemoved();
      lock (syncLock)
      {
        trackedRooms.Clear();
        hasActiveVacuum = false;
      }

      if (map != null)
      {
        MapTrackers.Remove(map);
      }
    }
  }
}
