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
    private const float RareTickDeltaHours = (float)GenTicks.TickRareInterval / GenDate.TicksPerHour;
    private const int InspectStringInitialCapacity = 192;

    private int targetVacuumPercent = VacuumPhysics.MinVacuumPercent;
    private int pumpingSpeedLevel = VacuumPhysics.MinPumpingSpeedLevel;
    private int currentState = (int)ChamberState.Unpowered;
    private CompPowerTrader? powerComp;

    /// <summary>Gets the XML configuration properties for this chamber.</summary>
    public CompProperties_VacuumChamber Props => (CompProperties_VacuumChamber)props;

    /// <summary>Gets the current evaluated operating state.</summary>
    public ChamberState CurrentState => (ChamberState)Volatile.Read(ref currentState);

    /// <summary>Gets the configured target vacuum percentage in [0, 100].</summary>
    public int TargetVacuumPercent => Volatile.Read(ref targetVacuumPercent);

    /// <summary>Gets the configured pumping speed level in [1, maxPumpingSpeedLevel].</summary>
    public int PumpingSpeedLevel => Volatile.Read(ref pumpingSpeedLevel);

    /// <summary>Gets the target vacuum as a normalized fraction in [0.0, 1.0].</summary>
    public float TargetVacuumFraction => TargetVacuumPercent / 100f;

    /// <inheritdoc />
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);

      bool isFreshPlacement = !respawningAfterLoad && Scribe.mode == LoadSaveMode.Inactive;
      int rawTarget = isFreshPlacement ? Props.defaultTargetVacuum : Volatile.Read(ref targetVacuumPercent);
      int rawSpeed = isFreshPlacement ? Props.defaultPumpingSpeedLevel : Volatile.Read(ref pumpingSpeedLevel);

      Volatile.Write(ref targetVacuumPercent, VacuumPhysics.ClampTargetVacuumPercent(rawTarget));
      Volatile.Write(
        ref pumpingSpeedLevel,
        VacuumPhysics.ClampPumpingSpeedLevel(rawSpeed, Props.maxPumpingSpeedLevel));

      ResolvePowerComp();
      UpdatePowerConsumption();
      RefreshState(forceDirtyMesh: false);
    }

    /// <inheritdoc />
    public override void ReceiveCompSignal(string signal)
    {
      base.ReceiveCompSignal(signal);

      if (IsPowerOrStatusSignal(signal))
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
      ChamberState state = RefreshStateCore(forceDirtyMesh: false, out Room? room);

      if (state != ChamberState.Operational || room == null || !parent.Spawned || parent.Map == null)
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

      float targetFraction = TargetVacuumFraction;
      if (currentVacuum == targetFraction)
      {
        return;
      }

      int speedLevel = PumpingSpeedLevel;
      float nextVacuum = VacuumPhysics.ComputeNextVacuum(
        currentVacuum,
        targetFraction,
        room.CellCount,
        speedLevel,
        RareTickDeltaHours,
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
      RefreshStateCore(forceDirtyMesh, out _);
    }

    /// <summary>
    /// Updates <see cref="CompPowerTrader.PowerOutput"/> to match the active pumping speed level.
    /// </summary>
    public void UpdatePowerConsumption()
    {
      CompPowerTrader? power = ResolvePowerComp();
      if (power == null)
      {
        return;
      }

      float desiredOutput = -VacuumPhysics.ComputePowerDrawWatts(
        PumpingSpeedLevel,
        Props.powerPerSpeedLevel);

      if (power.PowerOutput != desiredOutput)
      {
        power.PowerOutput = desiredOutput;
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

      int targetPercent = TargetVacuumPercent;
      int speedLevel = PumpingSpeedLevel;

      yield return new Command_Action
      {
        defaultLabel = "VC_SetVacuumLevel_Label".Translate(targetPercent),
        defaultDesc = "VC_SetVacuumLevel_Desc".Translate(),
        icon = ChamberTextures.SetVacuumLevelIcon,
        groupKey = TargetVacuumGizmoGroupKey,
        action = OpenTargetVacuumDialog,
      };

      bool isPressurizing = TryGetSealedRoomVacuum(out _, out float rawVacuum)
        && VacuumPhysics.IsPressurizing(rawVacuum, targetPercent / 100f);

      string speedLabelKey = isPressurizing
        ? "VC_SetPressurizationSpeed_Label"
        : "VC_SetPumpingSpeed_Label";

      yield return new Command_Action
      {
        defaultLabel = speedLabelKey.Translate(speedLevel),
        defaultDesc = "VC_SetPumpingSpeed_Desc".Translate(),
        icon = ChamberTextures.SetPumpingSpeedIcon,
        groupKey = PumpingSpeedGizmoGroupKey,
        action = OpenPumpingSpeedDialog,
      };
    }

    /// <inheritdoc />
    public override string CompInspectStringExtra()
    {
      StringBuilder sb = new StringBuilder(InspectStringInitialCapacity);
      int targetPercent = TargetVacuumPercent;
      int speedLevel = PumpingSpeedLevel;

      string stateKey = CurrentState switch
      {
        ChamberState.Operational => "VC_State_Operational",
        ChamberState.Inoperable => "VC_State_Inoperable",
        _ => "VC_State_Unpowered",
      };
      sb.Append(stateKey.Translate());

      sb.AppendLine();
      sb.Append("VC_Inspect_TargetVacuum".Translate(targetPercent));

      sb.AppendLine();
      bool hasSealedRoom = TryGetSealedRoomVacuum(out Room? room, out float rawVacuum);
      bool isPressurizing = hasSealedRoom
        && VacuumPhysics.IsPressurizing(rawVacuum, targetPercent / 100f);

      if (hasSealedRoom && room != null)
      {
        double volumeM3 = VacuumPhysics.ComputeRoomVolumeM3(room.CellCount, Props.ceilingHeightMeters);
        sb.Append("VC_Inspect_RoomVacuum".Translate(
          (rawVacuum * 100f).ToString("F1"),
          volumeM3.ToString("F1")));
      }
      else
      {
        sb.Append("VC_Inspect_RoomVacuumOutdoors".Translate());
      }

      sb.AppendLine();
      double speedM3H = VacuumPhysics.ComputePumpingSpeedM3PerHour(
        speedLevel,
        Props.pumpingSpeedPerLevel);
      float watts = VacuumPhysics.ComputePowerDrawWatts(
        speedLevel,
        Props.powerPerSpeedLevel);
      string speedInspectKey = isPressurizing
        ? "VC_Inspect_PressurizationSpeed"
        : "VC_Inspect_PumpingSpeed";
      sb.Append(speedInspectKey.Translate(
        speedLevel,
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
        Volatile.Write(
          ref targetVacuumPercent,
          VacuumPhysics.ClampTargetVacuumPercent(targetVacuumPercent));
        Volatile.Write(
          ref pumpingSpeedLevel,
          VacuumPhysics.ClampPumpingSpeedLevel(pumpingSpeedLevel, Props.maxPumpingSpeedLevel));
      }
    }

    private ChamberState RefreshStateCore(bool forceDirtyMesh, out Room? sealedRoom)
    {
      sealedRoom = null;
      CompPowerTrader? power = ResolvePowerComp();
      bool isPowered = power != null && power.PowerOn;
      bool isSealedIndoors = parent != null
        && parent.Spawned
        && RoomSealUtility.IsCellSealedIndoors(parent.Position, parent.Map, out sealedRoom);

      if (!isSealedIndoors)
      {
        sealedRoom = null;
      }

      ChamberState nextState = VacuumPhysics.EvaluateState(isPowered, isSealedIndoors);
      int previousState = Interlocked.Exchange(ref currentState, (int)nextState);
      if (previousState != (int)nextState || forceDirtyMesh)
      {
        if (parent != null && parent.Spawned && parent.Map != null)
        {
          parent.DirtyMapMesh(parent.Map);
        }
      }

      return nextState;
    }

    private CompPowerTrader? ResolvePowerComp()
    {
      CompPowerTrader? existing = Volatile.Read(ref powerComp);
      if (existing != null || parent == null)
      {
        return existing;
      }

      CompPowerTrader? resolved = parent.GetComp<CompPowerTrader>();
      if (resolved == null)
      {
        return null;
      }

      return Interlocked.CompareExchange(ref powerComp, resolved, null) ?? resolved;
    }

    private bool TryGetSealedRoomVacuum(out Room? sealedRoom, out float rawVacuum)
    {
      if (parent != null
        && parent.Spawned
        && RoomSealUtility.IsCellSealedIndoors(parent.Position, parent.Map, out sealedRoom)
        && sealedRoom != null)
      {
        rawVacuum = RoomSealUtility.GetRawVacuum(sealedRoom);
        return true;
      }

      sealedRoom = null;
      rawVacuum = 0f;
      return false;
    }

    private static bool IsPowerOrStatusSignal(string signal)
    {
      return signal == CompPowerTrader.PowerTurnedOnSignal
        || signal == CompPowerTrader.PowerTurnedOffSignal
        || signal == CompFlickable.FlickedOnSignal
        || signal == CompFlickable.FlickedOffSignal
        || signal == CompBreakdownable.BreakdownSignal;
    }

    private void OpenTargetVacuumDialog()
    {
      Find.WindowStack.Add(new Dialog_ChamberValue(
        "VC_Dialog_TargetVacuumTitle".Translate(),
        TargetVacuumPercent,
        VacuumPhysics.MinVacuumPercent,
        VacuumPhysics.MaxVacuumPercent,
        shiftStep: 10,
        unitSuffix: "%",
        summaryFormatter: value => "VC_Dialog_TargetSummary".Translate(value),
        onConfirm: value =>
        {
          Volatile.Write(
            ref targetVacuumPercent,
            VacuumPhysics.ClampTargetVacuumPercent(value));
        }));
    }

    private void OpenPumpingSpeedDialog()
    {
      bool isPressurizing = TryGetSealedRoomVacuum(out _, out float rawVacuum)
        && VacuumPhysics.IsPressurizing(rawVacuum, TargetVacuumFraction);

      string titleKey = isPressurizing
        ? "VC_Dialog_PressurizationSpeedTitle"
        : "VC_Dialog_PumpingSpeedTitle";

      Find.WindowStack.Add(new Dialog_ChamberValue(
        titleKey.Translate(),
        PumpingSpeedLevel,
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
          Volatile.Write(
            ref pumpingSpeedLevel,
            VacuumPhysics.ClampPumpingSpeedLevel(level, Props.maxPumpingSpeedLevel));
          UpdatePowerConsumption();
        }));
    }
  }
}
