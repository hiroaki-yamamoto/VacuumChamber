using RimWorld;
using UnityEngine;
using Verse;

namespace VacuumChamber
{
  /// <summary>
  /// Rotatable, stuff-selectable 1x1 Spacer-tier building that renders distinct directional
  /// graphics for <see cref="ChamberState.Unpowered"/>, <see cref="ChamberState.Operational"/>,
  /// and <see cref="ChamberState.Inoperable"/>.
  /// </summary>
  public class Building_VacuumChamber : Building
  {
    private readonly object graphicSync = new object();
    private CompVacuumChamber? chamberComp;
    private StateGraphicSet? cachedGraphics;

    /// <summary>Gets the cached <see cref="CompVacuumChamber"/> attached to this building.</summary>
    public CompVacuumChamber? ChamberComp
    {
      get
      {
        CompVacuumChamber? existing = Volatile.Read(ref chamberComp);
        if (existing != null)
        {
          return existing;
        }

        CompVacuumChamber? resolved = GetComp<CompVacuumChamber>();
        if (resolved == null)
        {
          return null;
        }

        return Interlocked.CompareExchange(ref chamberComp, resolved, null) ?? resolved;
      }
    }

    /// <inheritdoc />
    public override Graphic Graphic
    {
      get
      {
        CompVacuumChamber? comp = ChamberComp;
        if (comp == null)
        {
          return base.Graphic;
        }

        StateGraphicSet graphics = GetOrCreateStateGraphics(comp.Props);
        return graphics.GetForState(comp.CurrentState) ?? base.Graphic;
      }
    }

    private StateGraphicSet GetOrCreateStateGraphics(CompProperties_VacuumChamber props)
    {
      Color colorOne = DrawColor;
      Color colorTwo = DrawColorTwo;

      // Lock-free fast path when draw colors match the cached immutable snapshot.
      StateGraphicSet? snapshot = Volatile.Read(ref cachedGraphics);
      if (snapshot != null && snapshot.MatchesColors(colorOne, colorTwo))
      {
        return snapshot;
      }

      lock (graphicSync)
      {
        snapshot = cachedGraphics;
        if (snapshot != null && snapshot.MatchesColors(colorOne, colorTwo))
        {
          return snapshot;
        }

        Shader shader = ShaderTypeDefOf.CutoutComplex.Shader;
        Vector2 drawSize = def.graphicData != null ? def.graphicData.drawSize : Vector2.one;

        StateGraphicSet updated = new StateGraphicSet(
          colorOne,
          colorTwo,
          GraphicDatabase.Get<Graphic_Multi>(props.unpoweredTexPath, shader, drawSize, colorOne, colorTwo),
          GraphicDatabase.Get<Graphic_Multi>(props.operationalTexPath, shader, drawSize, colorOne, colorTwo),
          GraphicDatabase.Get<Graphic_Multi>(props.inoperableTexPath, shader, drawSize, colorOne, colorTwo));

        Volatile.Write(ref cachedGraphics, updated);
        return updated;
      }
    }

    /// <summary>
    /// Immutable snapshot of state graphics for a specific (<see cref="ColorOne"/>, <see cref="ColorTwo"/>) pair,
    /// allowing atomic lock-free reads from <see cref="Graphic"/>.
    /// </summary>
    private sealed class StateGraphicSet
    {
      private readonly Color colorOne;
      private readonly Color colorTwo;
      private readonly Graphic? unpowered;
      private readonly Graphic? operational;
      private readonly Graphic? inoperable;

      public StateGraphicSet(
        Color colorOne,
        Color colorTwo,
        Graphic? unpowered,
        Graphic? operational,
        Graphic? inoperable)
      {
        this.colorOne = colorOne;
        this.colorTwo = colorTwo;
        this.unpowered = unpowered;
        this.operational = operational;
        this.inoperable = inoperable;
      }

      public bool MatchesColors(Color first, Color second)
      {
        return colorOne == first && colorTwo == second;
      }

      public Graphic? GetForState(ChamberState state)
      {
        return state switch
        {
          ChamberState.Operational => operational,
          ChamberState.Inoperable => inoperable,
          _ => unpowered,
        };
      }
    }
  }
}
