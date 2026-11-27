namespace VacuumChamber
{
  /// <summary>
  /// Represents the evaluated operating state of a Vacuum Chamber building.
  /// Priority order when conditions overlap: <see cref="Unpowered"/> &gt; <see cref="Inoperable"/> &gt; <see cref="Operational"/>.
  /// </summary>
  public enum ChamberState
  {
    /// <summary>
    /// The chamber has no electrical power, is flicked off, or is broken down.
    /// This state takes visual priority over <see cref="Inoperable"/>.
    /// </summary>
    Unpowered = 0,

    /// <summary>
    /// The chamber is powered, but its room is outdoors, touches the map edge, or is unroofed.
    /// </summary>
    Inoperable = 1,

    /// <summary>
    /// The chamber is powered and inside a sealed, roofed indoor room.
    /// </summary>
    Operational = 2,
  }
}
