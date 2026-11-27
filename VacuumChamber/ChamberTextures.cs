using UnityEngine;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Cached UI command button textures loaded once on RimWorld startup.
  /// </summary>
  [StaticConstructorOnStartup]
  public static class ChamberTextures
  {
    /// <summary>Command gizmo icon for adjusting the target vacuum level.</summary>
    public static readonly Texture2D SetVacuumLevelIcon =
      ContentFinder<Texture2D>.Get("UI/Commands/VacuumChamber_SetVacuumLevel");

    /// <summary>Command gizmo icon for adjusting the pumping speed level.</summary>
    public static readonly Texture2D SetPumpingSpeedIcon =
      ContentFinder<Texture2D>.Get("UI/Commands/VacuumChamber_SetPumpingSpeed");
  }
}
