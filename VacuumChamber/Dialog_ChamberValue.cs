using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VacuumChamber
{
  /// <summary>
  /// Game-pausing modal dialog featuring <c>[-]</c>, numeric text input, and <c>[+]</c> controls
  /// for adjusting either the target vacuum percentage or the pumping speed level.
  /// </summary>
  public class Dialog_ChamberValue : Window
  {
    private const float WindowWidth = 360f;
    private const float WindowHeight = 196f;
    private const float TitleHeight = 32f;
    private const float SummaryOffsetY = 34f;
    private const float SummaryHeight = 24f;
    private const float InputRowOffsetY = 68f;
    private const float RowHeight = 34f;
    private const float StepButtonWidth = 44f;
    private const float ControlGap = 8f;
    private const float UnitLabelWidth = 32f;
    private const float UnitLabelPadding = 4f;
    private const float FooterButtonWidth = 110f;

    private readonly object syncLock = new object();
    private readonly string title;
    private readonly int minValue;
    private readonly int maxValue;
    private readonly int shiftStep;
    private readonly string unitSuffix;
    private readonly Func<int, string>? summaryFormatter;
    private readonly Action<int> onConfirm;

    private int currentValue;
    private string editBuffer;

    /// <inheritdoc />
    public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

    /// <summary>
    /// Initializes a new <see cref="Dialog_ChamberValue"/> modal window that pauses the simulation while open.
    /// </summary>
    /// <param name="title">Localized dialog header title.</param>
    /// <param name="initialValue">Starting integer value.</param>
    /// <param name="minValue">Inclusive minimum bound.</param>
    /// <param name="maxValue">Inclusive maximum bound.</param>
    /// <param name="shiftStep">Step increment/decrement when Shift is held.</param>
    /// <param name="unitSuffix">Optional unit suffix shown after the numeric field.</param>
    /// <param name="summaryFormatter">Optional formatter for the live preview subtitle.</param>
    /// <param name="onConfirm">Callback invoked with the clamped integer on confirmation.</param>
    public Dialog_ChamberValue(
      string title,
      int initialValue,
      int minValue,
      int maxValue,
      int shiftStep,
      string unitSuffix,
      Func<int, string>? summaryFormatter,
      Action<int> onConfirm)
    {
      this.title = title ?? string.Empty;
      this.minValue = minValue;
      this.maxValue = Math.Max(minValue, maxValue);
      this.shiftStep = Math.Max(1, shiftStep);
      this.unitSuffix = unitSuffix ?? string.Empty;
      this.summaryFormatter = summaryFormatter;
      this.onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));

      currentValue = Math.Clamp(initialValue, this.minValue, this.maxValue);
      editBuffer = currentValue.ToString();

      forcePause = true;
      absorbInputAroundWindow = true;
      doCloseX = true;
      closeOnAccept = true;
      closeOnCancel = true;
      onlyOneOfTypeAllowed = true;
    }

    /// <inheritdoc />
    public override void DoWindowContents(Rect inRect)
    {
      TextAnchor prevAnchor = Text.Anchor;
      GameFont prevFont = Text.Font;

      try
      {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), title);

        Text.Font = GameFont.Small;
        if (summaryFormatter != null)
        {
          GUI.color = ColoredText.SubtleGrayColor;
          Widgets.Label(
            new Rect(inRect.x, inRect.y + SummaryOffsetY, inRect.width, SummaryHeight),
            summaryFormatter(GetEffectiveValue()));
          GUI.color = Color.white;
        }

        float rowY = inRect.y + InputRowOffsetY;
        float unitWidth = string.IsNullOrEmpty(unitSuffix) ? 0f : UnitLabelWidth;
        float inputWidth = inRect.width - (StepButtonWidth * 2f) - (ControlGap * 2f) - unitWidth;

        Rect minusRect = new Rect(inRect.x, rowY, StepButtonWidth, RowHeight);
        Rect inputRect = new Rect(minusRect.xMax + ControlGap, rowY, inputWidth, RowHeight);
        Rect unitRect = new Rect(
          inputRect.xMax + UnitLabelPadding,
          rowY,
          Math.Max(0f, unitWidth - UnitLabelPadding),
          RowHeight);
        Rect plusRect = new Rect(inRect.xMax - StepButtonWidth, rowY, StepButtonWidth, RowHeight);

        int step = Event.current?.shift == true ? shiftStep : 1;

        if (Widgets.ButtonText(minusRect, "-"))
        {
          SoundDefOf.Click.PlayOneShotOnCamera();
          AdjustValue(-step);
        }

        DrawNumericInput(inputRect);

        if (!string.IsNullOrEmpty(unitSuffix))
        {
          Text.Anchor = TextAnchor.MiddleLeft;
          Widgets.Label(unitRect, unitSuffix);
          Text.Anchor = TextAnchor.UpperLeft;
        }

        if (Widgets.ButtonText(plusRect, "+"))
        {
          SoundDefOf.Click.PlayOneShotOnCamera();
          AdjustValue(step);
        }

        float footerY = inRect.yMax - RowHeight;
        Rect cancelRect = new Rect(inRect.x, footerY, FooterButtonWidth, RowHeight);
        Rect confirmRect = new Rect(inRect.xMax - FooterButtonWidth, footerY, FooterButtonWidth, RowHeight);

        if (Widgets.ButtonText(cancelRect, "VC_Dialog_Cancel".Translate()))
        {
          Close();
          return;
        }

        if (Widgets.ButtonText(confirmRect, "VC_Dialog_Confirm".Translate()))
        {
          CommitAndClose();
        }
      }
      finally
      {
        Text.Font = prevFont;
        Text.Anchor = prevAnchor;
        GUI.color = Color.white;
      }
    }

    /// <inheritdoc />
    public override void OnAcceptKeyPressed()
    {
      CommitAndClose();
      Event.current?.Use();
    }

    private void DrawNumericInput(Rect inputRect)
    {
      int valueCopy;
      string bufferCopy;
      lock (syncLock)
      {
        valueCopy = currentValue;
        bufferCopy = editBuffer;
      }

      int originalValue = valueCopy;
      string originalBuffer = bufferCopy;
      Widgets.TextFieldNumeric(inputRect, ref valueCopy, ref bufferCopy, minValue, maxValue);

      if (valueCopy != originalValue || !string.Equals(bufferCopy, originalBuffer, StringComparison.Ordinal))
      {
        lock (syncLock)
        {
          currentValue = valueCopy;
          editBuffer = bufferCopy;
        }
      }
    }

    private int GetEffectiveValue()
    {
      lock (syncLock)
      {
        return VacuumPhysics.ParseClampedInt(editBuffer, currentValue, minValue, maxValue);
      }
    }

    private void AdjustValue(int delta)
    {
      lock (syncLock)
      {
        int effective = VacuumPhysics.ParseClampedInt(editBuffer, currentValue, minValue, maxValue);
        currentValue = Math.Clamp(effective + delta, minValue, maxValue);
        editBuffer = currentValue.ToString();
      }
    }

    private void CommitAndClose()
    {
      int finalValue = GetEffectiveValue();
      onConfirm(finalValue);
      Close();
    }
  }
}
