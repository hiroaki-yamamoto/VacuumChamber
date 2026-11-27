using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Runtime component managing Vacuum Chamber state evaluation, dynamic power consumption,
  /// ISO 3529 room depressurization and pressurization, gizmos, inspect pane output, and save persistence.
  /// </summary>
  public class CompVacuumChamber : ThingComp
  {
    private const int TargetVacuumGizmoGroupKey = 918273641;
    private const int PumpingSpeedGizmoGroupKey = 918273642;

    private int targetVacuumPercent = VacuumPhysics.MinVacuumPercent;
    private int pumpingSpeedLevel = VacuumPhysics.MinPumpingSpeedLevel;
    private ChamberState currentState = ChamberState.Unpowered;
    private CompPowerTrader? powerComp;

    /// <summary>Gets the XML configuration properties for this chamber.</summary>
    public CompProperties_VacuumChamber Props => (CompProperties_VacuumChamber)props;

    /// <summary>Gets the current evaluated operating state.</summary>
    public ChamberState CurrentState => currentState;

    /// <summary>Gets the configured target vacuum percentage in [0, 100].</summary>
    public int TargetVacuumPercent => targetVacuumPercent;

    /// <summary>Gets the configured pumping speed level in [1, maxPumpingSpeedLevel].</summary>
    public int PumpingSpeedLevel => pumpingSpeedLevel;

    /// <summary>Gets the target vacuum as a normalized fraction in [0.0, 1.0].</summary>
    public float TargetVacuumFraction => targetVacuumPercent / 100f;

    /// <inheritdoc />
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);

      if (!respawningAfterLoad && Scribe.mode == LoadSaveMode.Inactive)
      {
        targetVacuumPercent = VacuumPhysics.ClampTargetVacuumPercent(Props.defaultTargetVacuum);
        pumpingSpeedLevel = VacuumPhysics.ClampPumpingSpeedLevel(
          Props.defaultPumpingSpeedLevel,
          Props.maxPumpingSpeedLevel);
      }
      else
      {
        targetVacuumPercent = VacuumPhysics.ClampTargetVacuumPercent(targetVacuumPercent);
        pumpingSpeedLevel = VacuumPhysics.ClampPumpingSpeedLevel(
          pumpingSpeedLevel,
          Props.maxPumpingSpeedLevel);
      }

      powerComp = parent.GetComp<CompPowerTrader>();
      UpdatePowerConsumption();
      RefreshState(forceDirtyMesh: false);
    }

    /// <inheritdoc />
    public override void ReceiveCompSignal(string signal)
    {
      base.ReceiveCompSignal(signal);

      if (signal == CompPowerTrader.PowerTurnedOnSignal
        || signal == CompPowerTrader.PowerTurnedOffSignal
        || signal == CompFlickable.FlickedOnSignal
        || signal == CompFlickable.FlickedOffSignal
        || signal == CompBreakdownable.BreakdownSignal)
      {
        UpdatePowerConsumption();
        RefreshState();
      }
    }

    /// <inheritdoc />
    public override void CompTickRare()
    {
      base.CompTickRare();

      UpdatePowerConsumption();
      RefreshState();

      if (currentState != ChamberState.Operational || !parent.Spawned || parent.Map == null)
      {
        return;
      }

      if (!RoomSealUtility.IsCellSealedIndoors(parent.Position, parent.Map, out Room? room) || room == null)
      {
        return;
      }

      float currentVacuum = RoomSealUtility.GetRawVacuum(room);
      bool isPlanetaryMap = !parent.Map.Biome.inVacuum;
      MapComponent_VacuumTracker? tracker = isPlanetaryMap
        ? MapComponent_VacuumTracker.GetTracker(parent.Map)
        : null;

      if (isPlanetaryMap && currentVacuum > 0f)
      {
        tracker?.RegisterRoom(room);
      }

      if (currentVacuum == TargetVacuumFraction)
      {
        return;
      }

      float deltaHours = (float)GenTicks.TickRareInterval / GenDate.TicksPerHour;
      float nextVacuum = VacuumPhysics.ComputeNextVacuum(
        currentVacuum,
        TargetVacuumFraction,
        room.CellCount,
        pumpingSpeedLevel,
        deltaHours,
        Props.ceilingHeightMeters,
        Props.pumpingSpeedPerLevel);

      room.Vacuum = nextVacuum;

      if (isPlanetaryMap)
      {
        if (nextVacuum > 0f)
        {
          tracker?.RegisterRoom(room);
        }
        else
        {
          tracker?.UnregisterRoom(room);
        }
      }
    }

    /// <summary>
    /// Re-evaluates <see cref="CurrentState"/> and dirties the map mesh whenever the visual state changes.
    /// </summary>
    /// <param name="forceDirtyMesh">If <c>true</c>, forces a map mesh rebuild even if the state is unchanged.</param>
    public void RefreshState(bool forceDirtyMesh = false)
    {
      if (powerComp == null && parent != null)
      {
        powerComp = parent.GetComp<CompPowerTrader>();
      }

      bool isPowered = powerComp != null && powerComp.PowerOn;
      bool isSealedIndoors = parent != null
        && parent.Spawned
        && RoomSealUtility.IsCellSealedIndoors(parent.Position, parent.Map, out _);

      ChamberState nextState = VacuumPhysics.EvaluateState(isPowered, isSealedIndoors);
      if (nextState != currentState || forceDirtyMesh)
      {
        currentState = nextState;
        if (parent != null && parent.Spawned && parent.Map != null)
        {
          parent.DirtyMapMesh(parent.Map);
        }
      }
    }

    /// <summary>
    /// Updates <see cref="CompPowerTrader.PowerOutput"/> to match the active pumping speed level.
    /// </summary>
    public void UpdatePowerConsumption()
    {
      if (powerComp == null && parent != null)
      {
        powerComp = parent.GetComp<CompPowerTrader>();
      }

      if (powerComp != null)
      {
        powerComp.PowerOutput = -VacuumPhysics.ComputePowerDrawWatts(
          pumpingSpeedLevel,
          Props.powerPerSpeedLevel);
      }
    }

    /// <inheritdoc />
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
      foreach (Gizmo gizmo in base.CompGetGizmosExtra())
      {
        yield return gizmo;
      }

      if (parent.Faction != Faction.OfPlayer)
      {
        yield break;
      }

      yield return new Command_Action
      {
        defaultLabel = "VC_SetVacuumLevel_Label".Translate(targetVacuumPercent),
        defaultDesc = "VC_SetVacuumLevel_Desc".Translate(),
        icon = ChamberTextures.SetVacuumLevelIcon,
        groupKey = TargetVacuumGizmoGroupKey,
        action = OpenTargetVacuumDialog,
      };

      string speedLabelKey = IsPressurizationMode(out _)
        ? "VC_SetPressurizationSpeed_Label"
        : "VC_SetPumpingSpeed_Label";

      yield return new Command_Action
      {
        defaultLabel = speedLabelKey.Translate(pumpingSpeedLevel),
        defaultDesc = "VC_SetPumpingSpeed_Desc".Translate(),
        icon = ChamberTextures.SetPumpingSpeedIcon,
        groupKey = PumpingSpeedGizmoGroupKey,
        action = OpenPumpingSpeedDialog,
      };
    }

    /// <inheritdoc />
    public override string CompInspectStringExtra()
    {
      StringBuilder sb = new StringBuilder();

      string stateKey = currentState switch
      {
        ChamberState.Operational => "VC_State_Operational",
        ChamberState.Inoperable => "VC_State_Inoperable",
        _ => "VC_State_Unpowered",
      };
      sb.Append(stateKey.Translate());

      sb.AppendLine();
      sb.Append("VC_Inspect_TargetVacuum".Translate(targetVacuumPercent));

      sb.AppendLine();
      bool isPressurizing = IsPressurizationMode(out Room? room);
      if (room != null)
      {
        double volumeM3 = VacuumPhysics.ComputeRoomVolumeM3(room.CellCount, Props.ceilingHeightMeters);
        sb.Append("VC_Inspect_RoomVacuum".Translate(
          (RoomSealUtility.GetRawVacuum(room) * 100f).ToString("F1"),
          volumeM3.ToString("F1")));
      }
      else
      {
        sb.Append("VC_Inspect_RoomVacuumOutdoors".Translate());
      }

      sb.AppendLine();
      double speedM3H = VacuumPhysics.ComputePumpingSpeedM3PerHour(
        pumpingSpeedLevel,
        Props.pumpingSpeedPerLevel);
      float watts = VacuumPhysics.ComputePowerDrawWatts(
        pumpingSpeedLevel,
        Props.powerPerSpeedLevel);
      string speedInspectKey = isPressurizing
        ? "VC_Inspect_PressurizationSpeed"
        : "VC_Inspect_PumpingSpeed";
      sb.Append(speedInspectKey.Translate(
        pumpingSpeedLevel,
        speedM3H.ToString("F0"),
        watts.ToString("F0")));

      return sb.ToString();
    }

    /// <inheritdoc />
    public override void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look(ref targetVacuumPercent, "targetVacuumPercent", Props.defaultTargetVacuum);
      Scribe_Values.Look(ref pumpingSpeedLevel, "pumpingSpeedLevel", Props.defaultPumpingSpeedLevel);

      if (Scribe.mode == LoadSaveMode.PostLoadInit)
      {
        targetVacuumPercent = VacuumPhysics.ClampTargetVacuumPercent(targetVacuumPercent);
        pumpingSpeedLevel = VacuumPhysics.ClampPumpingSpeedLevel(
          pumpingSpeedLevel,
          Props.maxPumpingSpeedLevel);
      }
    }

    private void OpenTargetVacuumDialog()
    {
      Find.WindowStack.Add(new Dialog_ChamberValue(
        "VC_Dialog_TargetVacuumTitle".Translate(),
        targetVacuumPercent,
        VacuumPhysics.MinVacuumPercent,
        VacuumPhysics.MaxVacuumPercent,
        shiftStep: 10,
        unitSuffix: "%",
        summaryFormatter: value => "VC_Dialog_TargetSummary".Translate(value),
        onConfirm: value =>
        {
          targetVacuumPercent = VacuumPhysics.ClampTargetVacuumPercent(value);
        }));
    }

    private bool IsPressurizationMode(out Room? sealedRoom)
    {
      if (parent != null
        && parent.Spawned
        && RoomSealUtility.IsCellSealedIndoors(parent.Position, parent.Map, out sealedRoom)
        && sealedRoom != null)
      {
        return RoomSealUtility.GetRawVacuum(sealedRoom) > TargetVacuumFraction;
      }

      sealedRoom = null;
      return false;
    }

    private void OpenPumpingSpeedDialog()
    {
      string titleKey = IsPressurizationMode(out _)
        ? "VC_Dialog_PressurizationSpeedTitle"
        : "VC_Dialog_PumpingSpeedTitle";

      Find.WindowStack.Add(new Dialog_ChamberValue(
        titleKey.Translate(),
        pumpingSpeedLevel,
        VacuumPhysics.MinPumpingSpeedLevel,
        Props.maxPumpingSpeedLevel,
        shiftStep: 5,
        unitSuffix: string.Empty,
        summaryFormatter: level =>
        {
          double speed = VacuumPhysics.ComputePumpingSpeedM3PerHour(level, Props.pumpingSpeedPerLevel);
          float watts = VacuumPhysics.ComputePowerDrawWatts(level, Props.powerPerSpeedLevel);
          return "VC_Dialog_SpeedSummary".Translate(speed.ToString("F0"), watts.ToString("F0"));
        },
        onConfirm: level =>
        {
          pumpingSpeedLevel = VacuumPhysics.ClampPumpingSpeedLevel(level, Props.maxPumpingSpeedLevel);
          UpdatePowerConsumption();
        }));
    }
  }
}
