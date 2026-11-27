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
    private CompVacuumChamber? chamberComp;
    private Graphic? unpoweredGraphic;
    private Graphic? operationalGraphic;
    private Graphic? inoperableGraphic;
    private Color cachedDrawColor;
    private Color cachedDrawColorTwo;

    /// <summary>Gets the cached <see cref="CompVacuumChamber"/> attached to this building.</summary>
    public CompVacuumChamber? ChamberComp => chamberComp ??= GetComp<CompVacuumChamber>();

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

        EnsureStateGraphics(comp.Props);

        return comp.CurrentState switch
        {
          ChamberState.Operational => operationalGraphic ?? base.Graphic,
          ChamberState.Inoperable => inoperableGraphic ?? base.Graphic,
          _ => unpoweredGraphic ?? base.Graphic,
        };
      }
    }

    private void EnsureStateGraphics(CompProperties_VacuumChamber props)
    {
      Color colorOne = DrawColor;
      Color colorTwo = DrawColorTwo;

      if (unpoweredGraphic != null
        && operationalGraphic != null
        && inoperableGraphic != null
        && cachedDrawColor == colorOne
        && cachedDrawColorTwo == colorTwo)
      {
        return;
      }

      Shader shader = ShaderTypeDefOf.CutoutComplex.Shader;
      Vector2 drawSize = def.graphicData != null ? def.graphicData.drawSize : Vector2.one;

      unpoweredGraphic = GraphicDatabase.Get<Graphic_Multi>(
        props.unpoweredTexPath,
        shader,
        drawSize,
        colorOne,
        colorTwo);

      operationalGraphic = GraphicDatabase.Get<Graphic_Multi>(
        props.operationalTexPath,
        shader,
        drawSize,
        colorOne,
        colorTwo);

      inoperableGraphic = GraphicDatabase.Get<Graphic_Multi>(
        props.inoperableTexPath,
        shader,
        drawSize,
        colorOne,
        colorTwo);

      cachedDrawColor = colorOne;
      cachedDrawColorTwo = colorTwo;
    }
  }
}
