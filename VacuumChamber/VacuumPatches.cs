using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Harmony patches allowing sealed Vacuum Chamber rooms to maintain and render vacuum
  /// consistently on both orbital (<c>BiomeDef.inVacuum == true</c>) and planetary maps.
  /// Intentionally leaves <see cref="Room.ExposedToSpace"/> unpatched so planetary outdoors remains pressurized.
  /// </summary>
  [HarmonyPatch]
  public static class VacuumPatches
  {
    private static readonly MethodInfo MapGetBiomeMethod =
      AccessTools.PropertyGetter(typeof(Map), nameof(Map.Biome));

    private static readonly FieldInfo BiomeInVacuumField =
      AccessTools.Field(typeof(BiomeDef), nameof(BiomeDef.inVacuum));

    private static readonly MethodInfo IsVacuumActiveOnMapMethod =
      AccessTools.Method(typeof(VacuumPatches), nameof(IsVacuumActiveOnMap));

    /// <summary>
    /// Returns <c>true</c> if vacuum mechanics should run on <paramref name="map"/>,
    /// either because the biome is natively in vacuum or because a Vacuum Chamber has depressurized a room.
    /// </summary>
    /// <param name="map">Target map.</param>
    /// <returns><c>true</c> when vacuum mechanics are active on <paramref name="map"/>.</returns>
    public static bool IsVacuumActiveOnMap(Map? map)
    {
      if (map?.Biome == null)
      {
        return false;
      }

      return map.Biome.inVacuum || MapComponent_VacuumTracker.HasActiveVacuumOnMap(map);
    }

    /// <summary>
    /// Streaming single-instruction lookahead transpiler that rewrites <c>Map.Biome.inVacuum</c>
    /// into <see cref="IsVacuumActiveOnMap(Map?)"/> in O(N) time and O(1) auxiliary space.
    /// </summary>
    private static IEnumerable<CodeInstruction> ReplaceBiomeInVacuumCheck(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original)
    {
      using IEnumerator<CodeInstruction> enumerator = instructions.GetEnumerator();
      if (!enumerator.MoveNext())
      {
        LogMissingBiomeCheck(original);
        yield break;
      }

      CodeInstruction previous = enumerator.Current;
      bool hasPending = true;
      int replacements = 0;

      while (enumerator.MoveNext())
      {
        CodeInstruction current = enumerator.Current;
        if (hasPending
          && previous.Calls(MapGetBiomeMethod)
          && current.LoadsField(BiomeInVacuumField))
        {
          CodeInstruction call = new CodeInstruction(OpCodes.Call, IsVacuumActiveOnMapMethod);
          call.labels.AddRange(previous.labels);
          call.blocks.AddRange(previous.blocks);
          yield return call;
          replacements++;
          hasPending = false;
          continue;
        }

        if (hasPending)
        {
          yield return previous;
        }

        previous = current;
        hasPending = true;
      }

      if (hasPending)
      {
        yield return previous;
      }

      if (replacements == 0)
      {
        LogMissingBiomeCheck(original);
      }
    }

    private static void LogMissingBiomeCheck(MethodBase original)
    {
      Log.Warning($"[VacuumChamber] Expected Map.Biome.inVacuum check not found in {original.DeclaringType?.FullName}.{original.Name}.");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(VacuumComponent), nameof(VacuumComponent.MapComponentDraw))]
    private static IEnumerable<CodeInstruction> VacuumComponent_MapComponentDraw_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(VacuumUtility), "EverInVacuum")]
    private static IEnumerable<CodeInstruction> VacuumUtility_EverInVacuum_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(VacuumUtility), nameof(VacuumUtility.PawnVacuumTickInterval))]
    private static IEnumerable<CodeInstruction> VacuumUtility_PawnVacuumTickInterval_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(HediffGiver_VacuumBurn), nameof(HediffGiver_VacuumBurn.OnIntervalPassed))]
    private static IEnumerable<CodeInstruction> HediffGiver_VacuumBurn_OnIntervalPassed_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
    private static IEnumerable<CodeInstruction> MouseoverReadout_MouseoverReadoutOnGUI_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.GetEffectiveVacuumForFire))]
    private static IEnumerable<CodeInstruction> FireUtility_GetEffectiveVacuumForFire_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlaySettings), "DoMapControls")]
    private static IEnumerable<CodeInstruction> PlaySettings_DoMapControls_Transpiler(
      IEnumerable<CodeInstruction> instructions,
      MethodBase original) => ReplaceBiomeInVacuumCheck(instructions, original);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(VacuumComponent), "ActiveOnMap", MethodType.Getter)]
    private static void VacuumComponent_ActiveOnMap_Postfix(
      VacuumComponent __instance,
      ref bool __result)
    {
      if (__result || !ModLister.OdysseyInstalled)
      {
        return;
      }

      Map? map = __instance.map;
      if (map?.Biome is { inVacuum: false })
      {
        __result = MapComponent_VacuumTracker.HasActiveVacuumOnMap(map);
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Room), nameof(Room.Vacuum), MethodType.Getter)]
    private static void Room_Vacuum_Postfix(Room __instance, ref float __result)
    {
      if (__result > 0f || !ModLister.OdysseyInstalled)
      {
        return;
      }

      Map? map = __instance.Map;
      if (map?.Biome == null
        || map.Biome.inVacuum
        || !MapComponent_VacuumTracker.HasActiveVacuumOnMap(map))
      {
        return;
      }

      if (RoomSealUtility.IsRoomSealedIndoors(__instance))
      {
        float raw = RoomSealUtility.GetRawVacuum(__instance);
        __result = raw <= Room.MinVaccum ? 0f : raw;
        return;
      }

      if (__instance.IsDoorway && map.regionAndRoomUpdater.Enabled)
      {
        float average = ComputeDoorwayAverageVacuum(__instance);
        __result = average <= Room.MinVaccum ? 0f : average;
        return;
      }

      if (RoomSealUtility.GetRawVacuum(__instance) > 0f)
      {
        __instance.Vacuum = 0f;
      }

      __result = 0f;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Room), nameof(Room.Notify_RoofChanged))]
    private static void Room_Notify_RoofChanged_Postfix(Room __instance)
    {
      Map? map = __instance.Map;
      if (__instance.Dereferenced || map == null)
      {
        return;
      }

      NotifyPlanetaryRoomIfUnsealed(__instance, map);
      RefreshChambersOnMap(map);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Room), nameof(Room.Notify_RoomShapeChanged))]
    private static void Room_Notify_RoomShapeChanged_Postfix(Room __instance)
    {
      Map? map = __instance.Map;
      if (map == null)
      {
        return;
      }

      if (!__instance.Dereferenced)
      {
        NotifyPlanetaryRoomIfUnsealed(__instance, map);
      }

      RefreshChambersOnMap(map);
    }

    private static float ComputeDoorwayAverageVacuum(Room doorway)
    {
      float sum = 0f;
      int adjacentCount = 0;
      List<Region> regions = doorway.Regions;
      for (int i = 0; i < regions.Count; i++)
      {
        Region region = regions[i];
        List<RegionLink> links = region.links;
        for (int j = 0; j < links.Count; j++)
        {
          Region? other = links[j].GetOtherRegion(region);
          Room? otherRoom = other?.Room;
          if (otherRoom != null && otherRoom != doorway)
          {
            if (RoomSealUtility.IsRoomSealedIndoors(otherRoom))
            {
              sum += RoomSealUtility.GetRawVacuum(otherRoom);
            }

            adjacentCount++;
          }
        }
      }

      return adjacentCount > 0 ? sum / adjacentCount : 0f;
    }

    private static void NotifyPlanetaryRoomIfUnsealed(Room room, Map map)
    {
      if (map.Biome is { inVacuum: false } && !RoomSealUtility.IsRoomSealedIndoors(room))
      {
        MapComponent_VacuumTracker.GetTracker(map)?.NotifyRoomUnsealed(room);
      }
    }

    /// <summary>
    /// Refreshes all Vacuum Chambers on <paramref name="map"/> in O(C) time with zero allocations,
    /// avoiding O(T_room) room-contents enumeration and runtime <see cref="DefDatabase{T}"/> lookups.
    /// </summary>
    private static void RefreshChambersOnMap(Map map)
    {
      ThingDef? chamberDef = VacuumChamberDefOf.VC_VacuumChamber;
      if (chamberDef == null || map.listerThings == null)
      {
        return;
      }

      List<Thing> chambers = map.listerThings.ThingsOfDef(chamberDef);
      for (int i = 0; i < chambers.Count; i++)
      {
        if (chambers[i] is Building_VacuumChamber chamber)
        {
          chamber.ChamberComp?.RefreshState();
        }
      }
    }
  }
}
