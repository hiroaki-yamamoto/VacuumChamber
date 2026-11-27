using System;

namespace VacuumChamber
{
  /// <summary>
  /// Pure, thread-safe ISO 3529 vacuum physics and chamber state calculations.
  /// Contains no mutable static state and no RimWorld map side effects.
  /// </summary>
  public static class VacuumPhysics
  {
    /// <summary>Default assumed ceiling height in meters (2.5 m).</summary>
    public const double DefaultCeilingHeightMeters = 2.5;

    /// <summary>Default pumping speed per level in cubic meters per hour (100 m³/h).</summary>
    public const double DefaultPumpingSpeedPerLevel = 100.0;

    /// <summary>Default electrical power draw per pumping speed level in watts (250 W).</summary>
    public const double DefaultPowerPerSpeedLevel = 250.0;

    /// <summary>Duration of one RimWorld rare tick (250 ticks) in in-game hours (0.1 h).</summary>
    public const double DefaultRareTickHours = 0.1;

    /// <summary>Snap tolerance so asymptotic pump-down reaches exact target cleanly.</summary>
    public const double SnapEpsilon = 1e-4;

    /// <summary>Minimum target vacuum percentage (0%).</summary>
    public const int MinVacuumPercent = 0;

    /// <summary>Maximum target vacuum percentage (100%).</summary>
    public const int MaxVacuumPercent = 100;

    /// <summary>Minimum pumping speed level (1).</summary>
    public const int MinPumpingSpeedLevel = 1;

    /// <summary>Maximum pumping speed level (50).</summary>
    public const int MaxPumpingSpeedLevel = 50;

    /// <summary>
    /// Computes the room volume in cubic meters as <c>roomCellCount * ceilingHeightMeters</c>.
    /// </summary>
    /// <param name="roomCellCount">Number of 1x1 m² tiles in the room.</param>
    /// <param name="ceilingHeightMeters">Ceiling height in meters (defaults to 2.5 m).</param>
    /// <returns>Volume in m³, or <c>0.0</c> if inputs are non-positive.</returns>
    /// <example>
    /// <code>
    /// double volume = VacuumPhysics.ComputeRoomVolumeM3(20);
    /// // volume == 50.0
    /// </code>
    /// </example>
    public static double ComputeRoomVolumeM3(
      int roomCellCount,
      double ceilingHeightMeters = DefaultCeilingHeightMeters)
    {
      if (roomCellCount <= 0 || ceilingHeightMeters <= 0.0)
      {
        return 0.0;
      }

      return roomCellCount * ceilingHeightMeters;
    }

    /// <summary>
    /// Computes the effective volumetric flow rate in cubic meters per hour
    /// (pumping speed during depressurization, or pressurization speed during pressurization).
    /// </summary>
    /// <param name="pumpingSpeedLevel">Speed level in [1, 50] (pumping / pressurization speed level).</param>
    /// <param name="speedPerLevel">Speed per level in m³/h (defaults to 100).</param>
    /// <returns>Volumetric flow rate in m³/h, or <c>0.0</c> if inputs are non-positive.</returns>
    /// <example>
    /// <code>
    /// double speed = VacuumPhysics.ComputePumpingSpeedM3PerHour(5);
    /// // speed == 500.0
    /// </code>
    /// </example>
    public static double ComputePumpingSpeedM3PerHour(
      int pumpingSpeedLevel,
      double speedPerLevel = DefaultPumpingSpeedPerLevel)
    {
      if (pumpingSpeedLevel <= 0 || speedPerLevel <= 0.0)
      {
        return 0.0;
      }

      return pumpingSpeedLevel * speedPerLevel;
    }

    /// <summary>
    /// Computes electrical power draw in watts (<c>250 W * level</c>).
    /// </summary>
    /// <param name="pumpingSpeedLevel">Speed level in [1, 50] (pumping / pressurization speed level).</param>
    /// <param name="powerPerLevel">Power per level in watts (defaults to 250).</param>
    /// <returns>Positive watt magnitude, or <c>0f</c> if inputs are non-positive.</returns>
    /// <example>
    /// <code>
    /// float watts = VacuumPhysics.ComputePowerDrawWatts(2);
    /// // watts == 500f
    /// </code>
    /// </example>
    public static float ComputePowerDrawWatts(
      int pumpingSpeedLevel,
      double powerPerLevel = DefaultPowerPerSpeedLevel)
    {
      if (pumpingSpeedLevel <= 0 || powerPerLevel <= 0.0)
      {
        return 0f;
      }

      return (float)(pumpingSpeedLevel * powerPerLevel);
    }

    /// <summary>
    /// Clamps a target vacuum percentage to [<see cref="MinVacuumPercent"/>, <see cref="MaxVacuumPercent"/>].
    /// </summary>
    /// <param name="value">Raw target vacuum percentage.</param>
    /// <returns>Clamped integer percentage in [0, 100].</returns>
    public static int ClampTargetVacuumPercent(int value)
    {
      return Math.Clamp(value, MinVacuumPercent, MaxVacuumPercent);
    }

    /// <summary>
    /// Clamps a speed level to [<see cref="MinPumpingSpeedLevel"/>, <paramref name="maxLevel"/>].
    /// </summary>
    /// <param name="value">Raw speed level.</param>
    /// <param name="maxLevel">Upper bound (defaults to 50).</param>
    /// <returns>Clamped integer speed level.</returns>
    public static int ClampPumpingSpeedLevel(
      int value,
      int maxLevel = MaxPumpingSpeedLevel)
    {
      int effectiveMax = Math.Max(MinPumpingSpeedLevel, maxLevel);
      return Math.Clamp(value, MinPumpingSpeedLevel, effectiveMax);
    }

