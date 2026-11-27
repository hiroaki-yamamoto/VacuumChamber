using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// XML-configurable properties for <see cref="CompVacuumChamber"/>.
  /// </summary>
  public class CompProperties_VacuumChamber : CompProperties
  {
    /// <summary>Assumed room ceiling height in meters for volume calculation (V = Area * height).</summary>
    public float ceilingHeightMeters = (float)VacuumPhysics.DefaultCeilingHeightMeters;

    /// <summary>Volumetric pumping speed per speed level in m³/h.</summary>
    public float pumpingSpeedPerLevel = (float)VacuumPhysics.DefaultPumpingSpeedPerLevel;

    /// <summary>Power consumption per pumping speed level in watts.</summary>
    public float powerPerSpeedLevel = (float)VacuumPhysics.DefaultPowerPerSpeedLevel;

    /// <summary>Maximum selectable pumping speed level.</summary>
    public int maxPumpingSpeedLevel = VacuumPhysics.MaxPumpingSpeedLevel;

    /// <summary>Default pumping speed level on initial construction.</summary>
    public int defaultPumpingSpeedLevel = VacuumPhysics.MinPumpingSpeedLevel;

    /// <summary>Default target vacuum percentage (0..100) on initial construction.</summary>
    public int defaultTargetVacuum = VacuumPhysics.MinVacuumPercent;

    /// <summary>Texture base path for the <see cref="ChamberState.Unpowered"/> graphic.</summary>
    public string unpoweredTexPath = "Things/Building/VacuumChamber/Unpowered/VacuumChamber_Unpowered";

    /// <summary>Texture base path for the <see cref="ChamberState.Operational"/> graphic.</summary>
    public string operationalTexPath = "Things/Building/VacuumChamber/Operational/VacuumChamber_Operational";

    /// <summary>Texture base path for the <see cref="ChamberState.Inoperable"/> graphic.</summary>
    public string inoperableTexPath = "Things/Building/VacuumChamber/Inoperable/VacuumChamber_Inoperable";

    /// <summary>
    /// Initializes a new instance of <see cref="CompProperties_VacuumChamber"/>
    /// and binds <see cref="CompProperties.compClass"/> to <see cref="CompVacuumChamber"/>.
    /// </summary>
    public CompProperties_VacuumChamber()
    {
      compClass = typeof(CompVacuumChamber);
    }
  }
}
