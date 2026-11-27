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
    private const int SweepIntervalTicks = 250;

    private static readonly ConditionalWeakTable<Map, MapComponent_VacuumTracker> MapTrackers =
      new ConditionalWeakTable<Map, MapComponent_VacuumTracker>();

    private readonly object syncLock = new object();
    private readonly HashSet<Room> trackedRooms = new HashSet<Room>();

    private volatile bool hasActiveVacuum;
    private VacuumComponent? cachedVacuumComponent;

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
      if (map != null)
      {
        MapTrackers.AddOrUpdate(map, this);
      }
    }

    private bool IsPlanetaryMap => map?.Biome is { inVacuum: false };

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
      if (!IsPlanetaryMap || !RoomSealUtility.IsRoomSealedIndoors(room) || room == null)
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
        DirtyVacuumComponent();
      }
    }

    /// <summary>
    /// Removes a planetary room from vacuum tracking and marks the map's vacuum component dirty if removed.
    /// </summary>
    /// <param name="room">Room to remove from tracking.</param>
    public void UnregisterRoom(Room? room)
    {
      if (room == null || !IsPlanetaryMap)
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
        DirtyVacuumComponent();
      }
    }

    /// <summary>
    /// Vents any vacuum from an unsealed planetary room and removes it from tracking.
    /// </summary>
    /// <param name="room">Room that lost its roof or enclosure.</param>
    public void NotifyRoomUnsealed(Room? room)
    {
      if (room == null || !IsPlanetaryMap)
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

      if (!IsPlanetaryMap
        || !hasActiveVacuum
        || !map.IsHashIntervalTick(SweepIntervalTicks))
      {
        return;
      }

      List<Room>? removedRooms = null;
      lock (syncLock)
      {
        if (trackedRooms.Count == 0)
        {
          hasActiveVacuum = false;
          return;
        }

        // Collect invalid rooms only when detected (zero allocations on steady-state sweeps).
        foreach (Room room in trackedRooms)
        {
          if (room.Dereferenced
            || !RoomSealUtility.IsRoomSealedIndoors(room)
            || RoomSealUtility.GetRawVacuum(room) <= 0f)
          {
            removedRooms ??= new List<Room>();
            removedRooms.Add(room);
          }
        }

        if (removedRooms != null)
        {
          for (int i = 0; i < removedRooms.Count; i++)
          {
            trackedRooms.Remove(removedRooms[i]);
          }

          hasActiveVacuum = trackedRooms.Count > 0;
        }
      }

      if (removedRooms == null)
      {
        return;
      }

      // Vent rooms and dirty RimWorld components strictly outside syncLock to avoid lock-order hazards.
      for (int i = 0; i < removedRooms.Count; i++)
      {
        Room invalid = removedRooms[i];
        if (!invalid.Dereferenced && RoomSealUtility.GetRawVacuum(invalid) > 0f)
        {
          invalid.Vacuum = 0f;
        }
      }

      DirtyVacuumComponent();
    }

    /// <inheritdoc />
    public override void ExposeData()
    {
      base.ExposeData();

      if (Scribe.mode == LoadSaveMode.Saving)
      {
        lock (syncLock)
        {
          savedRoomCells.Clear();
          savedRoomVacuums.Clear();

          if (IsPlanetaryMap)
          {
            foreach (Room room in trackedRooms)
            {
              if (room.Dereferenced || !RoomSealUtility.IsRoomSealedIndoors(room))
              {
                continue;
              }

              float rawVacuum = RoomSealUtility.GetRawVacuum(room);
              if (rawVacuum <= 0f)
              {
                continue;
              }

              using IEnumerator<IntVec3> cellEnum = room.Cells.GetEnumerator();
              if (cellEnum.MoveNext())
              {
                savedRoomCells.Add(cellEnum.Current);
                savedRoomVacuums.Add(rawVacuum);
              }
            }
          }
        }
      }

      Scribe_Collections.Look(ref savedRoomCells, "savedRoomCells", LookMode.Value);
      Scribe_Collections.Look(ref savedRoomVacuums, "savedRoomVacuums", LookMode.Value);

      if (Scribe.mode == LoadSaveMode.LoadingVars)
      {
        lock (syncLock)
        {
          savedRoomCells ??= new List<IntVec3>();
          savedRoomVacuums ??= new List<float>();
        }
      }
    }

    /// <inheritdoc />
    public override void FinalizeInit()
    {
      base.FinalizeInit();

      if (!IsPlanetaryMap)
      {
        ClearSavedBuffers();
        return;
      }

      List<IntVec3> cellsToRestore;
      List<float> vacuumsToRestore;
      lock (syncLock)
      {
        if (savedRoomCells.Count == 0 || savedRoomCells.Count != savedRoomVacuums.Count)
        {
          savedRoomCells.Clear();
          savedRoomVacuums.Clear();
          return;
        }

        cellsToRestore = new List<IntVec3>(savedRoomCells);
        vacuumsToRestore = new List<float>(savedRoomVacuums);
        savedRoomCells.Clear();
        savedRoomVacuums.Clear();
      }

      for (int i = 0; i < cellsToRestore.Count; i++)
      {
        IntVec3 cell = cellsToRestore[i];
        float vacuum = vacuumsToRestore[i];
        if (!cell.InBounds(map) || !float.IsFinite(vacuum) || vacuum <= 0f)
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
    }

    /// <inheritdoc />
    public override void MapRemoved()
    {
      base.MapRemoved();
      lock (syncLock)
      {
        trackedRooms.Clear();
        savedRoomCells.Clear();
        savedRoomVacuums.Clear();
        hasActiveVacuum = false;
      }

      Volatile.Write(ref cachedVacuumComponent, null);

      if (map != null)
      {
        MapTrackers.Remove(map);
      }
    }

    private void ClearSavedBuffers()
    {
      lock (syncLock)
      {
        savedRoomCells.Clear();
        savedRoomVacuums.Clear();
      }
    }

    private void DirtyVacuumComponent()
    {
      VacuumComponent? vacuumComp = Volatile.Read(ref cachedVacuumComponent);
      if (vacuumComp == null && map != null)
      {
        VacuumComponent? resolved = map.GetComponent<VacuumComponent>();
        if (resolved != null)
        {
          vacuumComp = Interlocked.CompareExchange(ref cachedVacuumComponent, resolved, null) ?? resolved;
        }
      }

      vacuumComp?.Dirty();
    }
  }
}