    /// <summary>
    /// Evaluates the chamber operating state enforcing <c>Unpowered &gt; Inoperable &gt; Operational</c>.
    /// </summary>
    /// <param name="isPowered">Whether the chamber is receiving power and operational on the grid.</param>
    /// <param name="isRoomSealedIndoors">Whether the containing room is roofed and sealed indoors.</param>
    /// <returns>The active <see cref="ChamberState"/>.</returns>
    /// <example>
    /// <code>
    /// ChamberState s1 = VacuumPhysics.EvaluateState(false, false); // ChamberState.Unpowered
    /// ChamberState s2 = VacuumPhysics.EvaluateState(true, false);  // ChamberState.Inoperable
    /// ChamberState s3 = VacuumPhysics.EvaluateState(true, true);   // ChamberState.Operational
    /// </code>
    /// </example>
    public static ChamberState EvaluateState(bool isPowered, bool isRoomSealedIndoors)
    {
      if (!isPowered)
      {
        return ChamberState.Unpowered;
      }

      return isRoomSealedIndoors ? ChamberState.Operational : ChamberState.Inoperable;
    }

    /// <summary>
    /// Computes the next room vacuum fraction in [0, 1] over <paramref name="deltaHours"/>
    /// using ISO 3529-1 isothermal gas dynamics:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Depressurization (<c>currentVacuum &lt; targetVacuum</c>):
    /// <c>V * dp/dt = -S * p</c>, giving <c>v(t + dt) = 1 - (1 - v(t)) * exp(-S * dt / V)</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Pressurization (<c>currentVacuum &gt; targetVacuum</c>), treating <paramref name="pumpingSpeedLevel"/>
    /// as the pressurization speed level: ISO 3529-1 conductance-driven gas admission
    /// <c>q_pV = S * (p_atm - p)</c>, i.e. <c>V * dp/dt = S * (p_atm - p)</c>,
    /// giving <c>v(t + dt) = v(t) * exp(-S * dt / V)</c>.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="currentVacuum">Current room vacuum fraction in [0, 1].</param>
    /// <param name="targetVacuum">Target vacuum fraction in [0, 1].</param>
    /// <param name="roomCellCount">Number of 1x1 m² tiles in the room.</param>
    /// <param name="pumpingSpeedLevel">
    /// Speed level in [1, 50] (100 m³/h per level), interpreted as pumping speed level
    /// during depressurization and pressurization speed level during pressurization.
    /// </param>
    /// <param name="deltaHours">Time step in in-game hours (defaults to 0.1 h per TickRare).</param>
    /// <param name="ceilingHeightMeters">Ceiling height in meters (defaults to 2.5 m).</param>
    /// <param name="speedPerLevel">Volumetric flow rate per level in m³/h (defaults to 100 m³/h).</param>
    /// <returns>Updated vacuum fraction clamped to <paramref name="targetVacuum"/>.</returns>
    /// <example>
    /// <code>
    /// // Depressurize: 20 tiles (V = 50 m³), Lv 1 (S = 100 m³/h), dt = 0.1 h -> 1 - exp(-0.2) ≈ 0.18126925
    /// float depressurized = VacuumPhysics.ComputeNextVacuum(0f, 1f, 20, 1);
    /// // Pressurize: 20 tiles (V = 50 m³), Lv 1 (S = 100 m³/h), dt = 0.1 h -> 1 * exp(-0.2) ≈ 0.81873075
    /// float pressurized = VacuumPhysics.ComputeNextVacuum(1f, 0f, 20, 1);
    /// </code>
    /// </example>
    public static float ComputeNextVacuum(
      float currentVacuum,
      float targetVacuum,
      int roomCellCount,
      int pumpingSpeedLevel,
      float deltaHours = (float)DefaultRareTickHours,
      float ceilingHeightMeters = (float)DefaultCeilingHeightMeters,
      float speedPerLevel = (float)DefaultPumpingSpeedPerLevel)
    {
      float clampedTarget = Math.Clamp(targetVacuum, 0f, 1f);
      double clampedCurrent = Math.Clamp((double)currentVacuum, 0.0, 1.0);
      if (roomCellCount <= 0
        || pumpingSpeedLevel <= 0
        || deltaHours <= 0f
        || ceilingHeightMeters <= 0f
        || speedPerLevel <= 0f
        || Math.Abs(clampedCurrent - clampedTarget) <= 0.0)
      {
        return currentVacuum;
      }

      double volumeM3 = ComputeRoomVolumeM3(roomCellCount, ceilingHeightMeters);
      double speedM3PerHour = ComputePumpingSpeedM3PerHour(pumpingSpeedLevel, speedPerLevel);
      double decayFactor = Math.Exp(-(speedM3PerHour / volumeM3) * deltaHours);

      if (clampedCurrent < clampedTarget)
      {
        double nextVacuum = 1.0 - (1.0 - clampedCurrent) * decayFactor;
        if (nextVacuum >= clampedTarget || clampedTarget - nextVacuum <= SnapEpsilon)
        {
          return clampedTarget;
        }

        return (float)nextVacuum;
      }

      double pressurizedVacuum = clampedCurrent * decayFactor;
      if (pressurizedVacuum <= clampedTarget || pressurizedVacuum - clampedTarget <= SnapEpsilon)
      {
        return clampedTarget;
      }

      return (float)pressurizedVacuum;
    }
  }
}
